using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.Xml.Serialization;
using ModbusGetFree;
class MultilingualTests
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message + " [" + Language.Code + "]"); }
    [STAThread] static int Main()
    {
        try
        {
            Application.EnableVisualStyles();
            Check(Language.Code == "en", "English startup default");
            Check(new Preferences().Language == "en", "English preference default");
            Language.Code = "invalid";
            Check(Language.Code == "en", "Unknown preference fallback");
            using (var main = new MainWindow(true))
            using (var definition = new DefinitionDialog(new DocumentState()))
            using (var traffic = new TrafficWindow())
            using (var chart = new ChartWindow())
            using (var connection = new ConnectionDialog(new SerialSettings()))
            using (var headerGrid = new DataGridView())
            {
                headerGrid.Columns.Add("Alias", "Alias");
                headerGrid.Rows.Add("User alias 123");
                string[] functions = DefinitionDialog.Functions.ToArray();
                var combo = Descendants(definition).OfType<ComboBox>().First();
                var languages = main.MainMenuStrip.Items.OfType<ToolStripMenuItem>().Single(item => item.Text == "Language");
                Check(languages.DropDownItems.Count == 10, "10 language choices");
                Check(main.MainMenuStrip.Items.IndexOf(languages) == main.MainMenuStrip.Items.Count - 2, "Language immediately before Help");
                Check(!((ToolStripMenuItem)main.MainMenuStrip.Items[6]).DropDownItems.OfType<ToolStripMenuItem>().Any(item => item == languages), "Language removed from View");
                var toolbar = main.Controls.OfType<ToolStrip>().First(x => !(x is MenuStrip) && !(x is StatusStrip));
                var buttons = toolbar.Items.OfType<ToolStripButton>().ToArray();
                string[] tips = buttons.Select(x => x.ToolTipText).ToArray();
                var inputs = Descendants(definition).OfType<TextBox>().ToDictionary(x => x, x => x.Text);
                foreach (string target in Language.Codes)
                {
                    // Pairwise transitions expose text that cannot be restored from another language.
                    foreach (string previous in Language.Codes)
                    {
                        Language.Code = previous;
                        Language.Apply(main); Language.Apply(definition); Language.Apply(chart);
                        Language.Apply(headerGrid);
                        Language.Code = target;
                        Language.Apply(main); Language.Apply(definition); Language.Apply(chart);
                        Language.Apply(headerGrid);
                        Check(headerGrid.Columns[0].HeaderText == Language.Text("Alias"), "Header transition " + previous);
                        Check((string)headerGrid.Rows[0].Cells[0].Value == "User alias 123", "User alias preserved");
                        Check(definition.Text == Language.Text("Read/Write Definition"), "Definition transition " + previous);
                        Check(chart.Text == Language.Text("Real time Charting"), "Chart transition " + previous);
                        for (int i = 0; i < buttons.Length; i++) Check(buttons[i].ToolTipText == Language.Text(tips[i]), "Tooltip transition " + tips[i]);
                        if (target == "en")
                            for (int i = 0; i < tips.Length; i++) Check(Language.Text(tips[i]) == tips[i], "Exact English tooltip " + tips[i]);
                        string fontMenu = Language.Text("&Font...");
                        Check(fontMenu.IndexOf("(&F)") == fontMenu.LastIndexOf("(&F)"), "No duplicated menu access key");
                        for (int i = 0; i < functions.Length; i++) Check((string)combo.Items[i] == Language.Text(functions[i]), "Function choice " + functions[i]);
                        foreach (var entry in inputs) Check(entry.Key.Text == entry.Value, "Editable fields and protocol bytes preserved");
                    }
                    Language.Apply(connection); Language.Apply(traffic);
                    Check(connection.Text == Language.Text("Connection Setup — Modbus RTU"), "Connection title");
                    foreach (string key in new[] { "Write Single Coil", "Send", "Open list", "Add to list", "Real time Charting", "Select a value cell, then Display > Link to Chart > Series 1..12", "Enter 2..256 bytes, starting with slave ID and function." })
                        if (target != "en") Check(Language.Text(key) != key, "Missing translation " + key);
                    Check(Language.Text("01 03 00 00 00 0A C5 CD") == "01 03 00 00 00 0A C5 CD", "Raw bytes preserved");
                    foreach (string previous in Language.Codes)
                    {
                        Language.Code = previous;
                        string error = Language.Text("Error: Enter 2..256 bytes, starting with slave ID and function.");
                        Language.Code = target;
                        Check(Language.Text(error) == Language.Text("Error: Enter 2..256 bytes, starting with slave ID and function."), "Output error transition " + previous);
                    }
                    int index = Array.IndexOf(Language.Codes, target);
                    languages.DropDownItems[index].PerformClick();
                    Check(Language.Code == target, "Menu click");
                    using (var reader = File.OpenRead(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "preferences.xml")))
                        Check(((Preferences)new XmlSerializer(typeof(Preferences)).Deserialize(reader)).Language == target, "Preference saved");
                    Language.Code = "invalid";
                    typeof(MainWindow).GetMethod("LoadPreferences", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(main, null);
                    Check(Language.Code == target, "Preference restored");
                    Console.WriteLine(target + ": pairwise transitions, menus, tooltips, dialogs, errors, values and persistence passed.");
                }
            }
            return 0;
        }
        catch (Exception error) { Console.WriteLine(error); return 1; }
    }
    static System.Collections.Generic.IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (Control descendant in Descendants(child)) yield return descendant;
        }
    }
}
