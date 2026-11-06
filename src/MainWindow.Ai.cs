using System;
using System.Drawing;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace ModbusGetFree
{
    public partial class MainWindow
    {
        AiReadServer aiServer;
        readonly Timer aiTimer = new Timer { Interval = 500 };
        public string CaptureAiSnapshot()
        {
            DateTime now = DateTime.UtcNow;
            return new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 }.Serialize(new {
                schemaVersion = 1, capturedAtUtc = now.ToString("o"), readOnly = true,
                connected = Bus.Connected, connectionSource = Bus.IsDemo ? "demo" : "device",
                windows = MdiChildren.OfType<DataWindow>().Select(d => d.AiSnapshot(now)).ToArray()
            });
        }
        public void StartAiInterface(int port)
        {
            if (aiServer != null) return;
            string json = CaptureAiSnapshot();
            aiServer = new AiReadServer(port);
            aiServer.Publish(json);
            aiTimer.Start();
        }
        public void StopAiInterface()
        {
            aiTimer.Stop();
            if (aiServer != null) { aiServer.Dispose(); aiServer = null; }
        }
        void BuildAiMenu()
        {
            var item = new ToolStripMenuItem("Data Port") { Name = "DataPortMenu" };
            item.Click += (s, e) => ShowAiInterface();
            var help = menu.Items.OfType<ToolStripMenuItem>().Last();
            help.DropDownItems.Insert(1, item);
            aiTimer.Tick += (s, e) => {
                try { if (aiServer != null) aiServer.Publish(CaptureAiSnapshot()); }
                catch (Exception) { StopAiInterface(); }
            };
            FormClosed += (s, e) => { StopAiInterface(); aiTimer.Dispose(); };
        }
        public void ShowAiInterface()
        {
            using (var dialog = CreateDataPortDialog()) dialog.ShowDialog(this);
        }
        Form CreateDataPortDialog()
        {
            var dialog = new Form { Text = Language.Text("Data Port"), Size = new Size(800, 520),
                MinimumSize = new Size(680, 440), StartPosition = FormStartPosition.CenterParent, Font = UiFonts.Create() };
            var info = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill, Name = "DataPortConnectionInfo" };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(8), Name = "DataPortButtons" };
            var start = new Button { Text = Language.Text("Enable Data Port"), AutoSize = true, Padding = new Padding(6) };
            var stop = new Button { Text = Language.Text("Stop Data Port"), AutoSize = true, Padding = new Padding(6) };
            var demo = new Button { Text = Language.Text("Open demo page"), AutoSize = true, Padding = new Padding(6) };
            var copy = new Button { Text = Language.Text("Copy connection info"), AutoSize = true, Padding = new Padding(6) };
            Action refresh = () => {
                string readme = Language.Code == "en" ? "ai/README.md" : "ai/README." + Language.Code + ".md";
                info.Text = aiServer == null ? Language.Text("Data Port is off.") + "\r\n\r\n"
                    + Language.Text("Only local read access. No device writes or extra polling.")
                    : Language.Text("Data Port is on. Closing the app or stopping the port revokes access.")
                    + "\r\n\r\n" + Language.Text("Address") + ": " + aiServer.BaseUrl
                    + "\r\n" + Language.Text("Access token") + ": " + aiServer.Token
                    + "\r\n\r\nGET /v1/snapshot\r\nAuthorization: Bearer <token>\r\n\r\n"
                    + string.Format(Language.Text("MCP setup: see {0}."), readme) + "\r\n"
                    + Language.Text("Current snapshots only; no history. Units require register documentation.")
                    + "\r\n" + Language.Text("Simulated data is marked demo. Share the token only with trusted tools.");
                start.Enabled = aiServer == null; stop.Enabled = demo.Enabled = copy.Enabled = aiServer != null;
            };
            start.Click += (s, e) => {
                try { StartAiInterface(18765); }
                catch (Exception ex) { LocalMessageBox.Show(dialog, Language.Text("Unable to start Data Port") + ": " + ex.Message); }
                refresh();
            };
            stop.Click += (s, e) => { StopAiInterface(); refresh(); };
            demo.Click += (s, e) => {
                if (aiServer != null) System.Diagnostics.Process.Start(aiServer.BaseUrl + "/demo?lang=" + Language.Code + "#" + aiServer.Token);
            };
            copy.Click += (s, e) => { if (aiServer != null) Clipboard.SetText(info.Text); };
            buttons.Controls.AddRange(new Control[] { start, stop, demo, copy });
            dialog.Controls.Add(info); dialog.Controls.Add(buttons); refresh();
            return dialog;
        }
    }
}
