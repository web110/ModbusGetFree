using System;using System.IO;using System.Linq;using System.Reflection;using System.Drawing;using System.Windows.Forms;using ModbusGetFree;
class SmoothGridTests {
 static int count;static void Check(bool pass,string name){if(!pass)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
 static bool Buffered(Control control){return (bool)typeof(Control).GetProperty("DoubleBuffered",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(control,null);}
 [STAThread]static int Main(string[] args){try{
 Application.EnableVisualStyles();using(var host=new MainWindow(true)){
 host.Show();var doc=host.NewDocument(new DocumentState{Quantity=20,Rows=10});Application.DoEvents();
 int width=doc.Grid.Columns[1].Width;Console.WriteLine("Widths="+width+","+doc.Grid.Columns[0].Width+" Font="+doc.Grid.DefaultCellStyle.Font);
 Check(width<170&&doc.Grid.Columns[0].Width<130,"both default columns narrower");
 int digits=TextRenderer.MeasureText("0000000000000",doc.Grid.DefaultCellStyle.Font,Size.Empty,TextFormatFlags.NoPadding).Width;
 Check(width>=digits+8,"default value width fits thirteen digit characters");
 foreach(string text in new[]{"(?2) 0x0232","0.00979449","102106","-123456.789"})Check(TextRenderer.MeasureText(text,doc.Grid.DefaultCellStyle.Font,Size.Empty,TextFormatFlags.NoPadding).Width+8<=width,"representative value fits: "+text);
 Check(Buffered(doc.Grid),"grid drawing double buffered");
 var summary=(Label)typeof(DataWindow).GetField("summary",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(doc);
 var result=(Label)typeof(DataWindow).GetField("result",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(doc);
 Check(Buffered(summary)&&Buffered(result),"summary and response drawing double buffered");
 Check((bool)typeof(Control).GetMethod("GetStyle",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(summary,new object[]{ControlStyles.AllPaintingInWmPaint}),"summary background and text drawn in same paint pass");
 int changes=0;summary.TextChanged+=(s,e)=>changes++;
 for(int i=0;i<50;i++)doc.UpdateSummary();Check(changes==0,"unchanged summary does not rewrite text");
 for(int i=0;i<50;i++){doc.Tx++;doc.UpdateSummary();}
 Check(changes==50&&summary.Text.Contains("Tx = 50"),"changed Tx remains visible every refresh");
 int valueChanges=0;doc.Grid.CellValueChanged+=(s,e)=>valueChanges++;
 doc.RefreshValues();doc.RefreshValues();Check(valueChanges==0,"unchanged values do not invalidate cells through assignment");
 doc.State.Values[0]=123;doc.RefreshValues();Check(valueChanges==1&&Convert.ToString(doc.Grid[1,0].Value)=="123","only changed data cell updated");
 doc.Grid.Columns[1].Width=220;Check(doc.Grid.Columns[1].Width==220,"manual column resizing remains available");
 using(var bitmap=new Bitmap(doc.Width,doc.Height)){doc.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(args[0],"compact-preview.png"));}
 }
 Console.WriteLine("Checks: "+count+"; Exit: 0");return 0;
 }catch(Exception ex){Console.WriteLine(ex);return 1;}}
}

