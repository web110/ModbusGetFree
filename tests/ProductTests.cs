using System;using System.IO;using System.Linq;using System.Drawing;using System.Threading.Tasks;using System.Windows.Forms;using ModbusGetFree;
class ProductTests
{
 static int count;static string output;
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);count++;Console.WriteLine("PASS "+label);}
 static T Find<T>(Control c,string name)where T:Control{return (T)c.Controls.Find(name,true).First();}
 [STAThread]static void Main(string[] args){output=args[0];Application.EnableVisualStyles();var host=new MainWindow(true);host.Shown+=async(s,e)=>{try{await Run(host);Console.WriteLine("Checks: "+count+"; Exit: 0");Environment.Exit(0);}catch(Exception ex){Console.WriteLine(ex);Environment.Exit(1);}};Application.Run(host);}
 static async Task Run(MainWindow host)
 {
  var help=host.Controls.OfType<MenuStrip>().Single().Items.OfType<ToolStripMenuItem>().Last();
  var helpItems=help.DropDownItems.OfType<ToolStripMenuItem>().Select(i=>i.Text.Replace("&","")).ToArray();
  Check(helpItems.Length==2&&helpItems[0]=="Help Topics"&&helpItems[1].Contains("About ModbusGetFree"),"Help only has topics and About");
  Check(host.Text.StartsWith("ModbusGetFree")&&host.Icon!=null,"product title and application icon");
  var doc=host.NewDocument(new DocumentState{Disabled=true});host.Settings.Demo=true;await host.QuickConnect();
  foreach(int fn in new[]{5,6,15,16,17,22,23})using(var d=new FunctionDialog(host,fn))
  {
   d.Show(host);Check(Find<Button>(d,"Cancel")!=null,"Cancel FC"+fn);
   Check(Find<Button>(d,"Send").Visible&&Find<Button>(d,"Cancel").Visible,"visible action buttons FC"+fn);
   if(fn==5){Check(Find<RadioButton>(d,"ValueOff").Checked,"single coil default is Off");Find<RadioButton>(d,"ValueOn").Checked=true;Find<RadioButton>(d,"UseMultipleFunction").Checked=true;Check(Find<CheckBox>(d,"CoilOn").Checked,"On radio changes request value");}
   if(fn==6){Find<TextBox>(d,"WriteValue").Text="123";Find<RadioButton>(d,"UseMultipleFunction").Checked=true;}
   if(fn==15||fn==16||fn==23)
   {
    Find<NumericUpDown>(d,"WriteQuantity").Value=4;var grid=Find<DataGridView>(d,"WriteValues");
    if(fn==15)Check(Find<CheckedListBox>(d,"CoilValues").Visible,"bulk coils use visible check list");
    for(int i=0;i<4;i++)grid.Rows[i].Cells[1].Value=(fn==15?i%2:100+i).ToString();
    var path=Path.Combine(output,"values-"+fn+".txt");d.SaveValues(path);grid.Rows[0].Cells[1].Value="0";d.LoadValues(path);
    Check(Convert.ToString(grid.Rows[1].Cells[1].Value)==(fn==15?"1":"101"),"bulk save/open FC"+fn);
    if(fn==15){var coils=Find<CheckedListBox>(d,"CoilValues");coils.SetItemChecked(0,true);Check(Convert.ToString(grid[1,0].Value)=="1","coil check changes request word");}
    if(fn!=15){var type=Find<ComboBox>(d,"RegisterType");Check(type.Items.Count==28,"combined type and byte order options FC"+fn);type.SelectedIndex=20;d.EditSelectedValue("1.25");Check(Convert.ToString(grid[1,0].Value)=="16288"&&Convert.ToString(grid[1,1].Value)=="0","float typed edit FC"+fn);Check(Find<ListBox>(d,"RegisterValues").Items[0].ToString()=="000 = 1.25","formatted register list FC"+fn);if(fn==23)Find<NumericUpDown>(d,"ReadQuantity").Value=2;}
   }
   if(fn==22){Check(d.Controls.Find("ANDBit15",true).Length==1&&d.Controls.Find("ORBit0",true).Length==1,"mask has 32 individual bit controls");Find<CheckBox>(d,"ANDBit0").Checked=false;Check(Find<TextBox>(d,"AndMask").Text=="0xFFFE","bit edit updates hex mask");Find<TextBox>(d,"OrMask").Text="0x0001";Check(Find<CheckBox>(d,"ORBit0").Checked,"hex mask updates bit checkboxes");}
   await d.Transmit();Check(d.ResultText.Contains("RX:")&&!d.ResultText.Contains("Error:"),"demo response FC"+fn);
   if(fn==5||fn==6)Check(d.ResultText.Contains(fn==5?"01 0F":"01 10"),"Use Function switches wire function FC"+fn);
   if(fn==23)Check(Find<ListBox>(d,"ReadValues").Items.Count==1&&!Find<ListBox>(d,"ReadValues").Items[0].ToString().Contains("—"),"separate typed read result");
   using(var bmp=new Bitmap(d.Width,d.Height)){d.DrawToBitmap(bmp,new Rectangle(Point.Empty,bmp.Size));bmp.Save(Path.Combine(output,"function-"+fn+".png"));}
   d.Close();
  }
  using(var d=new FunctionDialog(host,6)){d.Show(host);Find<CheckBox>(d,"CloseOnSuccess").Checked=true;await d.Transmit();Check(d.IsDisposed,"single write closes after successful response when selected");}
  await host.Bus.Close();
 }
}
