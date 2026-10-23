using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Security;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace ModbusGetFree
{
    public sealed class SerialPortEntry
    {
        public string PortName { get; private set; }
        public string DisplayName { get; private set; }
        public SerialPortEntry(string portName, string deviceName)
        {
            PortName = portName;
            DisplayName = string.IsNullOrWhiteSpace(deviceName) ? portName
                : Regex.IsMatch(deviceName, @"\b" + Regex.Escape(portName) + @"\b", RegexOptions.IgnoreCase)
                    ? deviceName : deviceName + " (" + portName + ")";
        }
        public override string ToString() { return DisplayName; }
    }

    public static class SerialPortCatalog
    {
        public static SerialPortEntry[] Build(IEnumerable<string> detected, IDictionary<string, string> names)
        {
            return detected
                .Where(p => Regex.IsMatch(p, @"^COM[1-9][0-9]*$", RegexOptions.IgnoreCase))
                .Select(p => p.ToUpperInvariant()).Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => long.Parse(p.Substring(3)))
                .Select(p => new SerialPortEntry(p, names.ContainsKey(p) ? names[p] : null)).ToArray();
        }

        public static SerialPortEntry[] Load()
        {
            var detected = SerialPort.GetPortNames();
            var active = new HashSet<string>(detected, StringComparer.OrdinalIgnoreCase);
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var root = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum"))
                    ReadDevices(root, 0, active, names);
            }
            catch (UnauthorizedAccessException) { }
            catch (SecurityException) { }
            catch (IOException) { }
            return Build(detected, names);
        }

        static void ReadDevices(RegistryKey key, int depth, HashSet<string> active, Dictionary<string, string> names)
        {
            if (key == null) return;
            try
            {
                if (depth == 3)
                {
                    using (var parameters = key.OpenSubKey("Device Parameters"))
                    {
                        string port = parameters == null ? null : parameters.GetValue("PortName") as string;
                        if (port == null || !active.Contains(port)) return;
                        string name = key.GetValue("FriendlyName") as string;
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            name = key.GetValue("DeviceDesc") as string;
                            if (name != null) name = name.Substring(name.LastIndexOf(';') + 1);
                        }
                        if (!string.IsNullOrWhiteSpace(name)) names[port] = name;
                    }
                    return;
                }
                foreach (string child in key.GetSubKeyNames())
                {
                    try { using (var next = key.OpenSubKey(child)) ReadDevices(next, depth + 1, active, names); }
                    catch (UnauthorizedAccessException) { }
                    catch (SecurityException) { }
                    catch (IOException) { }
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (SecurityException) { }
            catch (IOException) { }
        }

        public static string Resolve(string text, IEnumerable<SerialPortEntry> entries)
        {
            var entry = entries.FirstOrDefault(p => string.Equals(p.DisplayName, text, StringComparison.OrdinalIgnoreCase)
                || string.Equals(p.PortName, text, StringComparison.OrdinalIgnoreCase));
            if (entry != null) return entry.PortName;
            text = text.Trim().ToUpperInvariant();
            if (!Regex.IsMatch(text, @"^COM[1-9][0-9]*$")) throw new ArgumentException("Select a valid COM port.");
            return text;
        }
    }
}
