using System;
using System.IO;
using System.Linq;
using System.Xml;
using ModbusGetFree;
class MbpSaveTests
{
    static int checks;
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; }
    [STAThread] static int Main(string[] args)
    {
        try
        {
            string fixtures = Path.Combine(args[0], "tests/fixtures/mbp-poll922");
            foreach (string source in Directory.GetFiles(fixtures, "*.mbp"))
            {
                var original = MbpFile.Load(source);
                foreach (int version in new[] { 7, 9, 12 })
                {
                    string target = Path.Combine(args[1], Path.GetFileNameWithoutExtension(source) + "-v" + version + ".mbp");
                    var state = MbpFile.Load(source);
                    state.Names[0] = "中文 \"pH\" <&> " + new string('长', 300);
                    state.Values[0] = 0xabcd;
                    MbpFile.Save(target, state, version);
                    var loaded = MbpFile.Load(target);
                    Check(loaded.MbpVersion == version, "version " + target);
                    Check(loaded.Names[0] == state.Names[0] && loaded.Values[0] == 0xabcd, "alias/value " + target);
                    Check(loaded.Address == state.Address && loaded.Quantity == state.Quantity && loaded.Unit == state.Unit && loaded.Rows == state.Rows, "configuration " + target);
                    Check(loaded.Definitions.Select(d => d.Format + ":" + d.Order).SequenceEqual(original.Definitions.Select(d => d.Format + ":" + d.Order)), "formats " + target);
                    if (version == 12)
                    {
                        var xml = new XmlDocument(); xml.Load(target);
                        Check(xml.DocumentElement.Name == "ModbusPoll", "official XML root");
                        Check(xml.SelectSingleNode("ModbusPoll/Data/Bytes/B[1]").InnerText == "205" && xml.SelectSingleNode("ModbusPoll/Data/Bytes/B[2]").InnerText == "171", "official little endian byte storage");
                        Check(xml.SelectSingleNode("ModbusPoll/Data/CellData/Cell[@idx='0']/Name").InnerText == state.Names[0], "official cell-name structure");
                        Check(xml.SelectSingleNode("ModbusPoll/LogText/AutoStart").InnerText == "0", "complete inert log settings");
                    }
                    else Check(BitConverter.ToInt32(File.ReadAllBytes(target), 0) == (version == 7 ? 7000 : 9000), "binary header");
                }
            }
            var unchanged = MbpFile.Load(Path.Combine(fixtures, "HMT_pH.mbp"));
            string preserved = Path.Combine(args[1], "preserved.mbp");
            MbpFile.Save(preserved, unchanged, unchanged.MbpVersion);
            Check(File.ReadAllBytes(preserved).SequenceEqual(File.ReadAllBytes(Path.Combine(fixtures, "HMT_pH.mbp"))), "unedited original preserved byte for byte");
            byte[] before = File.ReadAllBytes(preserved);
            unchanged.Scale = 2;
            bool rejected = false;
            try { MbpFile.Save(preserved, unchanged, 7); } catch (IOException) { rejected = true; }
            Check(rejected && before.SequenceEqual(File.ReadAllBytes(preserved)), "unsupported setting leaves original intact");
            var dynamicRows = MbpFile.Load(Path.Combine(fixtures, "signed16.mbp")); dynamicRows.Rows = 0;
            string dynamicFile = Path.Combine(args[1], "dynamic.mbp");
            foreach (int version in new[] { 7, 9, 12 })
            {
                MbpFile.Save(dynamicFile, dynamicRows, version);
                Check(MbpFile.Load(dynamicFile).Rows == 0, "fit-to-quantity row mode v" + version);
            }
            string xmlPath = Path.Combine(args[1], "xml-metadata.mbp");
            var metadata = MbpFile.Load(Path.Combine(fixtures, "signed16.mbp")); MbpFile.Save(xmlPath, metadata, 12);
            var custom = new XmlDocument(); custom.Load(xmlPath);
            custom.SelectSingleNode("ModbusPoll/ColumnWidth/CW").InnerText = "1234";
            ((XmlElement)custom.SelectSingleNode("ModbusPoll/WP")).SetAttribute("left", "234");
            custom.SelectSingleNode("ModbusPoll/HexMode").InnerText = "1";
            custom.SelectSingleNode("ModbusPoll/dpi").InnerText = "192";
            custom.Save(xmlPath); metadata = MbpFile.Load(xmlPath); MbpFile.Save(xmlPath, metadata, 12);
            custom.Load(xmlPath);
            Check(custom.SelectSingleNode("ModbusPoll/ColumnWidth/CW").InnerText == "1234" &&
                ((XmlElement)custom.SelectSingleNode("ModbusPoll/WP")).GetAttribute("left") == "234" &&
                custom.SelectSingleNode("ModbusPoll/HexMode").InnerText == "1" && custom.SelectSingleNode("ModbusPoll/dpi").InnerText == "192", "same-version XML preserves source layout and display metadata");
            before = File.ReadAllBytes(xmlPath); int previousVersion = metadata.MbpVersion;
            using (var locked = new FileStream(xmlPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                rejected = false; try { MbpFile.Save(xmlPath, metadata, 9); } catch (IOException) { rejected = true; }
                Check(rejected && metadata.MbpVersion == previousVersion && File.ReadAllBytes(xmlPath).SequenceEqual(before), "locked destination retains bytes and version");
            }
            File.WriteAllText(Path.Combine(args[1], "bad-xml.mbp"), "<!DOCTYPE x [<!ENTITY y SYSTEM 'file:///no-such-file'>]><ModbusPoll>&y;</ModbusPoll>");
            rejected = false; try { MbpFile.Load(Path.Combine(args[1], "bad-xml.mbp")); } catch (XmlException) { rejected = true; }
            Check(rejected, "XML external entities prohibited");
            var hiddenNames = MbpFile.Load(Path.Combine(fixtures, "signed16.mbp")); hiddenNames.HideNames = true;
            string hiddenFile = Path.Combine(args[1], "hidden-names.mbp");
            MbpFile.Save(hiddenFile, hiddenNames, 9);
            Check(BitConverter.ToInt32(File.ReadAllBytes(hiddenFile), File.ReadAllBytes(hiddenFile).Length - 204 + 68) == 1 && MbpFile.Load(hiddenFile).HideNames, "binary hidden-name flag retained");
            string uiFile = Path.Combine(args[1], "ui-save.mbp");
            File.Copy(Path.Combine(fixtures, "HMT_pH.mbp"), uiFile, true);
            using (var host = new MainWindow(true))
            {
                host.OpenDocument(uiFile);
                var window = host.MdiChildren.OfType<DataWindow>().Single();
                Check(window.DocumentPath == uiFile && window.State.MbpVersion == 7, "opening retains source save path/version");
                window.State.Names[2] = "pH updated"; window.SaveDocument(false);
                Check(MbpFile.Load(uiFile).Names[2] == "pH updated" && BitConverter.ToInt32(File.ReadAllBytes(uiFile), 0) == 7000, "Save command preserves original archive");
            }
            Console.WriteLine("PASS MBP save: " + checks + " checks"); return 0;
        }
        catch (Exception ex) { Console.WriteLine(ex); return 1; }
    }
}
