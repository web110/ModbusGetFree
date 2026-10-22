using System;
using System.Globalization;

namespace ModbusGetFree
{
    /// <summary>
    /// 寄存器与显示值之间的转换，集中处理整数、浮点数、字节/字序和数值缩放。
    /// </summary>
    public static class ValueCodec
    {
        public static readonly string[] Formats =
        {
            "Signed 16-bit",
            "Unsigned 16-bit",
            "Hex",
            "Binary",
            "Signed 32-bit",
            "Unsigned 32-bit",
            "Float 32-bit",
            "Signed 64-bit",
            "Unsigned 64-bit",
            "Double 64-bit"
        };

        public static int Width(int format)
        {
            return format < 4 ? 1 : format < 7 ? 2 : 4;
        }

        static byte[] Bytes(ushort[] values, int start, int width, int order)
        {
            var b = new byte[width * 2];
            for (int i = 0; i < width; i++)
            {
                int word = (order == 1 || order == 3) ? width - 1 - i : i;
                ushort v = values[start + word];
                b[2 * i] = (byte)(v >> 8);
                b[2 * i + 1] = (byte)v;
                if (order == 2 || order == 3)
                {
                    byte t = b[2 * i];
                    b[2 * i] = b[2 * i + 1];
                    b[2 * i + 1] = t;
                }
            }

            return b;
        }

        public static string Format(ushort[] values, int start, int format, int order, double scale, double offset)
        {
            int w = Width(format);
            if (start + w > values.Length)
            {
                return "—";
            }

            byte[] b = Bytes(values, start, w, order);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(b);
            }

            if (format == 2)
            {
                int hi = values[start] >> 8, lo = values[start] & 255;
                return "(" + (hi >= 32 && hi < 127 ? (char)hi : '?')
                    + (lo >= 32 && lo < 127 ? (char)lo : '?') + ") 0x" + values[start].ToString("X4");
            }

            if (format == 3)
            {
                return Convert.ToString(values[start], 2).PadLeft(16, '0');
            }

            if (scale == 1 && offset == 0)
            {
                switch (format)
                {
                    case 0:
                        return unchecked((short)values[start]).ToString();
                    case 1:
                        return values[start].ToString();
                    case 4:
                        return BitConverter.ToInt32(b, 0).ToString();
                    case 5:
                        return BitConverter.ToUInt32(b, 0).ToString();
                    case 6:
                        return BitConverter.ToSingle(b, 0).ToString("G9", CultureInfo.InvariantCulture);
                    case 7:
                        return BitConverter.ToInt64(b, 0).ToString();
                    case 8:
                        return BitConverter.ToUInt64(b, 0).ToString();
                    case 9:
                        return BitConverter.ToDouble(b, 0).ToString("G17", CultureInfo.InvariantCulture);
                }
            }

            double x = format == 0 ? unchecked((short)values[start]) : format == 1
                ? values[start] : format == 4 ? BitConverter.ToInt32(b, 0) : format == 5 ? BitConverter.ToUInt32(b,
                 0) : format == 6 ? BitConverter.ToSingle(b, 0) : format == 7 ? BitConverter.ToInt64(b,
                 0) : format == 8 ? BitConverter.ToUInt64(b, 0) : BitConverter.ToDouble(b, 0);
            return (x * scale + offset).ToString("G12", CultureInfo.InvariantCulture);
        }

