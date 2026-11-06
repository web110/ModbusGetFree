using System;
using System.IO;
using System.Text;

namespace ModbusGetFree
{
    /// <summary>
    /// 兼容文件 MFC 二进制 .mbp 的只读导入器。已核对 7000/9000 布局；未知格式拒绝猜测。
    /// 所有长度均有上限，原文件不会被修改，导入后可在连接时正常轮询。
    /// </summary>
    public static class MbpImporter
    {
        public static DocumentState Load(string file)
        {
            using (var stream = File.OpenRead(file))
            {
                if (stream.Length > 16 * 1024 * 1024) throw new IOException("MBP file is too large.");
                using (var reader = new BinaryReader(stream, Encoding.Unicode))
                {
                    try { return Read(reader, Path.GetFileNameWithoutExtension(file)); }
                    catch (EndOfStreamException) { throw new IOException("Truncated MBP file."); }
                }
            }
        }

        static DocumentState Read(BinaryReader reader, string title)
        {
            int version = reader.ReadInt32();
            if (version != 7000 && version != 9000)
                throw new IOException("Unsupported MBP archive version: " + version + ". Supported: 7000/9000.");
            reader.ReadInt32(); // 兼容文件显示尺寸参数。
            var state = new DocumentState { Title = title, Disabled = false, AlignAddressRows = true };
            state.Function = reader.ReadInt32();
            state.Address = reader.ReadInt32();
            state.Quantity = reader.ReadInt32();
            reader.ReadInt32();
            if ((state.Function != 3 && state.Function != 4) || state.Quantity < 1 || state.Quantity > 125 ||
                state.Address < 0 || state.Address + state.Quantity > 65536)
                throw new IOException("This MBP importer supports FC03/04 register documents (1..125 registers).");
            var formats = new int[125];
            for (int i = 0; i < formats.Length; i++) formats[i] = reader.ReadInt32();
            state.Names = new string[state.Quantity];
            for (int i = 0; i < state.Quantity; i++)
            {
                for (int k = 0; k < 12; k++) reader.ReadInt32(); // 条件颜色和阈值，未导入。
                state.Names[i] = ReadString(reader);
                if (reader.ReadUInt32() != 0x55555555) throw new IOException("Invalid MBP register record.");
            }
            state.Values = new ushort[state.Quantity];
            for (int i = 0; i < state.Quantity; i++) state.Values[i] = reader.ReadUInt16();
            // 9000 在数据后加入固定 125 条缩放记录和 Quantity 条曲线引用。
            if (version == 9000)
            {
                for (int i = 0; i < 125; i++)
                {
                    for (int k = 0; k < 4; k++) reader.ReadDouble();
                    reader.ReadInt32();
                    if (reader.ReadInt32() != 0)
                        throw new IOException("This MBP uses scaling that is not yet supported; import stopped to avoid incorrect values.");
                }
                for (int i = 0; i < state.Quantity; i++) reader.ReadInt32();
            }
            state.ScanRate = reader.ReadInt32();
            state.Unit = reader.ReadInt32();
            for (int i = 0; i < 3; i++) reader.ReadInt32();
            if (reader.ReadInt32() != 44) throw new IOException("Unsupported MBP window layout.");
            for (int i = 0; i < 10; i++) reader.ReadInt32();
            for (int i = 0; i < 3; i++) reader.ReadInt32();
            state.Rows = reader.ReadInt32();
            if (state.Rows != 0 && state.Rows != 10 && state.Rows != 20 && state.Rows != 50 && state.Rows != 100)
                throw new IOException("Unsupported MBP row count.");
            if (state.Unit < 1 || state.Unit > 247 || state.ScanRate < 0 || state.ScanRate > 3600000)
                throw new IOException("Invalid MBP polling settings.");
            // Verified against 28 files saved by Modbus Poll 9.2.2.
            // MBP 16 is a continuation marker, never a standalone type.
            int[] types = { 0, 1, 2, 3, 6, 6, 9, 9, 4, 4, 6, 6, 9, 9, 4, 4, -1,
                        5, 5, 5, 5, 7, 7, 7, 7, 8, 8, 8, 8 };
            int[] orders = { 0, 0, 0, 0, 1, 0, 1, 0, 1, 0, 3, 2, 3, 2, 3, 2, 0,
                         0, 1, 2, 3, 0, 1, 2, 3, 0, 1, 2, 3 };
            for (int i = 0; i < state.Quantity; )
            {
                int code = formats[i];
                if (code < 0 || code >= types.Length || types[code] < 0)
                    throw new IOException("Unsupported MBP display format " + code + " at address " + (state.Address + i) + ".");
                int format = types[code];
                int width = ValueCodec.Width(format);
                if (i + width > state.Quantity)
                    throw new IOException("Incomplete MBP multi-register group at address " + (state.Address + i) + ".");
                for (int k = 1; k < width; k++)
                    if (formats[i + k] != 16) throw new IOException("Invalid MBP multi-register group.");
                state.Definitions.Add(new RegisterDefinition { Address = state.Address + i, Format = format, Order = orders[code] });
                i += width;
            }
            return state;
        }

        static string ReadString(BinaryReader reader)
        {
            // MFC CString: FF + FFFE 标记 Unicode，再跟变长字符数。
            if (reader.ReadByte() != 255 || reader.ReadUInt16() != 0xfffe)
                throw new IOException("Unsupported MBP string encoding.");
            uint length = reader.ReadByte();
            if (length == 255)
            {
                length = reader.ReadUInt16();
                if (length == 65535) length = reader.ReadUInt32();
            }
            if (length > 32768) throw new IOException("MBP alias is too long.");
            byte[] bytes = reader.ReadBytes((int)length * 2);
            if (bytes.Length != length * 2) throw new EndOfStreamException();
            return new UnicodeEncoding(false, false, true).GetString(bytes);
        }
    }
}
