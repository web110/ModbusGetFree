using System;using System.IO;using System.IO.Compression;using System.Linq;using System.Reflection;using System.Text;using System.Threading;using System.Threading.Tasks;using System.Windows.Forms;using System.Xml.Serialization;using ModbusGetFree;
class UpdateTests
{
 static int checks;static string root,payload;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);checks++;}
 static void Reject(Action action,string name){bool rejected=false;try{action();}catch{rejected=true;}Check(rejected,name);}
 static string Zip(string name, params string[] extra){string path=Path.Combine(root,name+".zip");using(var z=ZipFile.Open(path,ZipArchiveMode.Create)){z.CreateEntryFromFile(payload,"release/ModbusGetFree.exe");for(int i=0;i<extra.Length;i+=2){using(var w=new StreamWriter(z.CreateEntry(extra[i]).Open()))w.Write(extra[i+1]);}}return path;}
 static string Stage(string target){string stage=Path.Combine(UpdateService.CacheDirectory(target),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);return stage;}
 static string Json(string tag,string assets,bool draft=false,bool prerelease=false){return "{\"tag_name\":\""+tag+"\",\"draft\":"+draft.ToString().ToLower()+",\"prerelease\":"+prerelease.ToString().ToLower()+",\"assets\":["+assets+"]}";}
 static string Asset(string name,string url){return "{\"name\":\""+name+"\",\"browser_download_url\":\""+url+"\",\"size\":100}";}
 [STAThread]static void Main(string[] args){root=Path.GetFullPath(args[0]);payload=Path.GetFullPath(args[1]);Directory.CreateDirectory(root);typeof(UpdateService).GetField("CacheRoot",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,Path.Combine(root,"cache"));Application.EnableVisualStyles();Language.Initialize();using(var host=new MainWindow(true)){host.Shown+=async(s,e)=>{try{await Run(host);Console.WriteLine("Checks: "+checks);Environment.Exit(0);}catch(Exception ex){Console.WriteLine(ex);Environment.Exit(1);}};Application.Run(host);}}
 static async Task Run(MainWindow host)
 {
  Check(UpdateService.ParseVersion("v1.10")==UpdateService.ParseVersion("1.10.0.0"),"normalized version comparison");
  Check(UpdateService.ParseVersion("v1.11")>UpdateService.ParseVersion("v1.10"),"new version detected");
  Check(UpdateService.ParseVersion("1.9")<UpdateService.ParseVersion("1.10"),"numeric comparison, not string comparison");
  foreach(string bad in new[]{"", "latest", "v1.11-beta", "1"})Check(UpdateService.ParseVersion(bad)==null,"reject version "+bad);
  string repo="https://github.com/web110/ModbusGetFree", url=repo+"/releases/download/v1.11/ModbusGetFree-update.zip";
  Check(UpdateService.LatestUrl(repo)=="https://api.github.com/repos/web110/ModbusGetFree/releases/latest","repository API URL");
  Reject(()=>UpdateService.LatestUrl("http://github.com/a/b"),"reject HTTP repository");Reject(()=>UpdateService.LatestUrl("https://example.com/a/b"),"reject foreign host");
  string asset=Asset("ModbusGetFree-update.zip",url);
  Check(UpdateService.ParseRelease(Json("v1.11",asset),repo,"1.10").DownloadUrl==url,"select update asset");
  Check(UpdateService.ParseRelease(Json("v1.10",asset),repo,"1.10")==null,"same version ignored");Check(UpdateService.ParseRelease(Json("v1.9",asset),repo,"1.10")==null,"older version ignored");
  Check(UpdateService.ParseRelease(Json("v1.11",asset,true),repo,"1.10")==null,"draft ignored");Check(UpdateService.ParseRelease(Json("v1.11",asset,false,true),repo,"1.10")==null,"prerelease ignored");
  Check(UpdateService.ParseRelease(Json("v1.11",Asset("ModbusGetFree-update.zip","https://evil.example/app.zip")),repo,"1.10")==null,"foreign asset rejected");
  Check(UpdateService.ParseRelease(Json("v1.11",Asset("ModbusGetFree-update.zip","https://github.com/other/repo/releases/download/v1/a.zip")),repo,"1.10")==null,"other repository asset rejected");
  Check(UpdateService.ParseRelease(Json("v1.11",""),repo,"1.10")==null,"no asset ignored");
  Check(UpdateService.ParseRelease(Json("v1.11",Asset("ModbusGetFree-a.zip",url)+","+Asset("ModbusGetFree-b.zip",url)),repo,"1.10")==null,"ambiguous assets ignored");
  Check(!new Preferences().AllowUpdates,"fresh install default off");
  var prefs=new Preferences{AllowUpdates=true};using(var ms=new MemoryStream()){new XmlSerializer(typeof(Preferences)).Serialize(ms,prefs);ms.Position=0;Check(((Preferences)new XmlSerializer(typeof(Preferences)).Deserialize(ms)).AllowUpdates,"switch persisted");}
  foreach(string code in Language.Codes){Language.Code=code;using(var prompt=new UpdatePrompt("v1.11")){prompt.Show(host);Application.DoEvents();Check(prompt.Controls.Find("Upgrade now",true).Length==1,"immediate button "+code);Check(prompt.Controls.Find("Upgrade in background",true).Length==1,"background button "+code);Check(prompt.Controls.Find("Skip this time",true).Length==1,"skip button "+code);if(code!="en")Check(Language.Text("Upgrade now")!="Upgrade now","update translation "+code);prompt.Close();}}
  Language.Code="en";
  foreach(var pair in new[]{Tuple.Create("Upgrade now",DialogResult.Yes),Tuple.Create("Upgrade in background",DialogResult.No),Tuple.Create("Skip this time",DialogResult.Cancel)}){using(var prompt=new UpdatePrompt("v1.11")){var timer=new System.Windows.Forms.Timer{Interval=80};timer.Tick+=(s,e)=>{timer.Stop();((Button)prompt.Controls.Find(pair.Item1,true).Single()).PerformClick();};timer.Start();Check(prompt.ShowDialog(host)==pair.Item2,"actual prompt button action "+pair.Item1);timer.Dispose();}}
  string target=Path.Combine(root,"install","ModbusGetFree.exe");Directory.CreateDirectory(Path.GetDirectoryName(target));File.WriteAllText(target,"old executable");File.WriteAllText(Path.Combine(Path.GetDirectoryName(target),"preferences.xml"),"user settings");
  string zip=Zip("valid","release/assets/icons/test.png","new asset","release/preferences.xml","do not overwrite","release/user.mpdoc","do not overwrite","release/README.md","new readme");
  var release=new UpdateRelease{Version="v1.11",DownloadUrl=new Uri(zip).AbsoluteUri,Size=new FileInfo(zip).Length,Digest="sha256:"+UpdateService.Hash(zip)};
  Console.WriteLine("CACHE "+UpdateService.CacheDirectory(target)); string manifest=await UpdateService.DownloadAsync(release,target,CancellationToken.None);
  Check(File.ReadAllText(target)=="old executable","background download leaves running program unchanged");
  Check(UpdateService.FindPending(target)==manifest,"pending package survives next startup");
  var pending=UpdateService.ValidatePending(manifest,target);Check(pending.Files.Count==3,"only runtime assets and README selected");
  Check(!pending.Files.Any(f=>f.Name=="preferences.xml"||f.Name.EndsWith(".mpdoc")),"user settings and documents excluded");
  Reject(()=>UpdateService.ValidatePending(manifest,Path.Combine(root,"other","ModbusGetFree.exe")),"pending package bound to install path");
  string staged=Path.Combine(Path.GetDirectoryName(manifest),"content","README.md");File.AppendAllText(staged,"tamper");Reject(()=>UpdateService.ValidatePending(manifest,target),"pending hash detects tampering");File.WriteAllText(staged,"new readme");
  UpdateService.Install(manifest,target);Check(UpdateService.Hash(target)==UpdateService.Hash(payload),"staged executable installed");Check(File.ReadAllText(Path.Combine(Path.GetDirectoryName(target),"preferences.xml"))=="user settings","settings preserved on install");
  Check(File.ReadAllText(Path.Combine(Path.GetDirectoryName(target),"README.md"))=="new readme","README installed");
  File.WriteAllText(target,"rollback old executable");string readme=Path.Combine(Path.GetDirectoryName(target),"README.md");File.WriteAllText(readme,"rollback old README");File.SetAttributes(readme,FileAttributes.ReadOnly);
  try{Reject(()=>UpdateService.Install(manifest,target),"installation failure reported");Check(File.ReadAllText(target)=="rollback old executable","earlier replacements rolled back on later failure");Check(File.ReadAllText(readme)=="rollback old README","locked destination preserved");}finally{File.SetAttributes(readme,FileAttributes.Normal);}
  Reject(()=>UpdateService.Prepare(Zip("traversal","release/../escape.txt","x"),target,"v1.11",Stage(target)),"archive traversal rejected");
  Reject(()=>UpdateService.Prepare(Zip("absolute","/absolute.txt","x"),target,"v1.11",Stage(target)),"absolute archive path rejected");
  Reject(()=>UpdateService.Prepare(Zip("duplicates","release/README.md","one","release/readme.md","two"),target,"v1.11",Stage(target)),"case-insensitive duplicate rejected");
  Reject(()=>UpdateService.Prepare(zip,target,"v1.12",Stage(target)),"payload and tag version mismatch rejected");
  string libraryZip=Path.Combine(root,"renamed-library.zip");var bytes=File.ReadAllBytes(payload);int pe=BitConverter.ToInt32(bytes,0x3C);bytes[pe+23]|=0x20;using(var z=ZipFile.Open(libraryZip,ZipArchiveMode.Create)){using(var stream=z.CreateEntry("ModbusGetFree.exe").Open())stream.Write(bytes,0,bytes.Length);}Reject(()=>UpdateService.Prepare(libraryZip,target,"v1.11",Stage(target)),"DLL renamed to executable rejected");
  var cancellation=new CancellationTokenSource();cancellation.Cancel();bool cancelled=false;try{await UpdateService.DownloadAsync(release,target,cancellation.Token);}catch{cancelled=true;}Check(cancelled,"download cancellation");
  release.Digest="sha256:bad";bool mismatch=false;try{await UpdateService.DownloadAsync(release,target,CancellationToken.None);}catch(InvalidDataException){mismatch=true;}Check(mismatch,"download digest mismatch rejected");
 }
}




