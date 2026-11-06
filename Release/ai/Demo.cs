using System;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using ModbusGetFree;

class AiDemo
{
    [STAThread] static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        string requestedLanguage = CultureInfo.CurrentUICulture.Name;
        string evidencePath = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--lang" && i + 1 < args.Length) requestedLanguage = args[++i];
            else evidencePath = args[i];
        }
        Language.Code = Language.Codes.FirstOrDefault(code => code.Equals(requestedLanguage, StringComparison.OrdinalIgnoreCase))
            ?? Language.Codes.FirstOrDefault(code => code.Split('-')[0].Equals(requestedLanguage.Split('-')[0], StringComparison.OrdinalIgnoreCase)) ?? "en";
        Language.Initialize();
        using (var host = new MainWindow(true))
        {
            host.Shown += async (s, e) => {
                host.Settings.Demo = true; host.Settings.Delay = 0;
                await host.QuickConnect();
                var temperature = host.NewDocument(new DocumentState {
                    Title = Language.Text("Simulated temperature"), Unit = 1, Function = 3,
                    Address = 250, Quantity = 3, Format = 1, Scale = 0.1, ScanRate = 1000,
                    Names = new[] { Language.Text("Temperature A (°C)"), Language.Text("Temperature B (°C)"), Language.Text("Temperature C (°C)") } });
                var ph = host.NewDocument(new DocumentState {
                    Title = Language.Text("Simulated pH"), Unit = 2, Function = 3,
                    Address = 700, Quantity = 2, Format = 1, Scale = 0.01, ScanRate = 1000,
                    Names = new[] { Language.Text("pH channel A"), Language.Text("pH channel B") } });
                await temperature.Execute(true); await ph.Execute(true);
                host.LayoutMdi(MdiLayout.TileVertical);
                host.Text = "ModbusGetFree — " + Language.Text("Data Port") + " — " + Language.Text("Demo mode - simulated data, not measurements");
                try {
                    host.StartAiInterface(18765);
                    var server = (AiReadServer)typeof(MainWindow).GetField("aiServer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(host);
                    // Optional machine-readable evidence for the integration demonstration.
                    if (evidencePath != null) File.WriteAllText(evidencePath, server.Token);
                    System.Diagnostics.Process.Start(server.BaseUrl + "/demo?lang=" + Language.Code + "#" + server.Token);
                } catch (Exception error) { MessageBox.Show(Language.Text("Unable to start Data Port") + ": " + error.Message); }
            };
            Application.Run(host);
        }
    }
}
