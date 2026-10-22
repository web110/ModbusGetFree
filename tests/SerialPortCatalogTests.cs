using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using ModbusGetFree;

class SerialPortCatalogTests
{
    static int count;
    static void Check(bool success, string name)
    {
        if (!success) throw new Exception(name);
        count++; Console.WriteLine("PASS " + name);
    }
    [STAThread]
    static void Main()
    {
        var entries = SerialPortCatalog.Build(new[] { "COM10", "com5", "COM2", "COM300" },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                { "COM5", "USB Serial Port (COM5)" },
                { "COM2", "ELTIMA Virtual Serial Port (COM2->COM1)" },
                { "COM10", "蓝牙链接上的标准串行" }
            });
        Check(entries[0].DisplayName == "ELTIMA Virtual Serial Port (COM2->COM1)", "virtual pair name retained");
        Check(entries[1].DisplayName == "USB Serial Port (COM5)", "COM suffix not duplicated");
        Check(entries[2].DisplayName == "蓝牙链接上的标准串行 (COM10)", "Chinese device name and COM suffix");
        Check(entries.Length == 4 && !entries.Any(p => p.PortName == "COM6"), "undetected ports excluded");
        Check(entries.Last().PortName == "COM300" && entries[2].PortName == "COM10", "numeric sorting and ports above 256");
        Check(SerialPortCatalog.Resolve(entries[1].DisplayName, entries) == "COM5", "friendly label resolves to wire port");
        Check(SerialPortCatalog.Build(new string[0], new Dictionary<string,string>()).Length == 0, "no devices produces empty list");
        Check(SerialPortCatalog.Resolve("com301", entries) == "COM301", "manual COM entry");
        bool rejected = false;
        try { SerialPortCatalog.Resolve("USB Serial Port", entries); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "invalid device label rejected");
        using (var dialog = new ConnectionDialog(new SerialSettings { Port = "COM5" }))
        {
            var ports = (ComboBox)dialog.Controls.Find("SerialPort", true).Single();
            bool present = System.IO.Ports.SerialPort.GetPortNames().Contains("COM5");
            Check(present ? ((SerialPortEntry)ports.SelectedItem).PortName == "COM5" : ports.SelectedIndex == -1, "restore only a detected saved port");
            Check(ports.DropDownWidth >= 500 && ports.DropDownStyle == ComboBoxStyle.DropDownList, "wide list limited to detected choices");
        }
        Console.WriteLine("Checks: " + count + "; Exit: 0");
    }
}
