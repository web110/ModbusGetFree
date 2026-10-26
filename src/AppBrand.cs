using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
namespace ModbusGetFree
{
    public static class AppBrand
    {
        public const string Version = "1.10";
        public static Icon LoadIcon()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "branding", "app.ico");
            return File.Exists(path) ? new Icon(path) : Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        public static Icon LoadDocumentIcon()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "branding", "document.ico");
            return File.Exists(path) ? new Icon(path) : SystemIcons.Application;
        }
        public static string RepositoryUrl
        {
            get
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "github-repository.txt");
                string value = File.Exists(path) ? File.ReadAllText(path).Trim() : "";
                Uri url;
                return Uri.TryCreate(value, UriKind.Absolute, out url) && url.Scheme == "https" && url.Host == "github.com"
                    ? value : "https://github.com/web110/ModbusGetFree";
            }
        }
    }
    public partial class MainWindow
    {
        void ShowHelpTopics()
        {
            using (var dialog = new EditorDialog("Help Topics", 520))
            {
                var text = new TextBox { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical,
                    Text = Language.Chinese ? "ModbusGetFree\r\n\r\n连接：选择串口、波特率、校验和停止位。演示模式使用模拟从站。\r\n\r\nF8：设置从站地址、功能码、起始地址、数量和扫描周期。协议地址从 0 开始。\r\nF5 快速连接，F4 断开连接。禁用轮询时可按 F6 单次读写。\r\n\r\n右键点击数值可设置格式、字节序、字体、颜色和数值换算。\r\n\r\n05/06 写入单个值，也可选择 15/16；15/16/23 支持打开和保存数值列表。22 提供 AND/OR 掩码位。\r\n\r\n测试中心发送十六进制命令，可附加校验。停止通信记录捕获不会停止轮询。\r\n\r\n数据窗口保存为 .mpdoc，工作区保存为 .mprtu，支持导入兼容的 .mbp 寄存器文件。\r\n\r\nLanguage 可切换 10 种语言，默认英语，并自动保存选择。\r\n\r\n同一串口一次只能由一个程序使用。" : Language.HelpText };
                dialog.Controls.Clear(); dialog.Controls.Add(text); dialog.ShowDialog(this);
            }
        }
        void ShowAbout()
        {
            using (var dialog = new EditorDialog("About ModbusGetFree", 250))
            {
                dialog.ClientSize = new Size(680, 350);
                var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 9 };
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
                panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
                panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
                panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
                panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
                panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
                panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
                panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
                panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                panel.Controls.Add(new Label { Text = "ModbusGetFree — Modbus RTU desktop utility", AutoSize = true }, 0, 0);
                panel.Controls.Add(new Label { Name = "SoftwareVersion", Text = "v" + AppBrand.Version, AutoSize = true }, 0, 1);
                panel.Controls.Add(new Label { Text = "Design by Jim NI", AutoSize = true }, 0, 2);
                panel.Controls.Add(new Label { Text = "Copyright © 2026 Jim NI", AutoSize = true }, 0, 3);
                var license = new Label { Text = "MIT license", AutoSize = true };
                panel.Controls.Add(license, 0, 4);
                panel.Controls.Add(new Label { Text = "GitHub project", AutoSize = true }, 0, 5);
                var address = new TextBox { Name = "RepositoryUrl", Text = AppBrand.RepositoryUrl, ReadOnly = true, Dock = DockStyle.Fill, BackColor = SystemColors.Window };
                panel.Controls.Add(address, 0, 6);
                var actions = new FlowLayoutPanel { Dock = DockStyle.Fill };
                var open = new Button { Text = "Open GitHub", AutoSize = true };
                open.Click += (s, e) => Process.Start(new ProcessStartInfo(address.Text) { UseShellExecute = true });
                var copy = new Button { Text = "Copy link", AutoSize = true };
                copy.Click += (s, e) => { address.SelectAll(); address.Focus(); address.Copy(); };
                actions.Controls.Add(open); actions.Controls.Add(copy);
                panel.Controls.Add(actions, 0, 7);
                var updates = new CheckBox { Name = "AllowUpdates", Text = "Allow updates: check GitHub at startup", AutoSize = true, Checked = allowUpdates };
                updates.CheckedChanged += (s, e) => { allowUpdates = updates.Checked; SavePreferences(); if (!allowUpdates && updateDownload != null) updateDownload.Cancel(); };
                panel.Controls.Add(updates, 0, 8);
                dialog.Controls.Clear(); dialog.Controls.Add(panel);
                dialog.ShowDialog(this);
            }
        }
    }
}
