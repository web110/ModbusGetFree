using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace ModbusGetFree
{
    /// <summary>Versioned Modbus Poll documents. Unknown metadata stays attached to its source archive.</summary>
    public static class MbpFile
    {
        internal static readonly int[] Types = { 0, 1, 2, 3, 6, 6, 9, 9, 4, 4, 6, 6, 9, 9, 4, 4, -1, 5, 5, 5, 5, 7, 7, 7, 7, 8, 8, 8, 8 };
        internal static readonly int[] Orders = { 0, 0, 0, 0, 1, 0, 1, 0, 1, 0, 3, 2, 3, 2, 3, 2, 0, 0, 1, 2, 3, 0, 1, 2, 3, 0, 1, 2, 3 };
        const string DefaultTail = "6AMAAAEAAAABAAAAAAAAAAAAAAAsAAAAAAAAAAEAAAD/////////////////////MQAAADEAAACgAwAA1wQAAAAAAAAAAAAAAgAAAAoAAADKAwAAygMAAMgAAADIAAAAyAAAAMgAAADIAAAAyAAAAMgAAADIAAAAyAAAAMgAAAAAAAAAAAAAAAAAAAABAAAAAgAAAAAAAAABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA//7/AAAAAAABAAAAAQAAAAAAAAAAAAAA";

        public static DocumentState Load(string path)
        {
            byte[] bytes;
            using (var stream = File.OpenRead(path))
            {
                if (stream.Length == 0 || stream.Length > 16 * 1024 * 1024) throw new IOException("Invalid MBP file size.");
                using (var reader = new BinaryReader(stream)) bytes = reader.ReadBytes((int)stream.Length);
            }
            int archive = bytes.Length >= 4 ? BitConverter.ToInt32(bytes, 0) : 0;
            DocumentState state;
            if (archive == 7000 || archive == 9000)
            {
                state = MbpImporter.Load(path);
                state.MbpVersion = archive / 1000;
            }
            else
            {
                state = ReadXml(bytes, Path.GetFileNameWithoutExtension(path));
                state.MbpVersion = 12;
            }
            state.MbpOriginal = bytes;
            return state;
        }

        // The target is only replaced after the complete archive has been validated and written.
        public static void Save(string path, DocumentState state, int version)
        {
            Validate(state, version);
            byte[] bytes = version == 12 ? WriteXml(state) : WriteBinary(state, version);
            WriteAtomic(path, bytes);
            state.MbpVersion = version;
            state.MbpOriginal = bytes;
        }

        public static string ConversionWarning(DocumentState state, int version)
        {
            if (state.MbpOriginal == null || state.MbpVersion == 0 || (state.MbpVersion == 12) == (version == 12)) return null;
            return Language.Chinese
                ? "二进制 MBP 与 XML 相互转换时，将保留寄存器配置、数值、名称和数据格式；原文件的窗口位置、列宽、行高、日志配置及未支持的附加样式不会完整转换。选择不同的保存路径可以保留原文件。是否继续？"
                : "Conversion between binary MBP and XML preserves register settings, values, names and formats. Original window placement, column widths, row heights, log settings and unsupported extra styling are not fully converted. Choose a different path to keep the source file. Continue?";
        }

        internal static void WriteAtomic(string path, byte[] bytes)
        {
            string target = Path.GetFullPath(path);
            string temporary = Path.Combine(Path.GetDirectoryName(target), "." + Path.GetFileName(target) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(target)) File.Replace(temporary, target, null);
                else File.Move(temporary, target);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        static void Validate(DocumentState s, int version)
        {
            if (version != 7 && version != 9 && version != 12) throw new IOException("Unsupported MBP target version.");
            if ((s.Function != 3 && s.Function != 4) || s.Quantity < 1 || s.Quantity > 125 ||
                s.Address < 0 || s.Address > 65536 - s.Quantity || s.Unit < 1 || s.Unit > 247 ||
                s.ScanRate < 0 || s.ScanRate > 3600000 || s.Enron)
                throw new IOException("MBP saving currently supports FC03/04, 1..125 registers, standard addressing and slave IDs 1..247.");
            if (s.Rows != 0 && s.Rows != 10 && s.Rows != 20 && s.Rows != 50 && s.Rows != 100)
                throw new IOException("Unsupported MBP row count.");
            if (s.Names == null || s.Values == null || s.Names.Length < s.Quantity || s.Values.Length < s.Quantity)
                throw new IOException("Incomplete MBP register data.");
            if (s.Names.Take(s.Quantity).Any(n => n != null && n.Length > 32768)) throw new IOException("MBP alias is too long.");
            // These mappings are not verified yet. Refuse rather than silently discard the user's settings.
            if (s.Scale != 1 || s.Offset != 0 || s.ColorBelow != double.MinValue || s.ColorAbove != double.MaxValue ||
                s.Foreground != System.Drawing.Color.Black.ToArgb() || s.Background != System.Drawing.Color.White.ToArgb() ||
                s.Definitions.Any(d => d.Scale != 1 || d.Offset != 0 || d.ColorBelow != double.MinValue || d.ColorAbove != double.MaxValue ||
                    d.Foreground != System.Drawing.Color.Black.ToArgb() || d.Background != System.Drawing.Color.White.ToArgb() ||
                    d.FontName != UiFonts.Name || d.FontSize != UiFonts.Size || d.FontStyle != 0))
                throw new IOException("MBP conversion of custom scaling, colors or fonts is not supported. Save as .mpdoc to preserve these settings.");
            if (version != 12 && (s.BaseOne || s.AddressInCell))
                throw new IOException("Binary MBP conversion of one-based addresses or address-in-cell is not supported. Use v12 XML or .mpdoc.");
            Formats(s);
        }

        internal static int[] Formats(DocumentState state)
        {
            var formats = new int[125];
            for (int i = 0; i < state.Quantity; )
            {
                var d = state.Definitions.FirstOrDefault(x => x.Address == state.Address + i);
                int type = d == null ? state.Format : d.Format, order = d == null ? state.Order : d.Order;
                if (type < 0 || type >= ValueCodec.Formats.Length || order < 0 || order > 3) throw new IOException("Invalid MBP format or byte order.");
                int width = ValueCodec.Width(type);
                if (i + width > state.Quantity || state.Definitions.Any(x => x.Address > state.Address + i && x.Address < state.Address + i + width))
                    throw new IOException("Incomplete or overlapping MBP multi-register group at address " + (state.Address + i) + ".");
                int code = -1;
                for (int k = 0; k < Types.Length; k++) if (Types[k] == type && (width == 1 || Orders[k] == order)) { code = k; break; }
                if (code < 0) throw new IOException("Unsupported MBP display format.");
                formats[i] = code;
                for (int k = 1; k < width; k++) formats[i + k] = 16;
                i += width;
            }
            return formats;
        }

        sealed class Archive
        {
            public byte[] Header;
            public List<byte[]> Records = new List<byte[]>();
            public byte[] Scales;
            public byte[] Charts;
            public byte[] Tail;
            public int Version, Quantity, Address;
        }

        static Archive Split(byte[] bytes)
        {
            var a = new Archive();
            using (var r = new BinaryReader(new MemoryStream(bytes), Encoding.Unicode))
            {
                a.Version = r.ReadInt32() / 1000;
                r.BaseStream.Position = 12; a.Address = r.ReadInt32(); a.Quantity = r.ReadInt32();
                r.BaseStream.Position = 0; a.Header = r.ReadBytes(524);
                for (int i = 0; i < a.Quantity; i++)
                {
                    a.Records.Add(r.ReadBytes(48));
                    MbpImporter.ReadString(r);
                    if (r.ReadUInt32() != 0x55555555) throw new IOException("Invalid MBP record.");
                }
                r.BaseStream.Position += 2 * a.Quantity;
                if (a.Version == 9) { a.Scales = r.ReadBytes(5000); a.Charts = r.ReadBytes(4 * a.Quantity); }
                a.Tail = r.ReadBytes((int)(r.BaseStream.Length - r.BaseStream.Position));
            }
            return a;
        }

        static void Put(byte[] bytes, int offset, int value) { Array.Copy(BitConverter.GetBytes(value), 0, bytes, offset, 4); }

        static byte[] WriteBinary(DocumentState state, int version)
        {
            Archive source = null;
            if (state.MbpOriginal != null && state.MbpOriginal.Length >= 4 &&
                (BitConverter.ToInt32(state.MbpOriginal, 0) == 7000 || BitConverter.ToInt32(state.MbpOriginal, 0) == 9000))
                source = Split(state.MbpOriginal);
            else if (state.MbpOriginal != null) CheckXmlConversion(Xml(state.MbpOriginal).DocumentElement);
            if (source != null && source.Version != version)
            {
                // Binary metadata cannot safely be transplanted between archive layouts.
                // Only allow conversions of the verified default records.
                CheckConversionMetadata(source);
            }
            byte[] header = source == null ? new byte[524] : (byte[])source.Header.Clone();
            Put(header, 0, version * 1000); if (source == null) Put(header, 4, 192);
            Put(header, 8, state.Function); Put(header, 12, state.Address); Put(header, 16, state.Quantity);
            int[] formats = Formats(state);
            for (int i = 0; i < state.Quantity; i++) Put(header, 24 + 4 * i, formats[i]);
            byte[] tail = source == null ? Convert.FromBase64String(DefaultTail) : (byte[])source.Tail.Clone();
            if (source == null && version == 7) Array.Resize(ref tail, tail.Length - 16);
            if (source != null && source.Version != version)
            {
                if (version == 7) Array.Resize(ref tail, tail.Length - 16);
                else tail = tail.Concat(Convert.FromBase64String(DefaultTail).Skip(Convert.FromBase64String(DefaultTail).Length - 16)).ToArray();
            }
            Put(tail, 0, state.ScanRate); Put(tail, 4, state.Unit); Put(tail, 8, state.Disabled ? 0 : 1);
            Put(tail, 12, state.DisableOnError ? 1 : 0);
            // The row/column tables have variable length. Preserve unmodeled trailing settings.
            int oldColumns = BitConverter.ToInt32(tail, 72), oldRows = BitConverter.ToInt32(tail, 76);
            int newRows = state.Rows == 0 ? state.Quantity : state.Rows;
            int leading = state.Rows == 0 ? 0 : state.Address % newRows;
            int newColumns = 2 * ((leading + state.Quantity + newRows - 1) / newRows);
            int oldEnd = 80 + 4 * (oldColumns + oldRows);
            if (oldColumns < 1 || oldColumns > 250 || oldRows < 1 || oldRows > 2000 || oldEnd > tail.Length) throw new IOException("Unsupported MBP grid layout.");
            if (source != null && (source.Quantity != state.Quantity || source.Address != state.Address)) CheckConversionMetadata(source);
            if (oldColumns != newColumns || oldRows != newRows)
            {
                using (var buffer = new MemoryStream()) using (var w = new BinaryWriter(buffer))
                {
                    w.Write(tail, 0, 80);
                    for (int i = 0; i < newColumns; i++) w.Write(970);
                    for (int i = 0; i < newRows; i++) w.Write(200);
                    w.Write(tail, oldEnd, tail.Length - oldEnd); tail = buffer.ToArray();
                }
            }
            Put(tail, 64, Array.IndexOf(new[] { 10, 20, 50, 100, 0 }, state.Rows));
            Put(tail, 68, state.HideNames ? 1 : 0);
            Put(tail, 72, newColumns); Put(tail, 76, newRows);
            using (var buffer = new MemoryStream()) using (var w = new BinaryWriter(buffer, Encoding.Unicode))
            {
                w.Write(header);
                for (int i = 0; i < state.Quantity; i++)
                {
                    int oldIndex = source == null ? -1 : state.Address + i - source.Address;
                    byte[] record = source != null && oldIndex >= 0 && oldIndex < source.Records.Count ? source.Records[oldIndex] : DefaultRecord();
                    w.Write(record); WriteString(w, state.Names[i] ?? ""); w.Write(0x55555555u);
                }
                for (int i = 0; i < state.Quantity; i++) w.Write(state.Values[i]);
                if (version == 9)
                {
                    bool same = source != null && source.Version == 9 && source.Address == state.Address && source.Quantity == state.Quantity;
                    if (same) { w.Write(source.Scales); w.Write(source.Charts); }
                    else
                    {
                        for (int i = 0; i < 125; i++) { w.Write(0d); w.Write(65535d); w.Write(0d); w.Write(100d); w.Write(1); w.Write(0); }
                        for (int i = 0; i < state.Quantity; i++) w.Write(-1);
                    }
                }
                w.Write(tail); return buffer.ToArray();
            }
        }

        static byte[] DefaultRecord()
        {
            var bytes = new byte[48]; for (int i = 0; i < 6; i++) Put(bytes, i * 4, unchecked((int)0xff000000)); return bytes;
        }

        static void CheckConversionMetadata(Archive source)
        {
            if (source.Records.Any(r => !r.SequenceEqual(DefaultRecord())) ||
                (source.Charts != null && source.Charts.Any(b => b != 255)))
                throw new IOException("This MBP contains additional color/chart settings. Keep its original version and register range, or save as .mpdoc.");
        }

        static void CheckXmlConversion(XmlElement root)
        {
            foreach (XmlNode node in root.SelectNodes("Data/CellData/Cell"))
            {
                var cell = (XmlElement)node;
                if ((cell["Colors"] != null && (cell["Colors"].Attributes.Count != 0 || cell["Colors"].HasChildNodes)) ||
                    (cell["Font"] != null && cell["Font"].GetAttribute("used") != "false") ||
                    (cell["Compare"] != null && cell["Compare"].Attributes.Cast<XmlAttribute>().Any(a => a.Value != "0")))
                    throw new IOException("This XML MBP contains additional colors or fonts. Keep v12 XML to preserve them.");
            }
        }

        static void WriteString(BinaryWriter w, string text)
        {
            w.Write((byte)255); w.Write((ushort)0xfffe);
            if (text.Length < 255) w.Write((byte)text.Length);
            else { w.Write((byte)255); w.Write((ushort)text.Length); }
            w.Write(Encoding.Unicode.GetBytes(text));
        }

        static string Number(int value) { return value.ToString(CultureInfo.InvariantCulture); }
        static void Element(XmlElement parent, string name, int value) { Set(parent, name, Number(value)); }
        static void Set(XmlElement parent, string name, string value)
        {
            var e = parent[name]; if (e == null) { e = parent.OwnerDocument.CreateElement(name); parent.AppendChild(e); } e.InnerText = value;
        }
        static XmlElement Child(XmlElement parent, string name)
        {
            var e = parent[name]; if (e == null) { e = parent.OwnerDocument.CreateElement(name); parent.AppendChild(e); } return e;
        }

        static XmlDocument Xml(byte[] bytes)
        {
            var document = new XmlDocument { XmlResolver = null };
            using (var r = XmlReader.Create(new MemoryStream(bytes), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16 * 1024 * 1024 })) document.Load(r);
            if (document.DocumentElement == null || document.DocumentElement.Name != "ModbusPoll") throw new IOException("Unsupported MBP document format.");
            return document;
        }

        static int Integer(XmlElement parent, string name)
        {
            int value; if (parent[name] == null || !int.TryParse(parent[name].InnerText, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) throw new IOException("Invalid MBP XML field: " + name);
            return value;
        }

        static DocumentState ReadXml(byte[] bytes, string title)
        {
            var root = Xml(bytes).DocumentElement;
            var version = root["Version"]; var schema = root["FileSchema"];
            if (version == null || version.GetAttribute("major") != "12" || schema == null || schema.GetAttribute("r") != "0" || schema.GetAttribute("c") != "0")
                throw new IOException("Unsupported Modbus Poll XML version/schema. Supported: v12, schema 0/0.");
            var data = root["Data"]; if (data == null) throw new IOException("Missing MBP XML data.");
            if (Integer(data, "EnronMode") != 0) throw new IOException("Enron MBP XML is not supported.");
            foreach (string name in new[] { "Scales", "ValueNames", "ChartSeries", "BinNames" })
                if (data[name] != null && data[name].HasChildNodes) throw new IOException("MBP XML " + name + " is not supported yet; open stopped to avoid losing settings.");
            var s = new DocumentState { Title = title, AlignAddressRows = true,
                Function = Integer(data, "Function"), Address = Integer(data, "Address"), Quantity = Integer(data, "Quantity"),
                Unit = Integer(root, "SlaveID"), ScanRate = Integer(root, "ScanRate"), Rows = XmlRows(root),
                Disabled = Integer(root, "Enable") == 0, DisableOnError = Integer(root, "StopOnError") != 0,
                BaseOne = Integer(root, "OneBased") != 0, HideNames = Integer(root, "HideNames") != 0, AddressInCell = Integer(root, "DisplayAddr") != 0 };
            if (s.Quantity < 1 || s.Quantity > 125) throw new IOException("Invalid MBP XML quantity.");
            s.Names = new string[s.Quantity]; s.Values = new ushort[s.Quantity];
            var bytesNode = data["Bytes"]; var formatsNode = data["Formats"];
            if (bytesNode == null || formatsNode == null || bytesNode.ChildNodes.Count != s.Quantity * 2 || formatsNode.ChildNodes.Count != s.Quantity) throw new IOException("Invalid MBP XML data length.");
            int[] codes = new int[s.Quantity];
            for (int i = 0; i < s.Quantity; i++)
            {
                int hi, lo; var f = formatsNode.ChildNodes[i] as XmlElement;
                if (f == null || f.Name != "F" || !int.TryParse(f.GetAttribute("f"), out codes[i]) || codes[i] < 0 || codes[i] >= Types.Length ||
                    !int.TryParse(bytesNode.ChildNodes[2 * i].InnerText, out hi) || !int.TryParse(bytesNode.ChildNodes[2 * i + 1].InnerText, out lo) || hi < 0 || hi > 255 || lo < 0 || lo > 255)
                    throw new IOException("Unsupported MBP XML format or byte value.");
                s.Values[i] = (ushort)(hi | (lo << 8));
                s.Names[i] = "";
            }
            if (data["CellData"] != null) foreach (XmlNode node in data["CellData"].ChildNodes)
            {
                var cell = node as XmlElement; int index;
                if (cell == null || cell.Name != "Cell" || !int.TryParse(cell.GetAttribute("idx"), out index) || index < 0 || index >= s.Quantity)
                    throw new IOException("Invalid MBP XML cell index.");
                s.Names[index] = cell["Name"] == null ? "" : cell["Name"].InnerText;
            }
            for (int i = 0; i < s.Quantity; )
            {
                int code = codes[i]; if (Types[code] < 0) throw new IOException("Invalid MBP XML continuation.");
                int width = ValueCodec.Width(Types[code]);
                if (i + width > s.Quantity) throw new IOException("Incomplete MBP XML group.");
                for (int k = 1; k < width; k++) if (codes[i + k] != 16) throw new IOException("Invalid MBP XML group.");
                s.Definitions.Add(new RegisterDefinition { Address = s.Address + i, Format = Types[code], Order = Orders[code] }); i += width;
            }
            Validate(s, 12); return s;
        }

        static byte[] WriteXml(DocumentState s)
        {
            if (s.MbpVersion != 12 && s.MbpOriginal != null && s.MbpOriginal.Length >= 4 &&
                (BitConverter.ToInt32(s.MbpOriginal, 0) == 7000 || BitConverter.ToInt32(s.MbpOriginal, 0) == 9000)) CheckConversionMetadata(Split(s.MbpOriginal));
            var document = new XmlDocument { XmlResolver = null };
            if (s.MbpVersion == 12 && s.MbpOriginal != null) document = Xml(s.MbpOriginal);
            else document.LoadXml("<ModbusPoll><FileSchema r=\"0\" c=\"0\"/><Version major=\"12\" minor=\"0\" patch=\"0\" build=\"2444\"/></ModbusPoll>");
            var root = document.DocumentElement;
            if (root["Data"] != null && (Integer(root["Data"], "Address") != s.Address || Integer(root["Data"], "Quantity") != s.Quantity)) CheckXmlConversion(root);
            if (root["dpi"] == null) Element(root, "dpi", 96);
            Element(root, "ScanRate", s.ScanRate); Element(root, "SlaveID", s.Unit);
            Element(root, "Enable", s.Disabled ? 0 : 1); Element(root, "StopOnError", s.DisableOnError ? 1 : 0);
            Element(root, "OneBased", s.BaseOne ? 1 : 0); Element(root, "RowsDialog", Array.IndexOf(new[] { 10, 20, 50, 100, 0 }, s.Rows)); Element(root, "HideNames", s.HideNames ? 1 : 0);
            if (root["HexMode"] == null) Element(root, "HexMode", 0);
            Element(root, "DisplayAddr", s.AddressInCell ? 1 : 0);
            int rows = s.Rows == 0 ? s.Quantity : s.Rows, columns = 2 * (((s.Rows == 0 ? 0 : s.Address % rows) + s.Quantity + rows - 1) / rows);
            bool sameGrid = root["ColCount"] != null && root["RowCount"] != null && Integer(root, "ColCount") == columns && Integer(root, "RowCount") == rows;
            Element(root, "ColCount", columns); Element(root, "RowCount", rows);
            if (!sameGrid)
            {
                var widths = Child(root, "ColumnWidth"); widths.RemoveAll();
                for (int i = 0; i < columns; i++) { var e = document.CreateElement("CW"); e.InnerText = "900"; widths.AppendChild(e); }
                var heights = Child(root, "RowHight"); heights.RemoveAll();
                for (int i = 0; i < rows; i++) { var e = document.CreateElement("RH"); e.InnerText = "200"; heights.AppendChild(e); }
            }
            if (root["WP"] == null)
            {
                var wp = Child(root, "WP"); foreach (var pair in new[] { "left=0", "right=700", "top=0", "bottom=450", "ShowCmd=1", "MaxPosX=-1", "MaxPosY=-1", "MinPosX=-1", "MinPosY=-1" }) { var parts = pair.Split('='); wp.SetAttribute(parts[0], parts[1]); }
            }
            foreach (string name in new[] { "ScrollPosV", "ScrollPosH", "FocusRow", "FocusCol" }) if (root[name] == null) Element(root, name, 0);
            if (root["LogText"] == null)
            {
                var log = Child(root, "LogText");
                foreach (string name in new[] { "Eachread", "LogChangedOnly", "LogErrors", "LogErrorsOnly", "LogDate", "TDelimiter", "Delimiter", "AutoStart", "Flush", "Append", "NewLogFileAtMidnight", "InsertHeader", "NameCellsInTopRow", "PollDefinition" }) Element(log, name, 0);
                foreach (string name in new[] { "Rate", "LogAddress", "LogMs" }) Element(log, name, 1);
                Set(log, "LogName", "Type log name here"); Set(log, "FileName", "");
            }
            if (root["LogExcel"] == null)
            {
                var log = Child(root, "LogExcel");
                foreach (string name in new[] { "Eachread", "Rate", "InsertHeader", "NameCellsInTopRow", "PollDefinition" }) Element(log, name, 1);
                Element(log, "StopAfter", 1000); Element(log, "LogChangedOnly", 0); Set(log, "LogName", "Type log name here");
            }
            var data = Child(root, "Data"); Element(data, "Function", s.Function); Element(data, "Address", s.Address); Element(data, "Quantity", s.Quantity); Element(data, "EnronMode", 0);
            var formats = Child(data, "Formats"); formats.RemoveAll();
            int[] codes = Formats(s);
            for (int i = 0; i < s.Quantity; i++) { var e = document.CreateElement("F"); e.SetAttribute("f", Number(codes[i])); e.SetAttribute("v", "0"); formats.AppendChild(e); }
            var bytes = Child(data, "Bytes"); bytes.RemoveAll();
            foreach (ushort word in s.Values.Take(s.Quantity)) foreach (int b in new[] { word & 255, word >> 8 }) { var e = document.CreateElement("B"); e.InnerText = Number(b); bytes.AppendChild(e); }
            var cells = Child(data, "CellData");
            for (int i = 0; i < s.Quantity; i++)
            {
                var cell = cells.SelectSingleNode("Cell[@idx='" + i + "']") as XmlElement;
                if (cell == null && !string.IsNullOrEmpty(s.Names[i]))
                {
                    cell = document.CreateElement("Cell"); cell.SetAttribute("idx", Number(i)); cells.AppendChild(cell);
                    cell.InnerXml = "<Colors/><Compare compare1=\"0\" compare2=\"0\" conditional1=\"0\" conditional2=\"0\" cnc=\"0\"/><Name/><Font used=\"false\"/>";
                }
                if (cell != null) Set(cell, "Name", s.Names[i] ?? "");
            }
            foreach (XmlNode node in cells.ChildNodes.Cast<XmlNode>().ToArray())
            {
                var cell = node as XmlElement; int index;
                if (cell != null && int.TryParse(cell.GetAttribute("idx"), out index) && index >= s.Quantity) cells.RemoveChild(cell);
            }
            foreach (string name in new[] { "Scales", "ValueNames", "ChartSeries", "BinNames" }) Child(data, name);
            using (var buffer = new MemoryStream())
            {
                using (var writer = XmlWriter.Create(buffer, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true })) document.Save(writer);
                return buffer.ToArray();
            }
        }

        static int XmlRows(XmlElement root)
        {
            int index = Integer(root, "RowsDialog");
            if (index < 0 || index > 4) throw new IOException("Unsupported MBP XML row layout.");
            return new[] { 10, 20, 50, 100, 0 }[index];
        }
    }
}
