using System;using System.IO;using System.Linq;using System.Drawing;using System.Reflection;using System.Threading.Tasks;using System.Windows.Forms;using ModbusGetFree;
class TestCenterFixTests{
 static int checks;static string folder;
 static void Check(bool pass,string text){if(!pass)throw new Exception(text);checks++;Console.WriteLine("PASS "+text);}
 static void Reject(Action action,string text){bool rejected=false;try{action();}catch{rejected=true;}Check(rejected,text);}
 static ToolStripItem Tc(MainWindow host){return host.Controls.OfType<ToolStrip>().SelectMany(t=>t.Items.Cast<ToolStripItem>()).First(t=>t.Tag is int&&(int)t.Tag==32826);}
 [STAThread]static void Main(string[] args){folder=args[0];Application.EnableVisualStyles();var host=new MainWindow(true);host.Shown+=async(s,e)=>{try{await Run(host);Console.WriteLine("Checks: "+checks+"; Exit: 0");Environment.Exit(0);}catch(Exception ex){Console.WriteLine(ex);Environment.Exit(1);}};Application.Run(host);}
 static async Task Run(MainWindow host){
 Check(Modbus.Hex(TestCenterWindow.BuildFrame("01,03.00 00 00 0A",true,false))=="01 03 00 00 00 0A C5 CD","commas dots spaces and CRC vector");
 Check(Modbus.Hex(TestCenterWindow.BuildFrame("01 03 00 00 00 0A",true,true))=="01 03 00 00 00 0A F2","LRC two's complement appended");
 Check(Modbus.Hex(TestCenterWindow.BuildFrame("01 03 00 00 00 0A C5 CD",false,false))=="01 03 00 00 00 0A C5 CD","unchecked Add Check sends exact bytes");
 Reject(()=>TestCenterWindow.BuildFrame("",false,false),"blank command rejected");Reject(()=>TestCenterWindow.BuildFrame("GG 03",true,false),"invalid hex rejected");
 Reject(()=>TestCenterWindow.BuildFrame(string.Join(" ",Enumerable.Repeat("01",255)),true,false),"CRC frame length bounded");
 var state=new DocumentState{Unit=9,Address=100,Quantity=3,Disabled=true};var doc=host.NewDocument(state);
 Tc(host).PerformClick();var window=host.OwnedForms.OfType<TestCenterWindow>().Single();
 Check(window.Text=="Test Center"&&window.CommandText==""&&window.Commands.Length==0,"TC opens reference-style blank command/list view");
 Check(!window.AddCheck&&!window.UseLrc,"reference defaults: unchecked Add Check and CRC selected");
 foreach(string name in new[]{"OpenList","SaveList","Clear","AddToList","Send","Exit","Copy"})Check(window.Controls.Find(name,true).Length==1,"reference button "+name);
 Check(window.Controls.Find("Preset",true).Length==0,"incorrect preset function panel removed");
 Check(((ComboBox)window.Controls.Find("Command",true)[0]).DropDownStyle==ComboBoxStyle.DropDown,"command dropdown allows editing and list selection");
 var toolstrip=host.Controls.OfType<ToolStrip>().First(t=>!(t is MenuStrip)&&!(t is StatusStrip));
 using(var bitmap=new Bitmap(toolstrip.Width,toolstrip.Height)){toolstrip.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(folder,"toolbar-preview.png"));}
 window.CommandText="04,03.00 00 00 0A";window.AddToList();window.CommandText="04 03 00 00 00 0A";window.AddToList();
 Check(window.Commands.Length==1&&window.Commands[0]=="04 03 00 00 00 0A","list canonicalizes commands and avoids duplicates");
 string file=Path.Combine(folder,"commands.txt");window.SaveList(file);
 using(var second=new TestCenterWindow(host)){second.OpenList(file);Check(second.Commands.SequenceEqual(window.Commands),"plain-text command list save/open");File.WriteAllText(Path.Combine(folder,"invalid-list.txt"),"GG 03");Reject(()=>second.OpenList(Path.Combine(folder,"invalid-list.txt")),"invalid list rejected");Check(second.Commands.Length==1,"failed list load preserves existing commands");}
 await window.Transmit();Check(window.Messages.Last().Contains("Connect before"),"disconnected send reports actionable error");
 host.Settings.Demo=true;await host.QuickConnect();window.AddCheck=true;window.UseLrc=false;await window.Transmit();
 Check(window.Messages.Any(x=>x.StartsWith("Tx: 04 03"))&&window.Messages.Last().StartsWith("Rx: 04 03 14"),"TC sends to selected raw slave and shows raw response");
 Check(doc.State.Unit==9&&doc.State.Address==100&&doc.State.Quantity==3,"TC does not rewrite current data definition");
 Check(window.CopyText().Contains("Tx: ")&&window.CopyText().Contains("Rx: "),"Copy exports received/sent records");
 var list=(ListBox)window.Controls.Find("TrafficLog",true)[0];list.SelectedIndex=list.Items.Count-1;Check(window.CopyText()==window.Messages.Last(),"Copy uses selection when present");list.ClearSelected();
 host.InFlight=true;await window.Transmit();Check(host.InFlight&&window.Messages.Last().Contains("Bus busy"),"busy send preserves other operation ownership");host.InFlight=false;
 ((Button)window.Controls.Find("Clear",true)[0]).PerformClick();Check(window.Messages.Length==0&&window.Commands.Length==1,"Clear clears result records but retains command list");
 window.CommandText="04 03 00 00 00 0A";await window.Transmit();
 using(var bitmap=new Bitmap(window.Width,window.Height)){window.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(folder,"test-center-preview.png"));}
 Tc(host).PerformClick();Check(host.OwnedForms.OfType<TestCenterWindow>().Count()==1,"repeated TC command reuses window");
 await host.Bus.Close();window.Close();
 }
}
