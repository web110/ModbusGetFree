using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ModbusGetFree
{
    /// <summary>参考兼容文件 TC：可编辑命令列表、可选校验、原始收发记录。发送不改变轮询定义。</summary>
    public class TestCenterWindow : Form
    {
        readonly MainWindow host;
        readonly ComboBox input = new ComboBox { Name = "Command", Dock = DockStyle.Fill };
        readonly CheckBox addCheck = new CheckBox { Name = "AddCheck", Text = "Add Check", AutoSize = true };
        readonly RadioButton crc = new RadioButton { Name = "CRC", Text = "CRC", Checked = true, AutoSize = true };
        readonly RadioButton lrc = new RadioButton { Name = "LRC", Text = "LRC", AutoSize = true };
        readonly ListBox output = new ListBox
        {
            Name = "TrafficLog", Dock = DockStyle.Fill, IntegralHeight = false,
            HorizontalScrollbar = true, SelectionMode = SelectionMode.MultiExtended,
            Font = new Font("Courier New", 10)
        };
        readonly Button send = new Button { Name = "Send", Text = "Send", AutoSize = true };
        public string CommandText { get { return input.Text; } set { input.Text = value; } }
        public bool AddCheck { get { return addCheck.Checked; } set { addCheck.Checked = value; } }
        public bool UseLrc { get { return lrc.Checked; } set { lrc.Checked = value; crc.Checked = !value; } }
        public string[] Commands { get { return input.Items.Cast<string>().ToArray(); } }
        public string[] Messages { get { return output.Items.Cast<string>().ToArray(); } }

        public TestCenterWindow(MainWindow owner)
        {
            Icon = AppBrand.LoadIcon();
            host = owner;
            Text = "Test Center";
            Font = UiFonts.Create();
            Size = new Size(790, 540);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(10) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(new Label { Text = "Enter hex numbers separated by \",\" \".\" or space", AutoSize = true }, 0, 0);
            layout.Controls.Add(input, 0, 1);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            Action<string, string, Action> button = (name, text, action) =>
            {
                var item = new Button { Name = name, Text = text, AutoSize = true, MinimumSize = new Size(88, 30) };
                item.Click += (s, e) => { try { action(); } catch (Exception ex) { Append("Error: " + ex.Message); } };
                buttons.Controls.Add(item);
            };
            button("OpenList", "Open list", () => ChooseList(false));
            button("SaveList", "Save list", () => ChooseList(true));
            button("Clear", "Clear", () => output.Items.Clear());
            button("AddToList", "Add to list", AddToList);
            send.MinimumSize = new Size(88, 30);
            send.Click += async (s, e) => await Transmit();
            buttons.Controls.Add(send);
            button("Exit", "Exit", Close);
            button("Copy", "Copy", () => { string text = CopyText(); if (text.Length > 0) Clipboard.SetText(text); });
            layout.Controls.Add(buttons, 0, 2);
            var checks = new FlowLayoutPanel { Dock = DockStyle.Fill };
            checks.Controls.Add(addCheck); checks.Controls.Add(crc); checks.Controls.Add(lrc);
            layout.Controls.Add(checks, 0, 3);
            layout.Controls.Add(output, 0, 4);
            Controls.Add(layout);
            AcceptButton = send;
        }

        public static byte[] BuildFrame(string text, bool add, bool useLrc)
        {
            byte[] bytes = Parse(text);
            int extra = add ? useLrc ? 1 : 2 : 0;
            if (bytes.Length + extra > 256) throw new ArgumentException("Frame exceeds 256 bytes.");
            if (!add) return bytes;
            if (!useLrc) return Modbus.AddCrc(bytes);
            int sum = bytes.Sum(b => (int)b);
            return bytes.Concat(new[] { unchecked((byte)-sum) }).ToArray();
        }

        static byte[] Parse(string text)
        {
            if (text == null || text.Length > 4096) throw new ArgumentException("Invalid command length.");
            byte[] bytes = Modbus.FromHex(text.Replace('.', ' '));
            if (bytes.Length < 2 || bytes.Length > 256) throw new ArgumentException("Enter 2..256 bytes, starting with slave ID and function.");
            return bytes;
        }

        public void AddToList()
        {
            string command = Modbus.Hex(Parse(input.Text));
            if (!input.Items.Contains(command))
            {
                if (input.Items.Count >= 1000) throw new IOException("Command list limit is 1000.");
                input.Items.Add(command);
            }
            input.Text = command;
        }
        public void SaveList(string path) { File.WriteAllLines(path, Commands); }
        public void OpenList(string path)
        {
            if (new FileInfo(path).Length > 1024 * 1024) throw new IOException("Command list exceeds 1 MB.");
            var lines = File.ReadLines(path).Where(line => !string.IsNullOrWhiteSpace(line)).Take(1001).ToArray();
            if (lines.Length > 1000) throw new IOException("Command list limit is 1000.");
            var commands = lines.Select(line => Modbus.Hex(Parse(line))).Distinct().ToArray();
            input.Items.Clear(); input.Items.AddRange(commands);
            input.Text = commands.Length > 0 ? commands[0] : "";
        }
        void ChooseList(bool save)
        {
            using (FileDialog dialog = save ? (FileDialog)new SaveFileDialog() : new OpenFileDialog())
            {
                dialog.Filter = Language.Chinese ? "命令列表 (*.txt)|*.txt|所有文件|*.*" : "Command list (*.txt)|*.txt|All files|*.*";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (save) SaveList(dialog.FileName); else OpenList(dialog.FileName);
            }
        }
        public string CopyText()
        {
            var items = output.SelectedItems.Count > 0 ? output.SelectedItems.Cast<string>() : output.Items.Cast<string>();
            return string.Join(Environment.NewLine, items);
        }
        void Append(string text)
        {
            if (IsDisposed) return;
            output.Items.Add(Language.Text(text));
            if (output.Items.Count > 1500) output.Items.RemoveAt(0);
            output.TopIndex = output.Items.Count - 1;
        }
        public async Task Transmit()
        {
            if (host.InFlight) { Append("Error: Bus busy. Please try again."); return; }
            bool ownsBus = false;
            try
            {
                byte[] frame = BuildFrame(input.Text, AddCheck, UseLrc);
                if (!host.Bus.Connected) throw new InvalidOperationException("Connect before sending a command.");
                host.InFlight = ownsBus = true;
                send.Enabled = false;
                Append("Tx: " + Modbus.Hex(frame));
                byte[] response = await host.Bus.Exchange(frame, true);
                Append(frame[0] == 0 ? "Broadcast sent (no response)." : "Rx: " + Modbus.Hex(response));
            }
            catch (Exception ex) { Append("Error: " + ex.Message); }
            finally
            {
                if (ownsBus) host.InFlight = false;
                if (!IsDisposed) send.Enabled = true;
            }
        }
    }
}
