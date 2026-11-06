using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using ModbusGetFree;

class DataPortLanguageTests
{
    static int checks;
    static void Check(bool value, string message) { if (!value) throw new Exception(Language.Code + ": " + message); checks++; }
    static Form Dialog(MainWindow host)
    {
        var method = typeof(MainWindow).GetMethod("CreateDataPortDialog", BindingFlags.NonPublic | BindingFlags.Instance);
        Check(method != null, "Data Port dialog factory exists");
        return (Form)method.Invoke(host, null);
    }
    [STAThread] static int Main(string[] args)
    {
        try
        {
            using (var host = new MainWindow(true))
            {
                var help = host.MainMenuStrip.Items.OfType<ToolStripMenuItem>().Last();
                Check(help.DropDownItems.OfType<ToolStripMenuItem>().Any(i => i.Text == "Data Port"), "Help contains Data Port");
                Check(!host.MainMenuStrip.Items.OfType<ToolStripMenuItem>().Any(i => i.Text.Contains("AI /")), "no top-level AI menu");
                foreach (string code in Language.Codes)
                {
                    Language.Code = code;
                    Language.Apply(host);
                    Check(help.DropDownItems.OfType<ToolStripMenuItem>().Any(i => i.Text == Language.Text("Data Port")), "translated Help entry");
                    Check(code == "en" || Language.Text("Data Port") != "Data Port", "menu translated");
                    using (var dialog = Dialog(host))
                    {
                        dialog.Show(); Application.DoEvents();
                        Check(dialog.Text == Language.Text("Data Port"), "localized title");
                        var info = (TextBox)dialog.Controls.Find("DataPortConnectionInfo", true).Single();
                        Check(info.Text.Contains(Language.Text("Data Port is off.")), "localized off state");
                        foreach (string key in new[] { "Enable Data Port", "Stop Data Port", "Open demo page", "Copy connection info" })
                            Check(dialog.Controls.Find("DataPortButtons", true).Single().Controls.OfType<Button>().Any(b => b.Text == Language.Text(key)), "button " + key);
                        var panel = dialog.Controls.Find("DataPortButtons", true).Single();
                        foreach (Button button in panel.Controls)
                            Check(panel.ClientRectangle.Contains(button.Bounds), "button fits layout: " + button.Text);
                        if (args.Length > 0)
                        {
                            using (var bitmap = new System.Drawing.Bitmap(dialog.Width, dialog.Height))
                            {
                                dialog.DrawToBitmap(bitmap, new System.Drawing.Rectangle(System.Drawing.Point.Empty, dialog.Size));
                                bitmap.Save(System.IO.Path.Combine(args[0], "DataPort-" + code + ".png"));
                            }
                        }
                    }
                    host.StartAiInterface(0);
                    using (var dialog = Dialog(host))
                    {
                        string text = dialog.Controls.Find("DataPortConnectionInfo", true).Single().Text;
                        Check(text.Contains(Language.Text("Access token")), "localized enabled info");
                        Check(text.Contains(code == "en" ? "ai/README.md" : "ai/README." + code + ".md"), "language-specific documentation");
                        Check(text.Contains("GET /v1/snapshot") && text.Contains("Authorization: Bearer"), "protocol text preserved");
                    }
                    host.StopAiInterface();
                }
            }
            Console.WriteLine("PASS " + checks + " Data Port language checks"); return 0;
        }
        catch (Exception error) { Console.WriteLine(error); return 1; }
    }
}
