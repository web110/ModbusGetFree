using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ModbusGetFree
{
    /// <summary>
    /// 主窗口控件、生命周期、连接状态及多窗口轮询调度。其余职责放在同名 partial 文件中。
    /// </summary>
    public partial class MainWindow : Form
    {
        public readonly Bus Bus = new Bus();
        bool inFlight;
        TaskCompletionSource<bool> exchangeFinished = new TaskCompletionSource<bool>();
        public bool InFlight
        {
            get { return inFlight; }
            set
            {
                if (inFlight == value) return;
                if (value) exchangeFinished = new TaskCompletionSource<bool>();
                inFlight = value;
                if (!value) exchangeFinished.TrySetResult(true);
            }
        }
        int manualOperations;
        readonly System.Threading.SemaphoreSlim manualGate = new System.Threading.SemaphoreSlim(1, 1);
        public bool PollingPaused { get { return manualOperations > 0; } }

        // Called on the UI thread. Reserve polling before waiting, so another timer tick
        // cannot start a new poll between the current response and the manual request.
        public async Task RunManualOperation(Func<Task> operation)
        {
            manualOperations++;
            bool acquired = false;
            try
            {
                await manualGate.WaitAsync();
                acquired = true;
                while (InFlight) await exchangeFinished.Task;
                if (closing || IsDisposed) throw new InvalidOperationException("Application is closing.");
                await operation();
            }
            finally
            {
                manualOperations--;
                if (acquired) manualGate.Release();
            }
        }
        public SerialSettings Settings = new SerialSettings();
        readonly MenuStrip menu = new MenuStrip();
        readonly ToolStrip tools = new ToolStrip();
        readonly StatusStrip status = new StatusStrip();
        readonly ToolStripStatusLabel connection = new ToolStripStatusLabel
        {
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
        readonly Timer scheduler = new Timer
        {
            Interval = 20
        };
        TrafficWindow trafficForm;
        DataWindow startupPlaceholder;
        readonly Queue<string> recent = new Queue<string>();
        int serial;
        bool scheduling;
        bool closing;
        bool dirty;
        string workspacePath;
        ToolStripMenuItem windowMenu;
        ToolStripMenuItem connectItem;
        ToolStripMenuItem disconnectItem;
        ToolStripButton connectButton;
        Dictionary<int, ToolStripMenuItem> commands = new Dictionary<int, ToolStripMenuItem>();
        Dictionary<int, ToolStripButton> toolbarCommands = new Dictionary<int, ToolStripButton>();
        DocumentState defaultDocument;
        bool autoConnect;
        List<string> recentFiles = new List<string>();
        ChartWindow chart;

        public MainWindow(bool empty)
        {
            Icon = AppBrand.LoadIcon();
            Text = "ModbusGetFree";
            IsMdiContainer = true;
            Size = new Size(1250, 850);
            MinimumSize = new Size(900, 620);
            Font = UiFonts.Create();
            menu.Font = UiFonts.Create();
            tools.Font = UiFonts.Create();
            status.Font = UiFonts.Create();
            MainMenuStrip = menu;
            BuildAppMenus();
            BuildLanguageMenu();
            BuildToolbar();
            status.Items.Add(connection);
            status.Items.Add(new ToolStripStatusLabel("Modbus RTU"));
            Controls.Add(tools);
            Controls.Add(menu);
            Controls.Add(status);
            tools.Dock = DockStyle.Top;
            menu.Dock = DockStyle.Top;
            status.Dock = DockStyle.Bottom;
            Bus.Traffic += line =>
            {
                if (IsDisposed || !IsHandleCreated)
                {
                    return;
                }

                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() => AddTraffic(line)));
                }
                else
                {
                    AddTraffic(line);
                }

            };
            scheduler.Tick += async (s, e) => await Schedule();
            scheduler.Start();
            MdiChildActivate += (s, e) => UpdateConnection();
            FormClosing += OnClosing;
            if (!empty)
            {
                Shown += async (s, e) =>
                {
                    LoadPreferences();
                    var initial = NewDocument(defaultDocument == null ? null : CloneDocument(defaultDocument));
                    if (defaultDocument == null) startupPlaceholder = initial;
                    dirty = false;
                    if (autoConnect)
                    {
                        await QuickConnect();
                    }

                };
            }

            UpdateConnection();
        }

        ToolStripMenuItem MakeMenu(string text)
        {
            var item = new ToolStripMenuItem(text);
            menu.Items.Add(item);
            return item;
        }

        ToolStripMenuItem Item(ToolStripMenuItem parent, string text, Keys shortcut, EventHandler action)
        {
            var item = new ToolStripMenuItem(text);
            if (shortcut != Keys.None)
            {
                item.ShortcutKeys = shortcut;
            }

            item.Click += action;
            parent.DropDownItems.Add(item);
            return item;
        }

        ToolStripButton Tool(string text, EventHandler handler)
        {
            var b = new ToolStripButton(text);
            b.Click += handler;
            tools.Items.Add(b);
            return b;
        }

        public DataWindow Active
        {
            get
            {
                return ActiveMdiChild as DataWindow;
            }
        }

        public void MarkDirty()
        {
            // 用户编辑或创建窗口后，不再把启动窗口当成可替换的占位窗口。
            startupPlaceholder = null;
            dirty = true;
            Text = "ModbusGetFree — " + (workspacePath == null ? "Untitled" : Path.GetFileName(workspacePath)) + " *";
        }

        public DataWindow NewDocument(DocumentState state)
        {
            if (state == null)
            {
                state = new DocumentState
                {
                    Title = "Data" + (++serial),
                    Left = (serial - 1) * 28,
                    Top = (serial - 1) * 28
                };
            }
            else
            {
                serial++;
            }

            if (defaultDocument != null && state.Title == "Data" + serial)
            {
                string title = state.Title;
                state = CloneDocument(defaultDocument);
                state.Title = title;
            }

            var d = new DataWindow(this, state);
            d.DataUpdated += SampleChart;
            d.MdiParent = this;
            d.FormClosed += (s, e) => MarkDirty();
            d.Show();
            if (MdiChildren.Length == 1)
            {
                d.WindowState = FormWindowState.Maximized;
            }

            MarkDirty();
            return d;
        }

        // 每次只选择一个到期窗口；实际请求完成后再安排下一次轮询。
        async Task Schedule()
        {
            if (scheduling || closing || InFlight || PollingPaused || !Bus.Connected)
            {
                return;
            }

            scheduling = true;
            try
            {
                var next = MdiChildren.OfType<DataWindow>().Where(d => !d.IsDisposed
                    && !d.Busy && !d.State.Disabled && d.NextPoll <= DateTime.UtcNow).OrderBy(d => d.NextPoll).FirstOrDefault();
                if (next != null)
                {
                    await next.Execute(false);
                }
            }
            finally
            {
                scheduling = false;
            }
        }

        public async Task Connect()
        {
            if (InFlight || Bus.Connected)
            {
                return;
            }

            using (var d = new ConnectionDialog(Settings))
            {
                if (d.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
            }

            try
            {
                Bus.Open(Settings);
                foreach (var child in MdiChildren.OfType<DataWindow>())
                {
                    child.NextPoll = DateTime.UtcNow;
                }

                MarkDirty();
                SavePreferences();
                UpdateConnection();
            }
            catch (Exception ex)
            {
                LocalMessageBox.Show(this, ex.Message, "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            await Task.FromResult(0);
        }

        public async Task Disconnect()
        {
            try
            {
                await Bus.Close();
                UpdateConnection();
            }
            catch (Exception ex)
            {
                LocalMessageBox.Show(this, ex.Message);
            }
        }

        void UpdateConnection()
        {
            connection.Text = Bus.Connected ? (Bus.IsDemo ? "Connected — RTU Demo"
                : Settings.Port + ": " + Settings.Baud + ", " + Settings.DataBits + ", " + Settings.Parity
                + ", " + Settings.StopBits + " — RTU") : "No Connection — press F3 to connect";
            connectItem.Enabled = !Bus.Connected;
            disconnectItem.Enabled = Bus.Connected;
            if (connectButton != null)
            {
                connectButton.Enabled = !Bus.Connected;
            }

            foreach (var d in MdiChildren.OfType<DataWindow>())
            {
                d.UpdateSummary();
            }

            RefreshChecks();
        }

        void AddTraffic(string line)
        {
            recent.Enqueue(line);
            while (recent.Count > 1500)
            {
                recent.Dequeue();
            }

            if (trafficForm != null && !trafficForm.IsDisposed)
            {
                trafficForm.Append(line);
            }
        }

        void ShowTraffic()
        {
            if (trafficForm == null || trafficForm.IsDisposed)
            {
                trafficForm = new TrafficWindow();
                foreach (string line in recent) trafficForm.Append(line);
            }
            ShowToolWindow(trafficForm);
        }

        void Scaling(DataWindow target = null)
        {
            var doc = target ?? Active;
            if (doc == null)
            {
                return;
            }

            var definition = doc.SelectedDefinition;
            if (definition.Format != 0 && definition.Format != 1 && definition.Format != 4 && definition.Format != 5)
            {
                LocalMessageBox.Show(this, "Scaling is available for 16-bit and 32-bit integer groups.");
                return;
            }

            using (var d = new EditorDialog("Value Scaling — Selected Group", 240))
            {
                var scale = new TextBox
                {
                    Text = definition.Scale.ToString(CultureInfo.InvariantCulture)
                };
                var offset = new TextBox
                {
                    Text = definition.Offset.ToString(CultureInfo.InvariantCulture)
                };
                d.Field("Scale", scale);
                d.Field("Offset", offset);
                d.Note("Displayed value = raw value × scale + offset. Writes apply the inverse transform.");
                d.Buttons(() =>
                {
                    double a = double.Parse(scale.Text, CultureInfo.InvariantCulture),
                         b = double.Parse(offset.Text, CultureInfo.InvariantCulture);
                    if (a == 0 || double.IsNaN(a) || double.IsInfinity(a) || double.IsNaN(b) || double.IsInfinity(b))
                    {
                        throw new ArgumentException("Use finite values and nonzero scale.");
                    }

                    doc.UpdateSelectedDefinitions(group =>
                    {
                        group.Scale = a;
                        group.Offset = b;
                    });
                    return true;
                });
                d.ShowDialog(this);
            }
        }

        async void OnClosing(object sender, FormClosingEventArgs e)
        {
            if (closing)
            {
                return;
            }

            e.Cancel = true;
            if (!ConfirmDiscard())
            {
                return;
            }

            closing = true;
            scheduler.Stop();
            await Bus.Close();
            BeginInvoke(new Action(() => Close()));
        }
        string PrefsPath
        {
            get
            {
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "preferences.xml");
            }
        }
    }
}
