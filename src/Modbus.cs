using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ModbusGetFree
{
    /// <summary>
    /// 纯 Modbus 报文处理：CRC、组帧、响应校验、数据解码及高级功能解析，不访问界面或串口。
    /// </summary>
    public static class Modbus
    {
        // 计算 Modbus CRC16，发送时低字节在前。
        public static ushort Crc(byte[] bytes, int length)
        {
            ushort c = 0xffff;
            for (int i = 0; i < length; i++)
            {
                c ^= bytes[i];
                for (int j = 0; j < 8; j++)
                {
                    c = (ushort)((c & 1) != 0 ? (c >> 1) ^ 0xa001 : c >> 1);
                }
            }

            return c;
        }

        public static byte[] AddCrc(byte[] p)
        {
            var b = new byte[p.Length + 2];
            Array.Copy(p, b, p.Length);
            ushort c = Crc(p, p.Length);
            b[p.Length] = (byte)c;
            b[p.Length + 1] = (byte)(c >> 8);
            return b;
        }

        public static byte[] Words(params int[] words)
        {
            var b = new byte[words.Length * 2];
            for (int i = 0; i < words.Length; i++)
            {
                b[2 * i] = (byte)(words[i] >> 8);
                b[2 * i + 1] = (byte)words[i];
            }

            return b;
        }

        // 构造常用功能请求，并在发出前校验地址、数量及写入值。
        public static byte[] Request(int unit, int fn, int address, int quantity, ushort[] values)
        {
            if (unit < 0 || unit > 247)
            {
                throw new ArgumentException("Slave ID must be 0..247.");
            }

            if (fn != 1 && fn != 2 && fn != 3 && fn != 4 && fn != 5 && fn != 6 && fn != 15 && fn != 16)
            {
                throw new ArgumentException("Unsupported function.");
            }

            int max = fn <= 2 ? 2000 : fn <= 4 ? 125 : fn == 15 ? 1968 : fn == 16 ? 123 : 1;
            if (quantity < 1 || quantity > max || address < 0 || address + quantity > 65536)
            {
                throw new ArgumentException("Invalid address / quantity. Maximum quantity: " + max);
            }

            if (unit == 0 && fn <= 4)
            {
                throw new ArgumentException("Broadcast supports writes only.");
            }

            var p = new List<byte>
            {
                (byte)unit,
                (byte)fn
            };
            p.AddRange(Words(address));
            if (fn > 4)
            {
                if (values == null || values.Length != quantity)
                {
                    throw new ArgumentException("Value count does not match quantity.");
                }

                if (fn == 5 || fn == 15)
                {
                    foreach (ushort v in values)
                    {
                        if (v > 1)
                        {
                            throw new ArgumentException("Coil values must be 0 or 1.");
                        }
                    }
                }
            }

            if (fn == 5 || fn == 6)
            {
                p.AddRange(Words(fn == 5 ? (values[0] == 0 ? 0 : 0xff00) : values[0]));
            }
            else
            {
                p.AddRange(Words(quantity));
                if (fn == 16)
                {
                    p.Add((byte)(quantity * 2));
                    foreach (ushort v in values)
                    {
                        p.AddRange(Words(v));
                    }
                }
                else if (fn == 15)
                {
                    int n = (quantity + 7) / 8;
                    p.Add((byte)n);
                    for (int i = 0; i < n; i++)
                    {
                        byte b = 0;
                        for (int k = 0; k < 8 && i * 8 + k < quantity; k++)
                        {
                            b |= (byte)(values[i * 8 + k] << k);
                        }

                        p.Add(b);
                    }
                }
            }

            return AddCrc(p.ToArray());
        }

        public static byte[] EnronRequest(int unit, int fn, int address, int quantity, ushort[] values)
        {
            if (fn != 3 && fn != 4 && fn != 16)
            {
                throw new ArgumentException("Enron 32-bit ranges require function 03, 04 or 16.");
            }

            int max = fn == 16 ? 61 : 62;
            if (quantity < 1 || quantity > max || !((address >= 5000 && address
                + quantity <= 6000) || (address >= 7000 && address + quantity <= 8000)))
            {
                throw new ArgumentException("Invalid Enron 32-bit range or quantity.");
            }

            if (fn != 16)
            {
                return Request(unit, fn, address, quantity, null);
            }

            if (unit < 0 || unit > 247 || values == null || values.Length != quantity * 2)
            {
                throw new ArgumentException("Invalid Enron write values.");
            }

            var p = new List<byte>
            {
                (byte)unit,
                16
            };
            p.AddRange(Words(address, quantity));
            p.Add((byte)(quantity * 4));
            foreach (ushort v in values)
            {
                p.AddRange(Words(v));
            }

            return AddCrc(p.ToArray());
        }

        // 先校验完整性和响应身份，再处理从站异常码。
        public static void Validate(byte[] response, byte[] request)
        {
            if (response.Length < 5)
            {
                throw new IOException("Incomplete response.");
            }

            ushort crc = Crc(response, response.Length - 2);
            if (response[response.Length - 2] != (byte)crc || response[response.Length - 1] != (byte)(crc >> 8))
            {
                throw new IOException("CRC error.");
            }

            if (response[0] != request[0])
            {
                throw new IOException("Slave ID mismatch.");
            }

            if (response[1] == (request[1] | 128))
            {
                if (response.Length != 5)
                {
                    throw new IOException("Malformed exception.");
                }

                throw new IOException("Exception " + response[2] + ": " + ExceptionName(response[2]));
            }

            if (response[1] != request[1])
            {
                throw new IOException("Function mismatch.");
            }
        }

        // 将读取响应还原为原始 16 位数据；写响应则检查回显。
        public static ushort[] Decode(byte[] r, byte[] q, int count)
        {
            Validate(r, q);
            int fn = q[1];
            if (fn > 4)
            {
                if (r.Length != 8)
                {
                    throw new IOException("Invalid write response length.");
                }

                for (int i = 0; i < 6; i++)
                {
                    if (r[i] != q[i])
                    {
                        throw new IOException("Write response mismatch.");
                    }
                }

                return new ushort[0];
            }

            int bytes = fn <= 2 ? (count + 7) / 8 : count * 2;
            if (r[2] != bytes || r.Length != bytes + 5)
            {
                throw new IOException("Byte count mismatch.");
            }

            var v = new ushort[count];
            for (int i = 0; i < count; i++)
            {
                v[i] = fn <= 2 ? (ushort)((r[3 + i / 8] >> (i % 8)) & 1) : (ushort)((r[3 + i * 2] << 8) | r[4 + i * 2]);
            }

            return v;
        }

        public static string DescribeResponse(byte[] r, byte[] q)
        {
            Validate(r, q);
            int fn = q[1];
            if (fn <= 4)
            {
                int n = q[4] * 256 + q[5];
                return "Values: " + string.Join(", ", Array.ConvertAll(Decode(r, q, n), v => v.ToString()));
            }

            if (fn == 5 || fn == 6 || fn == 15 || fn == 16)
            {
                Decode(r, q, 0);
                return "Write acknowledged.";
            }

            if (fn == 8)
            {
                if (r.Length != 8 || q.Length != 8 || r[2] != q[2] || r[3] != q[3])
                {
                    throw new IOException("Diagnostic response mismatch.");
                }

                if (q[2] == 0 && q[3] == 0)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        if (r[i] != q[i])
                        {
                            throw new IOException("Diagnostic echo mismatch.");
                        }
                    }

                    return "Diagnostic return-query-data OK.";
                }

                return "Diagnostic data: " + (r[4] * 256 + r[5]);
            }

            if (fn == 11)
            {
                if (r.Length != 8)
                {
                    throw new IOException("Invalid event counter response.");
                }

                return "Status: 0x" + ((r[2] << 8) | r[3]).ToString("X4") + "; Event count: " + ((r[4] << 8) | r[5]);
            }

            if (fn == 17)
            {
                if (r.Length != r[2] + 5 || r[2] < 2)
                {
                    throw new IOException("Invalid server ID response.");
                }

                return "Server ID: " + r[3] + "; Run indicator: " + r[4] + "; Data: "
                    + System.Text.Encoding.ASCII.GetString(r, 5, r[2] - 2);
            }

            if (fn == 22)
            {
                if (r.Length != 10 || q.Length != 10)
                {
                    throw new IOException("Invalid mask write echo.");
                }

                for (int i = 0; i < 8; i++)
                {
                    if (r[i] != q[i])
                    {
                        throw new IOException("Mask write echo mismatch.");
                    }
                }

                return "Mask write acknowledged.";
            }

            if (fn == 23)
            {
                int n = q[4] * 256 + q[5];
                if (r.Length != n * 2 + 5 || r[2] != n * 2)
                {
                    throw new IOException("Invalid read/write byte count.");
                }

                var values = new List<string>();
                for (int i = 0; i < n; i++)
                {
                    values.Add((r[3 + 2 * i] * 256 + r[4 + 2 * i]).ToString());
                }

                return "Read values: " + string.Join(", ", values.ToArray());
            }

            if (fn == 43)
            {
                if (r.Length < 10 || r[2] != 14)
                {
                    throw new IOException("Invalid device identification.");
                }

                var parts = new List<string>();
                int pos = 8;
                for (int i = 0; i < r[7]; i++)
                {
                    if (pos + 2 > r.Length - 2)
                    {
                        throw new IOException("Truncated object header.");
                    }

                    int id = r[pos++], length = r[pos++];
                    if (pos + length > r.Length - 2)
                    {
                        throw new IOException("Truncated object.");
                    }

                    parts.Add("Object " + id + ": " + System.Text.Encoding.ASCII.GetString(r, pos, length));
                    pos += length;
                }

                if (pos != r.Length - 2)
                {
                    throw new IOException("Unexpected identification data.");
                }

                parts.Add("More follows: " + r[5] + "; Next object: " + r[6]);
                return string.Join("\r\n", parts.ToArray());
            }

            return "Raw response accepted.";
        }

        public static string ExceptionName(int code)
        {
            switch (code)
            {
                case 1:
                    return "Illegal function";
                case 2:
                    return "Illegal data address";
                case 3:
                    return "Illegal data value";
                case 4:
                    return "Slave device failure";
                case 5:
                    return "Acknowledge";
                case 6:
                    return "Slave device busy";
                case 8:
                    return "Memory parity error";
                case 10:
                    return "Gateway path unavailable";
                case 11:
                    return "Gateway target failed to respond";
                default:
                    return "Unknown exception";
            }
        }

        public static string Hex(byte[] b)
        {
            return BitConverter.ToString(b).Replace('-', ' ');
        }

        public static byte[] FromHex(string text)
        {
            var s = text.Split(new[] { ' ', '\t', '\r', '\n', ',', '-' }, StringSplitOptions.RemoveEmptyEntries);
            var b = new byte[s.Length];
            for (int i = 0; i < s.Length; i++)
            {
                b[i] = byte.Parse(s[i], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return b;
        }
    }
}
