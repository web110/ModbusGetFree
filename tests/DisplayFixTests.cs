using System;using System.IO;using System.Linq;using System.Drawing;using System.Windows.Forms;using ModbusGetFree;
class DisplayFixTests {
 static int checks;
 static void Check(bool pass,string name){if(!pass)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
 [STAThread]static int Main(string[] args){try{
 Check(ValueCodec.Format(new ushort[]{0x0232},0,2,0,1,0)=="(?2) 0x0232","version Hex-ASCII matches reference");
 Check(ValueCodec.Format(new ushort[]{0x4142},0,2,0,1,0)=="(AB) 0x4142","ASCII byte order");
 foreach(ushort value in new ushort[]{0,0x0232,0x4142,0x3078,0x2020,0xffff}){
 string shown=ValueCodec.Display(new[]{value},0,2,0,1,0);
 Check(ValueCodec.Encode(shown,2,0,1,0)[0]==value,"Hex-ASCII display parses for write "+value);
 }
 Check(ValueCodec.Encode("0x0232 '.2'",2,0,1,0)[0]==0x0232,"previous hex text remains accepted");
 Check(ValueCodec.Encode("0x3078 '0x'",2,0,1,0)[0]==0x3078,"old ASCII containing 0x remains accepted");
 Check(ValueCodec.Encode("(0x) 0x3078",2,0,1,0)[0]==0x3078,"new ASCII containing 0x remains accepted");
 foreach(int order in new[]{0,1,2,3}){
 var small=ValueCodec.Encode("0.00979449134",6,order,1,0);var saved=(ushort[])small.Clone();
 Check(ValueCodec.Display(small,0,6,order,1,0)=="0.00979449","float six significant digits order "+order);
 var large=ValueCodec.Encode("102098.203",6,order,1,0);
 Check(ValueCodec.Display(large,0,6,order,1,0)=="102098","large float precision order "+order);
 Check(ValueCodec.Encode(ValueCodec.Format(small,0,6,order,1,0),6,order,1,0).SequenceEqual(saved),"full precision path preserves exact float words "+order);
 Check(small.SequenceEqual(saved),"display leaves raw registers unchanged "+order);
 }
 Application.EnableVisualStyles();using(var host=new MainWindow(true)){
 host.Show();Application.DoEvents();var state=new DocumentState {Title="Display reference comparison",Quantity=10,Rows=10};
 state.Definitions.Add(new RegisterDefinition{Address=0,Format=2});
 state.Definitions.Add(new RegisterDefinition{Address=1,Format=6});
 state.Definitions.Add(new RegisterDefinition{Address=3,Format=6,ColorAbove=102098.1});
 state.Names=new string[10];state.Values=new ushort[10];state.Names[0]="版本";state.Names[1]="EC (mS/cm)";state.Names[3]="电阻率 (Ω·cm)";
 state.Values[0]=0x0232;Array.Copy(ValueCodec.Encode("0.00979449134",6,0,1,0),0,state.Values,1,2);Array.Copy(ValueCodec.Encode("102098.203",6,0,1,0),0,state.Values,3,2);
 var doc=host.NewDocument(state);doc.RefreshValues();
 Check(Convert.ToString(doc.Grid[1,0].Value)=="(?2) 0x0232","grid uses reference Hex-ASCII text");
 Check(Convert.ToString(doc.Grid[1,1].Value)=="0.00979449"&&Convert.ToString(doc.Grid[1,3].Value)=="102098","grid uses six digit float display");
 Check(doc.DisplayGroup(2)=="--"&&doc.DisplayGroup(4)=="--","continuations unchanged");
 Check(doc.FormatGroup(1)!="0.00979449","chart log and editor retain high precision path");
 Check(doc.Grid[1,3].Style.ForeColor.ToArgb()==Color.Red.ToArgb(),"conditional colors compare full precision instead of rounded display");
 Check(doc.Grid[0,0].InheritedStyle.Alignment==DataGridViewContentAlignment.MiddleRight,"alias right aligned");
 Check(doc.Grid[1,1].InheritedStyle.Alignment==DataGridViewContentAlignment.MiddleRight,"values right aligned");
 Check(doc.DefinitionAt(1).FontName==UiFonts.Name,"reference font default");
 using(var bitmap=new Bitmap(doc.Width,doc.Height)){doc.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(args[0],"display-preview.png"));}
 }
 Console.WriteLine("Checks: "+checks+"; Exit: 0");return 0;
 }catch(Exception ex){Console.WriteLine(ex);return 1;}}
}
