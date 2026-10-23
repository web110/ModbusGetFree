using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ModbusGetFree
{
    /// <summary>通信捕获窗口。停止捕获只影响此窗口，不停止各数据窗口的轮询。</summary>
    public class TrafficWindow : Form
    {
        class Entry
        {
            public long Sequence;
            public string Time;
            public string Text;
        }

        readonly List<Entry> entries = new List<Entry>();
        readonly TextBox output = new TextBox
        {
            Name = "TrafficText", Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
            ScrollBars = ScrollBars.Both, WordWrap = false, BackColor = Color.White, Font = new Font("Courier New", 10)
        };
        readonly Button stop = new Button { Name = "Capture", Text = "Stop", AutoSize = true };
        readonly Button save = new Button { Name = "Save", Text = "Save", AutoSize = true };
        readonly Button copy = new Button { Name = "Copy", Text = "Copy", AutoSize = true };
        readonly Button log = new Button { Name = "Log", Text = "Log", AutoSize = true };
        readonly CheckBox stopOnError = new CheckBox { Name = "StopOnError", Text = "Stop on Error", AutoSize = true };
        readonly CheckBox timeStamp = new CheckBox { Name = "TimeStamp", Text = "Time stamp", AutoSize = true };
        StreamWriter logWriter;
        long sequence;
        public bool Capturing { get; private set; }
        public bool StopOnError { get { return stopOnError.Checked; } set { stopOnError.Checked = value; } }
        public bool TimeStamp { get { return timeStamp.Checked; } set { timeStamp.Checked = value; } }
        public int EntryCount { get { return entries.Count; } }
        public bool Logging { get { return logWriter != null; } }
        public string CaptureText { get { return output.Text; } }

        public TrafficWindow()
        {
            Icon = AppBrand.LoadIcon();
            Text = "Communication Traffic";
            Size = new Size(1000, 470);
            Font = UiFonts.Create();
            var bar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new Padding(8)
            };
            var exit = new Button { Name = "Exit", Text = "Exit", AutoSize = true };
            var clear = new Button { Name = "Clear", Text = "Clear", AutoSize = true };
            foreach (var button in new[] { exit, stop, clear, save, copy, log })
            {
                button.MinimumSize = new Size(80, 30);
                bar.Controls.Add(button);
            }
            bar.Controls.Add(stopOnError);
            bar.Controls.Add(timeStamp);
            Controls.Add(output);
            Controls.Add(bar);
            exit.Click += (s, e) => Close();
            stop.Click += (s, e) => SetCapturing(!Capturing);
            clear.Click += (s, e) => ClearCapture();
            timeStamp.CheckedChanged += (s, e) => RefreshText();
            save.Click += (s, e) => ChooseSave(false);
            log.Click += (s, e) => { if (Logging) StopLog(); else ChooseSave(true); };
            copy.Click += (s, e) =>
            {
                try { Clipboard.SetText(output.SelectionLength > 0 ? output.SelectedText : CaptureText); }
                catch (Exception ex) { LocalMessageBox.Show(this, ex.Message, "Copy"); }
            };
            FormClosed += (s, e) => StopLog();
            SetCapturing(true);
        }

        public void SetCapturing(bool enabled)
        {
            Capturing = enabled;
            stop.Text = enabled ? "Stop" : "Start";
            save.Enabled = copy.Enabled = !enabled && entries.Count > 0;
        }

        public void ClearCapture()
        {
            entries.Clear();
            sequence = 0;
            output.Clear();
            SetCapturing(Capturing);
        }

        public void Append(string line)
        {
            if (!Capturing || IsDisposed) return;
            var match = Regex.Match(line, @"^(\d{2}:\d{2}:\d{2}\.\d{3})  (.*)$");
            var entry = new Entry
            {
                Sequence = sequence++,
                Time = match.Success ? match.Groups[1].Value : DateTime.Now.ToString("HH:mm:ss.fff"),
                Text = match.Success ? match.Groups[2].Value : line
            };
            entries.Add(entry);
            string formatted = Format(entry);
            if (entries.Count > 1500)
            {
                entries.RemoveAt(0);
                RefreshText();
            }
            else output.AppendText(formatted + Environment.NewLine);
            if (logWriter != null)
            {
                try { logWriter.WriteLine(formatted); }
                catch (Exception ex)
                {
                    StopLog();
                    SetCapturing(false);
                    LocalMessageBox.Show(this, ex.Message, "Traffic log stopped");
                }
            }
            if (StopOnError && entry.Text.StartsWith("ERR:", StringComparison.Ordinal)) SetCapturing(false);
        }

        string Format(Entry entry)
        {
            string kind = entry.Text.StartsWith("TX:") ? "Tx" : entry.Text.StartsWith("RX:") ? "Rx"
                : entry.Text.StartsWith("ERR:") ? "Err" : "Info";
            string text = kind == "Info" ? entry.Text : entry.Text.Substring(entry.Text.IndexOf(':') + 1).TrimStart();
            return (TimeStamp ? entry.Time + "  " : "") + kind + ":" + entry.Sequence.ToString("D6") + "-" + text;
        }

        void RefreshText()
        {
            output.Text = string.Join(Environment.NewLine, entries.Select(Format)) +
                (entries.Count > 0 ? Environment.NewLine : "");
            output.SelectionStart = output.TextLength;
            output.ScrollToCaret();
        }

        public void SaveTo(string path) { File.WriteAllText(path, CaptureText, Encoding.UTF8); }
        public void StartLog(string path)
        {
            var writer = new StreamWriter(path, true, Encoding.UTF8) { AutoFlush = true };
            StopLog();
            logWriter = writer;
            log.Text = "Log off";
        }
        public void StopLog()
        {
            var writer = logWriter;
            logWriter = null;
            if (writer != null) writer.Dispose();
            log.Text = "Log";
        }
        void ChooseSave(bool continuous)
        {
            using (var dialog = new SaveFileDialog { Filter = "Traffic text|*.txt", FileName = "communication.txt" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try { if (continuous) StartLog(dialog.FileName); else SaveTo(dialog.FileName); }
                catch (Exception ex) { LocalMessageBox.Show(this, ex.Message, "Traffic file"); }
            }
        }
    }
}

