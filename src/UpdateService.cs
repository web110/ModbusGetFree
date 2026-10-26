using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Xml;
using System.Xml.Serialization;

[assembly: AssemblyVersion("1.10.0.0")]
[assembly: AssemblyFileVersion("1.10.0.0")]
namespace ModbusGetFree
{
    public sealed class UpdateRelease
    {
        public string Version, DownloadUrl, Digest;
        public long Size;
    }
    public sealed class UpdateFile
    {
        public string Name, Hash;
    }
    public sealed class PendingUpdate
    {
        public string Target, Version;
        public List<UpdateFile> Files = new List<UpdateFile>();
    }
    public static class UpdateService
    {
        const long MaxBytes = 150 * 1024 * 1024;
        // Test assemblies can redirect staging without changing the user's update cache.
        internal static string CacheRoot = null;
        public static Version ParseVersion(string text)
        {
            Version value;
            if (string.IsNullOrWhiteSpace(text)) return null;
            text = text.Trim();
            if (text.StartsWith("v", StringComparison.OrdinalIgnoreCase)) text = text.Substring(1);
            if (!Version.TryParse(text, out value) || value.Major < 0 || value.Minor < 0) return null;
            return new Version(value.Major, value.Minor, Math.Max(0, value.Build), Math.Max(0, value.Revision));
        }
        public static string LatestUrl(string repository)
        {
            var uri = new Uri(repository);
            string[] parts = uri.AbsolutePath.Trim('/').Split('/');
            if (uri.Scheme != "https" || uri.Host != "github.com" || parts.Length != 2 ||
                parts.Any(p => !System.Text.RegularExpressions.Regex.IsMatch(p, @"^[A-Za-z0-9_.-]+$")))
                throw new InvalidDataException("Invalid GitHub repository.");
            return "https://api.github.com/repos/" + parts[0] + "/" + parts[1] + "/releases/latest";
        }
        public static UpdateRelease ParseRelease(string json, string repository, string currentVersion)
        {
            var data = new JavaScriptSerializer { MaxJsonLength = 2 * 1024 * 1024 }.Deserialize<Dictionary<string, object>>(json);
            if (Convert.ToBoolean(data["draft"]) || Convert.ToBoolean(data["prerelease"])) return null;
            string tag = Convert.ToString(data["tag_name"]);
            var version = ParseVersion(tag);
            if (version == null || version <= ParseVersion(currentVersion)) return null;
            string path = new Uri(repository).AbsolutePath.TrimEnd('/') + "/releases/download/";
            var candidates = new List<UpdateRelease>();
            foreach (Dictionary<string, object> asset in (IEnumerable)data["assets"])
            {
                string name = Convert.ToString(asset["name"]);
                if (!name.StartsWith("ModbusGetFree", StringComparison.OrdinalIgnoreCase) || !name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
                string download = Convert.ToString(asset["browser_download_url"]);
                Uri uri;
                long size = Convert.ToInt64(asset["size"]);
                if (!Uri.TryCreate(download, UriKind.Absolute, out uri) || uri.Scheme != "https" || uri.Host != "github.com" ||
                    !uri.AbsolutePath.StartsWith(path, StringComparison.Ordinal) || size <= 0 || size > MaxBytes) continue;
                object digest;
                candidates.Add(new UpdateRelease { Version = tag, DownloadUrl = download, Size = size,
                    Digest = asset.TryGetValue("digest", out digest) ? Convert.ToString(digest) : "" });
                // A deliberately named update archive takes precedence over full project archives.
                if (name.Equals("ModbusGetFree-update.zip", StringComparison.OrdinalIgnoreCase)) return candidates.Last();
            }
            return candidates.Count == 1 ? candidates[0] : null;
        }
        static WebClient Client()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var client = new WebClient();
            client.Headers[HttpRequestHeader.UserAgent] = "ModbusGetFree/" + AppBrand.Version;
            client.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
            return client;
        }
        public static async Task<UpdateRelease> CheckAsync(string repository, CancellationToken token)
        {
            using (var client = Client())
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                timeout.CancelAfter(15000);
                using (timeout.Token.Register(client.CancelAsync))
                {
                    try { return ParseRelease(await client.DownloadStringTaskAsync(LatestUrl(repository)), repository, AppBrand.Version); }
                    catch (WebException ex)
                    {
                        var response = ex.Response as HttpWebResponse;
                        if (response != null && response.StatusCode == HttpStatusCode.NotFound) return null;
                        throw;
                    }
                }
            }
        }
        public static string CacheDirectory(string target)
        {
            using (var sha = SHA256.Create())
            {
                string key = BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(target).ToUpperInvariant()))).Replace("-", "").Substring(0, 24);
                string root = CacheRoot ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ModbusGetFree", "Updates");
                return Path.Combine(root, key);
            }
        }
        public static string Hash(string path)
        {
            using (var sha = SHA256.Create()) using (var input = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
        }
        public static async Task<string> DownloadAsync(UpdateRelease release, string target, CancellationToken token)
        {
            string cache = CacheDirectory(target);
            Directory.CreateDirectory(cache);
            string stage = Path.Combine(cache, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            string zip = Path.Combine(stage, "download.zip");
            using (var client = Client())
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                timeout.CancelAfter(300000);
                client.DownloadProgressChanged += (s, e) => { if (e.BytesReceived > MaxBytes) client.CancelAsync(); };
                using (timeout.Token.Register(client.CancelAsync)) await client.DownloadFileTaskAsync(release.DownloadUrl, zip);
            }
            token.ThrowIfCancellationRequested();
            if (new FileInfo(zip).Length != release.Size) throw new InvalidDataException("Update download size mismatch.");
            if (!string.IsNullOrEmpty(release.Digest) && !string.Equals(release.Digest, "sha256:" + Hash(zip), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Update checksum mismatch.");
            string manifest = Prepare(zip, target, release.Version, stage);
            token.ThrowIfCancellationRequested();
            string pointer = Path.Combine(cache, "pending.txt");
            // Publish only a fully validated package, replacing an older pending package atomically.
            string temp = pointer + ".tmp";
            File.WriteAllText(temp, manifest);
            if (File.Exists(pointer)) File.Replace(temp, pointer, null); else File.Move(temp, pointer);
            File.Delete(zip);
            return manifest;
        }
        public static string Prepare(string zip, string target, string version, string stage)
        {
            var pending = new PendingUpdate { Target = Path.GetFullPath(target), Version = version };
            if (ParseVersion(version) == null) throw new InvalidDataException("Invalid update version.");
            string content = Path.Combine(stage, "content");
            Directory.CreateDirectory(content);
            using (var archive = ZipFile.OpenRead(zip))
            {
                long total = 0;
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var archiveNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in archive.Entries)
                {
                    string name = entry.FullName.Replace('\\', '/');
                    if (!archiveNames.Add(name)) throw new InvalidDataException("Duplicate archive file.");
                    if (name.StartsWith("/") || name.Contains(":") || name.Split('/').Any(p => p == ".." || p.EndsWith(".") || p.EndsWith(" ")) || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                        throw new InvalidDataException("Unsafe update archive path.");
                    total += entry.Length;
                    if (total > MaxBytes || archive.Entries.Count > 5000) throw new InvalidDataException("Update archive is too large.");
                }
                var executables = archive.Entries.Where(e => e.FullName.Replace('\\', '/').EndsWith("ModbusGetFree.exe", StringComparison.OrdinalIgnoreCase)).ToArray();
                if (executables.Length != 1) throw new InvalidDataException("Update must contain one ModbusGetFree.exe.");
                string exePath = executables[0].FullName.Replace('\\', '/');
                if (exePath.Split('/').Last() != "ModbusGetFree.exe") throw new InvalidDataException("Invalid update executable.");
                string prefix = exePath.Substring(0, exePath.Length - "ModbusGetFree.exe".Length);
                foreach (var entry in archive.Entries)
                {
                    string full = entry.FullName.Replace('\\', '/');
                    if (!full.StartsWith(prefix, StringComparison.Ordinal) || full.EndsWith("/")) continue;
                    string relative = full.Substring(prefix.Length);
                    if (!IsProgramFile(relative)) continue;
                    if (!names.Add(relative)) throw new InvalidDataException("Duplicate update file.");
                    string output = Path.Combine(content, relative.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(output));
                    entry.ExtractToFile(output);
                    pending.Files.Add(new UpdateFile { Name = relative, Hash = Hash(output) });
                }
            }
            string program = Path.Combine(content, "ModbusGetFree.exe");
            var assembly = AssemblyName.GetAssemblyName(program);
            if (assembly.Name != "ModbusGetFree" || ParseVersion(assembly.Version.ToString()) != ParseVersion(version) || !IsExecutable(program))
                throw new InvalidDataException("Update executable version does not match its release.");
            string manifest = Path.Combine(stage, "manifest.xml");
            using (var stream = File.Create(manifest)) new XmlSerializer(typeof(PendingUpdate)).Serialize(stream, pending);
            return manifest;
        }
        static bool IsExecutable(string path)
        {
            using (var reader = new BinaryReader(File.OpenRead(path)))
            {
                if (reader.ReadUInt16() != 0x5A4D) return false;
                reader.BaseStream.Position = 0x3C;
                long header = reader.ReadUInt32();
                if (header < 64 || header + 24 > reader.BaseStream.Length) return false;
                reader.BaseStream.Position = header;
                if (reader.ReadUInt32() != 0x00004550) return false;
                reader.BaseStream.Position = header + 22;
                return (reader.ReadUInt16() & 0x2000) == 0; // Reject a DLL renamed to .exe.
            }
        }
        public static bool IsProgramFile(string relative)
        {
            if (relative.Contains(":") || relative.Split('/').Any(p => p == ".." || p.Length == 0)) return false;
            return relative == "ModbusGetFree.exe" || relative == "LICENSE" || relative == "UPDATES.md" ||
                (relative.StartsWith("README", StringComparison.Ordinal) && !relative.Contains('/') && relative.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) ||
                (relative.StartsWith("assets/", StringComparison.Ordinal) && new[] { ".png", ".ico", ".svg" }.Contains(Path.GetExtension(relative).ToLowerInvariant()));
        }
        static PendingUpdate ReadManifest(string manifest)
        {
            using (var reader = XmlReader.Create(manifest, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                return (PendingUpdate)new XmlSerializer(typeof(PendingUpdate)).Deserialize(reader);
        }
        public static PendingUpdate ValidatePending(string manifest, string target)
        {
            string cache = Path.GetFullPath(CacheDirectory(target)) + Path.DirectorySeparatorChar;
            if (!Path.GetFullPath(manifest).StartsWith(cache, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid update staging directory.");
            var pending = ReadManifest(manifest);
            if (!string.Equals(pending.Target, Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase) || ParseVersion(pending.Version) == null || ParseVersion(pending.Version) <= ParseVersion(AppBrand.Version) ||
                pending.Files == null || pending.Files.Count == 0 || pending.Files.Count > 5000 || pending.Files.Count(f => f.Name == "ModbusGetFree.exe") != 1)
                throw new InvalidDataException("Invalid pending update.");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in pending.Files)
            {
                if (!IsProgramFile(file.Name) || !names.Add(file.Name) || Hash(Path.Combine(Path.GetDirectoryName(manifest), "content", file.Name)) != file.Hash)
                    throw new InvalidDataException("Pending update validation failed.");
            }
            var payload = AssemblyName.GetAssemblyName(Path.Combine(Path.GetDirectoryName(manifest), "content", "ModbusGetFree.exe"));
            if (payload.Name != "ModbusGetFree" || ParseVersion(payload.Version.ToString()) != ParseVersion(pending.Version) || !IsExecutable(Path.Combine(Path.GetDirectoryName(manifest), "content", "ModbusGetFree.exe"))) throw new InvalidDataException("Invalid pending executable version.");
            return pending;
        }
        public static string FindPending(string target)
        {
            string pointer = Path.Combine(CacheDirectory(target), "pending.txt");
            if (!File.Exists(pointer)) return null;
            string manifest = File.ReadAllText(pointer).Trim();
            ValidatePending(manifest, target);
            return manifest;
        }
        public static Process LaunchHelper(string manifest, string target)
        {
            ValidatePending(manifest, target);
            string helper = Path.Combine(Path.GetDirectoryName(manifest), "UpdateHelper.exe");
            File.Copy(target, helper, true);
            return Process.Start(new ProcessStartInfo(helper, "--apply-update " + Process.GetCurrentProcess().Id + " \"" + manifest + "\"") { UseShellExecute = false, CreateNoWindow = true });
        }
        public static void ApplyFromHelper(string parentId, string manifest)
        {
            string stage = Path.GetDirectoryName(Path.GetFullPath(manifest));
            try
            {
                var pending = ReadManifest(manifest);
                ValidatePending(manifest, pending.Target);
                int id;
                if (!int.TryParse(parentId, out id)) return;
                try { using (var parent = Process.GetProcessById(id)) { if (!parent.WaitForExit(120000)) return; } }
                catch (ArgumentException) { }
                Install(manifest, pending.Target);
                File.Delete(Path.Combine(CacheDirectory(pending.Target), "pending.txt"));
                Process.Start(new ProcessStartInfo(pending.Target) { WorkingDirectory = Path.GetDirectoryName(pending.Target), UseShellExecute = true });
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(stage, "update-error.txt"), ex.ToString());
                System.Windows.Forms.MessageBox.Show(Language.Text("Update installation failed. Your previous version was kept.") + "\r\n" + Path.Combine(stage, "update-error.txt"), "ModbusGetFree");
            }
        }
        public static void Install(string manifest, string target)
        {
            var pending = ValidatePending(manifest, target);
            string stage = Path.GetDirectoryName(manifest), destination = Path.GetDirectoryName(target);
            string backup = Path.Combine(stage, "backup");
            var applied = new List<UpdateFile>();
            foreach (var file in pending.Files)
            {
                string output = Path.Combine(destination, file.Name);
                if (File.Exists(output) && (File.GetAttributes(output) & FileAttributes.ReparsePoint) != 0) throw new IOException("Update destination contains a link.");
                for (var dir = new DirectoryInfo(Path.GetDirectoryName(output)); dir != null; dir = dir.Parent)
                {
                    if (dir.Exists && (dir.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Update destination contains a link.");
                    if (dir.FullName.Equals(destination, StringComparison.OrdinalIgnoreCase)) break;
                }
                if (File.Exists(output))
                {
                    string saved = Path.Combine(backup, file.Name);
                    Directory.CreateDirectory(Path.GetDirectoryName(saved));
                    File.Copy(output, saved, true);
                }
            }
            try
            {
                foreach (var file in pending.Files)
                {
                    string output = Path.Combine(destination, file.Name);
                    Directory.CreateDirectory(Path.GetDirectoryName(output));
                    string replacement = output + ".update-" + Guid.NewGuid().ToString("N");
                    try
                    {
                        File.Copy(Path.Combine(stage, "content", file.Name), replacement);
                        if (File.Exists(output)) File.Replace(replacement, output, null); else File.Move(replacement, output);
                        applied.Add(file);
                    }
                    finally { if (File.Exists(replacement)) File.Delete(replacement); }
                }
            }
            catch
            {
                foreach (var file in applied.AsEnumerable().Reverse())
                {
                    string output = Path.Combine(destination, file.Name), saved = Path.Combine(backup, file.Name);
                    if (File.Exists(saved)) File.Copy(saved, output, true); else if (File.Exists(output)) File.Delete(output);
                }
                throw;
            }
        }
    }
}



