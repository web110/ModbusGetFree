using System;using System.Linq;using System.Drawing;using System.Reflection;using System.IO;using System.Windows.Forms;using ModbusGetFree;
class OriginalFontTests{
 static int checks;
 static void Check(bool pass,string label){if(!pass)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
 static bool Matches(Font font){return font.Name==UiFonts.Name&&Math.Abs(font.SizeInPoints-9)<0.01&&font.Style==FontStyle.Regular;}
 [STAThread]static int Main(string[] args){try{
 Application.EnableVisualStyles();using(var host=new MainWindow(true)){
 host.Show();var doc=host.NewDocument(new DocumentState{Quantity=10});Application.DoEvents();
 Check(Matches(host.Font),"main UI matches observed Microsoft YaHei UI regular 9pt");
 foreach(var strip in host.Controls.OfType<ToolStrip>())Check(Matches(strip.Font),"menu/toolbar/status font "+strip.GetType().Name);
 Check(Matches(doc.Font)&&Matches(doc.Grid.Font)&&Matches(doc.Grid.DefaultCellStyle.Font),"alias table header and data default fonts match");
 Check(doc.DefinitionAt(0).FontName==UiFonts.Name&&doc.DefinitionAt(0).FontSize==9,"new register group inherits verified default");
 Check(Matches(doc.Grid[1,0].InheritedStyle.Font),"rendered value cell uses verified font");
 foreach(string field in new[]{"summary","result"})Check(Matches(((Control)typeof(DataWindow).GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(doc)).Font),"statistics font "+field);
 Check(Matches(doc.Grid.ContextMenuStrip.Font),"context menu font");
 using(var editor=new EditorDialog("Font preview",240)){Check(Matches(editor.Font),"generic dialog font");}
 using(var definition=new DefinitionDialog(doc.State,()=>{},()=>System.Threading.Tasks.Task.FromResult(0),()=>false)){Check(Matches(definition.Font),"read/write definition font");}
 using(var chart=new ChartWindow()){Check(Matches(chart.Font),"chart UI font");}
 using(var traffic=new TrafficWindow()){
 Check(Matches(traffic.Font),"communication controls font");
 Check(traffic.Controls.Find("TrafficText",true)[0].Font.Name=="Courier New","raw traffic remains monospace matching reference appearance");
 }
 doc.State.Names[0]="温度 / Temperature";doc.State.Values[0]=250;doc.RefreshValues();
 doc.Grid.CurrentCell=doc.Grid[1,0];doc.UpdateSelectedDefinitions(d=>{d.FontName="Verdana";d.FontSize=12;d.FontStyle=(int)FontStyle.Italic;});
 var clone=MainWindow.CloneDocument(doc.Snapshot());
 Check(clone.Definitions[0].FontName=="Verdana"&&clone.Definitions[0].FontSize==12&&clone.Definitions[0].FontStyle==2,"explicit user font persists unchanged");
 // 再用默认文档生成预览，不连接设备。
 doc.Close();
 var previewState=new DocumentState{Title="Original font — Microsoft YaHei UI 9 pt",Quantity=10,Names=new string[10],Values=new ushort[10]};
 previewState.Names[0]="温度 / Temperature";previewState.Names[1]="版本";previewState.Values[0]=250;
 var preview=host.NewDocument(previewState);
 preview.RefreshValues();
 using(var bitmap=new Bitmap(host.Width,host.Height)){host.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(args[0],"font-preview.png"));}
 }
 Console.WriteLine("Checks: "+checks+"; Exit: 0");return 0;
 }catch(Exception ex){Console.WriteLine(ex);return 1;}}
}