        /// <summary>界面使用界面的 Float32 六位有效数字；精确转换仍使用 Format。</summary>
        public static string Display(ushort[] values, int start, int format, int order, double scale, double offset)
        {
            string full = Format(values, start, format, order, scale, offset);
            double value;
            if (format == 6 && double.TryParse(full, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return value.ToString("G6", CultureInfo.InvariantCulture);
            return full;
        }

        public static ushort ParseWord(string s)
        {
            s = s.Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return ushort.Parse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            int n = int.Parse(s, CultureInfo.InvariantCulture);
            if (n < short.MinValue || n > ushort.MaxValue)
            {
                throw new FormatException("16-bit value out of range.");
            }

            return unchecked((ushort)n);
        }

        // 写入时反向应用缩放，再按所选字序拆回寄存器。
        public static ushort[] Encode(string text, int format, int order, double scale, double offset)
        {
            if (scale == 0)
            {
                throw new ArgumentException("Scale cannot be zero.");
            }

            if (format < 4)
            {
                if (format == 2)
                {
                    text = text.Trim();
                    int marker = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                        ? 0 : text.LastIndexOf("0x", StringComparison.OrdinalIgnoreCase);
                    text = marker >= 0 ? text.Substring(marker).Trim().Split(' ')[0] : text.Trim();
                }

                if (format == 3)
                {
                    return new[]
                    {
                        Convert.ToUInt16(text, 2)
                    };
                }

                if (scale == 1 && offset == 0)
                {
                    return new[]
                    {
                        ParseWord(text)
                    };
                }

                double n = (double.Parse(text, CultureInfo.InvariantCulture) - offset) / scale;
                if (n != Math.Truncate(n) || n < (format == 0 ? -32768 : 0) || n > (format == 0 ? 32767 : 65535))
                {
                    throw new FormatException("Scaled value does not fit a 16-bit register.");
                }

                return new[]
                {
                    unchecked((ushort)(int)n)
                };
            }

            byte[] b;
            if (scale == 1 && offset == 0)
            {
                switch (format)
                {
                    case 4:
                        b = BitConverter.GetBytes(int.Parse(text, CultureInfo.InvariantCulture));
                        break;
                    case 5:
                        b = BitConverter.GetBytes(uint.Parse(text, CultureInfo.InvariantCulture));
                        break;
                    case 6:
                        b = BitConverter.GetBytes(float.Parse(text, CultureInfo.InvariantCulture));
                        break;
                    case 7:
                        b = BitConverter.GetBytes(long.Parse(text, CultureInfo.InvariantCulture));
                        break;
                    case 8:
                        b = BitConverter.GetBytes(ulong.Parse(text, CultureInfo.InvariantCulture));
                        break;
                    default:
                        b = BitConverter.GetBytes(double.Parse(text, CultureInfo.InvariantCulture));
                        break;
                }
            }
            else
            {
                double x = (double.Parse(text, CultureInfo.InvariantCulture) - offset) / scale;
                switch (format)
                {
                    case 4:
                        if (x != Math.Truncate(x))
                        {
                            throw new FormatException("Integer required.");
                        }

                        b = BitConverter.GetBytes(checked((int)x));
                        break;
                    case 5:
                        if (x != Math.Truncate(x))
                        {
                            throw new FormatException("Integer required.");
                        }

                        b = BitConverter.GetBytes(checked((uint)x));
                        break;
                    case 6:
                        b = BitConverter.GetBytes((float)x);
                        break;
                    case 7:
                        if (x != Math.Truncate(x))
                        {
                            throw new FormatException("Integer required.");
                        }

                        b = BitConverter.GetBytes(checked((long)x));
                        break;
                    case 8:
                        if (x != Math.Truncate(x))
                        {
                            throw new FormatException("Integer required.");
                        }

                        b = BitConverter.GetBytes(checked((ulong)x));
                        break;
                    default:
                        b = BitConverter.GetBytes(x);
                        break;
                }
            }

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(b);
            }

            int w = b.Length / 2;
            var result = new ushort[w];
            for (int i = 0; i < w; i++)
            {
                int word = order == 1 || order == 3 ? w - 1 - i : i;
                byte hi = b[2 * i], lo = b[2 * i + 1];
                if (order == 2 || order == 3)
                {
                    byte t = hi;
                    hi = lo;
                    lo = t;
                }

                result[word] = (ushort)(hi * 256 + lo);
            }

            return result;
        }
    }
}
