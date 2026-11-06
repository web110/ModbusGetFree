using System;
using System.IO;
using System.Windows.Forms;
using System.Xml.Serialization;
using ModbusGetFree;
class MbpLayoutTests
{
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);}
 [STAThread] static int Main(string[] args){try{
  var s=MbpImporter.Load(Path.Combine(args[0],"tests/fixtures/mbp-poll922/HMT_pH.mbp"));
  using(var host=new MainWindow(true))using(var d=host.NewDocument(s)){
   Check(d.Grid.Columns[1].HeaderText=="02080","HMT column must start at 02080");
   Check(d.Grid.RowCount==20,"20 address rows");
   for(int r=0;r<9;r++) Check(d.Grid[0,r].ReadOnly && d.Grid[1,r].Value==null,"leading blank row");
   Check(Convert.ToString(d.Grid[0,11].Value)=="pH","pH at row 11");
   d.Grid.CurrentCell=d.Grid[1,11];Check(d.SelectedRawIndex==2,"selected pH raw index");
   d.Grid.CurrentCell=d.Grid[0,0];d.PasteText("ignored");Check(s.Names[0]!="ignored","blank paste ignored");
   d.Grid.CurrentCell=d.Grid[0,11];d.PasteText("pH renamed");Check(s.Names[2]=="pH renamed","alias edits map correctly");
   Check(s.Address==2089&&s.Quantity==10,"protocol address unchanged");
   using(var stream=new MemoryStream()){
    var xml=new XmlSerializer(typeof(DocumentState));xml.Serialize(stream,d.Snapshot());stream.Position=0;
    using(var reopened=host.NewDocument((DocumentState)xml.Deserialize(stream)))Check(reopened.Grid.Columns[1].HeaderText=="02080","saved layout retained");
   }
   s.Address=2099;s.Quantity=4;s.Definitions.Clear();d.Rebuild();
   Check(d.Grid.ColumnCount==4 && d.Grid.Columns[3].HeaderText=="02100","cross-column headers");
   d.Grid.CurrentCell=d.Grid[1,19];Check(d.SelectedRawIndex==0,"last row maps first register");
   d.Grid.CurrentCell=d.Grid[3,0];Check(d.SelectedRawIndex==1,"next column maps next register");
   s.Rows=0;d.Rebuild();Check(d.Grid.Columns[1].HeaderText=="02099"&&d.Grid.RowCount==4,"dynamic rows start at actual address");
  }
  Console.WriteLine("PASS MBP address layout, editing, cross-column and persistence");return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
