using System;
using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;

namespace ModbusGetFree
{
    /// <summary>
    /// 串口连接参数编辑窗口。
    /// </summary>
    public class ConnectionDialog : EditorDialog
    {
        public ConnectionDialog(SerialSettings settings) : base("Connection Setup — Modbus RTU", 530)
        {
            var ports = new ComboBox { Name = "SerialPort", DropDownStyle = ComboBoxStyle.DropDownList, DropDownWidth = 550, MaxDropDownItems = 20, IntegralHeight = false, DropDownHeight = 380 };
            var portEntries = SerialPortCatalog.Load();
            ports.Items.AddRange(portEntries);
            foreach (var entry in portEntries)
                if (string.Equals(entry.PortName, settings.Port, StringComparison.OrdinalIgnoreCase)) ports.SelectedItem = entry;
            ports.DropDown += (s, e) =>
            {
                string selected = ports.SelectedItem is SerialPortEntry ? ((SerialPortEntry)ports.SelectedItem).PortName : ports.Text;
                portEntries = SerialPortCatalog.Load();
                ports.Items.Clear(); ports.Items.AddRange(portEntries);
                foreach (var entry in portEntries)
                    if (string.Equals(entry.PortName, selected, StringComparison.OrdinalIgnoreCase)) ports.SelectedItem = entry;
            };
            var baud = new ComboBox();
            baud.Items.AddRange(new object[] { "1200", "2400", "4800", "9600", "19200", "38400", "57600", "115200" });
            baud.Text = settings.Baud.ToString();
            var parity = Options(new[] { "None", "Odd", "Even", "Mark", "Space" }, (int)settings.Parity);
            var bits = Number(settings.DataBits, 7, 8);
            var stop = Options(new[] { "1", "2" }, settings.StopBits == StopBits.Two ? 1 : 0);
            var timeout = Number(settings.Timeout, 100, 60000);
            var delay = Number(settings.Delay, 2, 60000);
            var rts = new CheckBox
            {
                Text = "RTS toggle",
                Checked = settings.RtsToggle
            };
            var dtr = new CheckBox
            {
                Text = "DTR enable",
                Checked = settings.Dtr
            };
            var echo = new CheckBox
            {
                Text = "Remove local echo",
                Checked = settings.RemoveEcho
            };
            var demo = new CheckBox
            {
                Text = "Use simulated RTU slave",
                Checked = settings.Demo
            };
            Field("Connection", new Label
            {
                Text = "Serial Port / Modbus RTU",
                TextAlign = ContentAlignment.MiddleLeft
            });
            Field("Serial port", ports);
            Field("Baud rate", baud);
            Field("Data bits", bits);
            Field("Parity", parity);
            Field("Stop bits", stop);
            Field("Response timeout (ms)", timeout);
            Field("Delay between polls (ms)", delay);
            Field("RS-485 direction", rts);
            Field("Control line", dtr);
            Field("Adapter echo", echo);
            Field("Demo (no hardware)", demo);
            Note("RTU requires 8 data bits. RTS timing depends on the adapter; automatic-direction RS-485 adapters are preferred.");
            demo.CheckedChanged += (s, e) =>
            {
                ports.Enabled = baud.Enabled = parity.Enabled = bits.Enabled = stop.Enabled = rts.Enabled = dtr.Enabled = echo.Enabled = !demo.Checked;
            };
            ports.Enabled = baud.Enabled = parity.Enabled = bits.Enabled = stop.Enabled = rts.Enabled = dtr.Enabled = echo.Enabled = !demo.Checked;
            Buttons(() =>
            {
                int rate;
                if (!int.TryParse(baud.Text, out rate) || rate < 50 || rate > 4000000)
                {
                    throw new ArgumentException("Invalid baud rate.");
                }

                if (bits.Value != 8)
                {
                    throw new ArgumentException("Modbus RTU requires 8 data bits.");
                }

                if (!demo.Checked && string.IsNullOrWhiteSpace(ports.Text))
                {
                    throw new ArgumentException("Select a serial port.");
                }

                settings.Port = demo.Checked ? settings.Port : SerialPortCatalog.Resolve(ports.Text, portEntries);
                settings.Baud = rate;
                settings.DataBits = (int)bits.Value;
                settings.Parity = (Parity)parity.SelectedIndex;
                settings.StopBits = stop.SelectedIndex == 0 ? StopBits.One : StopBits.Two;
                settings.Timeout = (int)timeout.Value;
                settings.Delay = (int)delay.Value;
                settings.RtsToggle = rts.Checked;
                settings.Dtr = dtr.Checked;
                settings.RemoveEcho = echo.Checked;
                settings.Demo = demo.Checked;
                return true;
            });
        }
    }
}
