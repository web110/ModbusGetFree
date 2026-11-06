using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ModbusGetFree;
class StartupFileTests
{
    [STAThread] static int Main(string[] args)
    {
        Exception failure = null;
        string file = args.Length > 1 ? args[1] : Path.Combine(args[0], "startup file.mpdoc");
        var fixture = new DocumentState { Title = "Startup regression", Quantity = 2, Values = new ushort[] { 123, 456 }, Names = new string[] { "A", "B" } };
        if (args.Length <= 1) using (var stream = File.Create(file))
            new System.Xml.Serialization.XmlSerializer(typeof(DocumentState)).Serialize(stream, fixture);

        {
            EventHandler inspect = null;
            inspect = (s, e) =>
            {
                Application.Idle -= inspect;
                var main = Application.OpenForms.OfType<MainWindow>().First();
                try
                {
                    var expected = args.Length > 1 ? MbpImporter.Load(file) : fixture;
                    var docs = main.MdiChildren.OfType<DataWindow>().ToArray();
                    if (docs.Length != 1 || docs[0].State.Title != expected.Title ||
                        docs[0].State.Quantity != expected.Quantity ||
                        docs[0].State.Definitions.Count != expected.Definitions.Count ||
                        !docs[0].State.Values.SequenceEqual(expected.Values) ||
                        !docs[0].State.Names.SequenceEqual(expected.Names))
                        throw new Exception("Startup must load the supplied MBP instead of the default document.");
                    using (var menu = new MainWindow(true))
                    {
                        menu.Show();
                        menu.OpenDocument(file);
                        var opened = menu.MdiChildren.OfType<DataWindow>().Single();
                        var actual = docs[0];
                        if (actual.Grid.RowCount != opened.Grid.RowCount || actual.Grid.ColumnCount != opened.Grid.ColumnCount)
                            throw new Exception("Startup and File Open grid dimensions differ.");
                        for (int row = 0; row < opened.Grid.RowCount; row++)
                            for (int col = 0; col < opened.Grid.ColumnCount; col++)
                                if (Convert.ToString(actual.Grid[col, row].Value) != Convert.ToString(opened.Grid[col, row].Value))
                                    throw new Exception("Startup and File Open cells differ.");
                    }
                }
                catch (Exception ex) { failure = ex; }
                finally { main.Dispose(); }
            };
            Application.Idle += inspect;
            Program.Main(new[] { file });
        }
        Console.WriteLine(failure == null ? "PASS startup MBP matches File Open" : failure.Message);
        return failure == null ? 0 : 1;
    }
}
