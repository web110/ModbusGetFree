using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ModbusGetFree
{
    // Deliberately small HTTP/1.1 GET service: one request per connection, bounded
    // headers/time/workers; never dispatches operations to the UI or device bus.
    public sealed class AiReadServer : IDisposable
    {
        readonly TcpListener listener;
        readonly SemaphoreSlim clients = new SemaphoreSlim(4);
        volatile bool stopped;
        sealed class Publication
        {
            public readonly string Json;
            public readonly long Timestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            public Publication(string json) { Json = json; }
        }
        Publication snapshot = new Publication("{\"error\":\"snapshot_not_ready\"}");
        public string Token { get; private set; }
        public string BaseUrl { get; private set; }

        public AiReadServer(int port)
        {
            byte[] bytes = new byte[32];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
            Token = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start(8);
            BaseUrl = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;
            Task.Run((Func<Task>)Accept);
        }

        public void Publish(string json) { Interlocked.Exchange(ref snapshot, new Publication(json)); }

        async Task Accept()
        {
            try
            {
                while (!stopped)
                {
                    var client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    if (!clients.Wait(0)) { client.Close(); continue; }
                    // Own each accepted socket until its bounded request completes.
                    ThreadPool.QueueUserWorkItem(_ => {
                        try { Serve(client); }
                        catch (IOException) { }
                        catch (SocketException) { }
                        catch (ObjectDisposedException) { }
                        finally { client.Close(); clients.Release(); }
                    });
                }
            }
            catch (ObjectDisposedException) { }
            catch (SocketException) { }
        }

        void Serve(TcpClient client)
        {
            using (client)
            {
                client.ReceiveTimeout = 2000; client.SendTimeout = 2000;
                var stream = client.GetStream();
                var header = new StringBuilder();
                var deadline = DateTime.UtcNow.AddSeconds(3);
                while (header.Length < 8192 && DateTime.UtcNow < deadline)
                {
                    int b = stream.ReadByte();
                    if (b < 0) return;
                    header.Append((char)b);
                    if (header.Length >= 4 && header.ToString(header.Length - 4, 4) == "\r\n\r\n") break;
                }
                if (!header.ToString().EndsWith("\r\n\r\n")) { Reply(stream, 400, "Bad Request", "{}"); return; }
                string[] lines = header.ToString().Split(new[] { "\r\n" }, StringSplitOptions.None);
                string[] first = lines[0].Split(' ');
                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 1; i < lines.Length && lines[i].Length > 0; i++)
                {
                    int colon = lines[i].IndexOf(':');
                    if (colon < 1 || headers.ContainsKey(lines[i].Substring(0, colon))) { Reply(stream, 400, "Bad Request", "{}"); return; }
                    headers.Add(lines[i].Substring(0, colon), lines[i].Substring(colon + 1).Trim());
                }
                string host, origin, authorization;
                headers.TryGetValue("Host", out host);
                headers.TryGetValue("Origin", out origin);
                headers.TryGetValue("Authorization", out authorization);
                if (first.Length != 3 || host != new Uri(BaseUrl).Authority) { Reply(stream, 400, "Bad Request", "{}"); return; }
                if (origin != null && origin != BaseUrl) { Reply(stream, 403, "Forbidden", "{}"); return; }
                if (first[0] != "GET") { Reply(stream, 405, "Method Not Allowed", "{}"); return; }
                if (first[1].Split('?')[0] == "/demo")
                {
                    string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ai", "demo.html");
                    if (File.Exists(path)) Reply(stream, 200, "OK", File.ReadAllText(path), "text/html; charset=utf-8");
                    else Reply(stream, 404, "Not Found", "{}");
                    return;
                }
                if (authorization != "Bearer " + Token) { Reply(stream, 401, "Unauthorized", "{\"error\":\"token_required\"}"); return; }
                if (stopped) { Reply(stream, 503, "Service Unavailable", "{}"); return; }
                if (first[1] == "/v1/snapshot")
                {
                    var current = Interlocked.CompareExchange(ref snapshot, null, null);
                    double age = (System.Diagnostics.Stopwatch.GetTimestamp() - current.Timestamp)
                        / (double)System.Diagnostics.Stopwatch.Frequency;
                    if (age > 5) Reply(stream, 503, "Service Unavailable", "{\"error\":\"snapshot_expired\"}");
                    else Reply(stream, 200, "OK", current.Json);
                }
                else Reply(stream, 404, "Not Found", "{\"error\":\"not_found\"}");
            }
        }

        static void Reply(Stream stream, int code, string reason, string body, string type = "application/json; charset=utf-8")
        {
            byte[] bytes = Encoding.UTF8.GetBytes(body);
            byte[] head = Encoding.ASCII.GetBytes("HTTP/1.1 " + code + " " + reason + "\r\nContent-Type: " + type
                + "\r\nContent-Length: " + bytes.Length + "\r\nConnection: close\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nReferrer-Policy: no-referrer\r\n\r\n");
            stream.Write(head, 0, head.Length); stream.Write(bytes, 0, bytes.Length);
        }

        public void Dispose() { stopped = true; listener.Stop(); Publish("{\"error\":\"stopped\"}"); }
    }
}
