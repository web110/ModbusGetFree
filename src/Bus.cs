using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace ModbusGetFree
{
    /// <summary>
    /// 串口连接与共享 RTU 通道。通过信号量保证同一时刻只有一个请求；同时提供无硬件演示从站。
    /// </summary>
    public class Bus : IDisposable
    {
        SerialPort port;
        // 多个数据窗口共享同一串口，所有收发必须持有此信号量。
        readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        readonly Dictionary<string, ushort[]> memory = new Dictionary<string, ushort[]>();
        SerialSettings settings;
        DateTime lastEnd = DateTime.MinValue;
        bool connected;
        public event Action<string> Traffic;
        public bool Connected
        {
            get
            {
                return connected;
            }
        }

        public bool IsDemo
        {
            get
            {
                return settings != null && settings.Demo;
            }
        }

        public void Open(SerialSettings s)
        {
            if (connected)
            {
                throw new InvalidOperationException("Already connected.");
            }

            settings = s;
            if (!s.Demo)
            {
                port = new SerialPort(s.Port, s.Baud, s.Parity, s.DataBits, s.StopBits);
                port.ReadTimeout = s.Timeout;
                port.WriteTimeout = s.Timeout;
                port.DtrEnable = s.Dtr;
                try
                {
                    port.Open();
                }
                catch
                {
                    port.Dispose();
                    port = null;
                    throw;
                }
            }

            connected = true;
        }

        public async Task Close()
        {
            await gate.WaitAsync();
            try
            {
                connected = false;
                if (port != null)
                {
                    port.Dispose();
                    port = null;
                }
            }
            finally
            {
                gate.Release();
            }
        }

        void Log(string text)
        {
            var h = Traffic;
            if (h != null)
            {
                h(DateTime.Now.ToString("HH:mm:ss.fff") + "  " + text);
            }
        }

        ushort[] Memory(int unit, bool coils)
        {
            string key = unit + ":" + coils;
            ushort[] m;
            if (!memory.TryGetValue(key, out m))
            {
                m = new ushort[65536];
                for (int i = 0; i < m.Length; i++)
                {
                    m[i] = (ushort)(coils ? i % 2 : i % 1000);
                }

                memory[key] = m;
            }

            return m;
        }

        byte[] Simulate(byte[] q, bool enron)
        {
            int fn = q[1], unit = q[0];
            if (enron)
            {
                int ea = q[2] * 256 + q[3], en = q[4] * 256 + q[5];
                if (fn == 3 || fn == 4)
                {
                    var m = Memory(unit + 1000, false);
                    var p = new List<byte>
                    {
                        q[0],
                        q[1],
                        (byte)(en * 4)
                    };
                    for (int i = 0; i < en * 2; i++)
                    {
                        p.AddRange(Modbus.Words(m[ea * 2 + i]));
                    }

                    return Modbus.AddCrc(p.ToArray());
                }

                if (fn == 16)
                {
                    for (int u = unit == 0 ? 1 : unit; u <= (unit == 0 ? 247 : unit); u++)
                    {
                        var m = Memory(u + 1000, false);
                        for (int i = 0; i < en * 2; i++)
                        {
                            m[ea * 2 + i] = (ushort)(q[7 + i * 2] * 256 + q[8 + i * 2]);
                        }
                    }

                    var echo = new byte[6];
                    Array.Copy(q, echo, 6);
                    return Modbus.AddCrc(echo);
                }
            }

            if (fn == 8)
            {
                if (q.Length != 8 || q[2] != 0 || q[3] != 0)
                {
                    return Modbus.AddCrc(new byte[] { q[0], 136, 3 });
                }

                return (byte[])q.Clone();
            }

            if (fn == 11)
            {
                return Modbus.AddCrc(new byte[] { q[0], 11, 0, 0, 0, 1 });
            }

            if (fn == 17)
            {
                byte[] id = System.Text.Encoding.ASCII.GetBytes("RTU Demo");
                var p = new List<byte>
                {
                    q[0],
                    17,
                    (byte)(id.Length + 2),
                    1,
                    255
                };
                p.AddRange(id);
                return Modbus.AddCrc(p.ToArray());
            }

            if (fn == 22)
            {
                if (q.Length != 10)
                {
                    throw new IOException("Mask write requires address, AND mask and OR mask.");
                }

                int a = q[2] * 256 + q[3], and = q[4] * 256 + q[5], or = q[6] * 256 + q[7];
                for (int u = unit == 0 ? 1 : unit; u <= (unit == 0 ? 247 : unit); u++)
                {
                    var m = Memory(u, false);
                    m[a] = (ushort)((m[a] & and) | (or & (~and)));
                }

                return (byte[])q.Clone();
            }

            if (fn == 23)
            {
                if (q.Length < 15)
                {
                    throw new IOException("Incomplete read/write request.");
                }

                int a = q[2] * 256 + q[3], n = q[4] * 256 + q[5], wa = q[6] * 256 + q[7], wn = q[8] * 256 + q[9];
                if (n < 1 || n > 125 || wn < 1 || wn > 121 || a + n > 65536
                    || wa + wn > 65536 || q[10] != wn * 2 || q.Length != 13 + wn * 2)
                {
                    throw new IOException("Invalid read/write range or byte count.");
                }

                var m = Memory(unit, false);
                for (int i = 0; i < wn; i++)
                {
                    m[wa + i] = (ushort)(q[11 + 2 * i] * 256 + q[12 + 2 * i]);
                }

                var p = new List<byte>
                {
                    q[0],
                    23,
                    (byte)(n * 2)
                };
                for (int i = 0; i < n; i++)
                {
                    p.AddRange(Modbus.Words(m[a + i]));
                }

                return Modbus.AddCrc(p.ToArray());
            }

            if (fn == 43)
            {
                if (q.Length != 7 || q[2] != 14)
                {
                    throw new IOException("Expected MEI 0E request.");
                }

                string[] objects =
                {
                    "Independent RTU",
                    "Poll RTU",
                    "1.0"
                };
                int start = q[4];
                if (start > 2)
                {
                    return Modbus.AddCrc(new byte[] { q[0], 171, 2 });
                }

                var p = new List<byte>
                {
                    q[0],
                    43,
                    14,
                    q[3],
                    1,
                    0,
                    0,
                    (byte)(3 - start)
                };
                for (int i = start; i < 3; i++)
                {
                    byte[] text = System.Text.Encoding.ASCII.GetBytes(objects[i]);
                    p.Add((byte)i);
                    p.Add((byte)text.Length);
                    p.AddRange(text);
                }

                return Modbus.AddCrc(p.ToArray());
            }

            {
                if (q.Length < 8)
                {
                    throw new IOException("Demo expects a standard read/write request.");
                }

                int a = q[2] * 256 + q[3], n = q[4] * 256 + q[5];
                if (fn == 1 || fn == 2 || fn == 3 || fn == 4)
                {
                    if (a + n > 65536)
                    {
                        throw new IOException("Invalid demo range.");
                    }

                    var p = new List<byte>
                    {
                        q[0],
                        q[1],
                        (byte)(fn <= 2 ? (n + 7) / 8 : n * 2)
                    };
                    var m = Memory(unit, fn <= 2);
                    if (fn <= 2)
                    {
                        for (int i = 0; i < (n + 7) / 8; i++)
                        {
                            byte b = 0;
                            for (int k = 0; k < 8 && i * 8 + k < n; k++)
                            {
                                b |= (byte)(m[a + i * 8 + k] << k);
                            }

                            p.Add(b);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < n; i++)
                        {
                            p.AddRange(Modbus.Words(m[a + i]));
                        }
                    }

                    return Modbus.AddCrc(p.ToArray());
                }

                if (fn != 5 && fn != 6 && fn != 15 && fn != 16)
                {
                    return Modbus.AddCrc(new byte[] { q[0], (byte)(fn | 128), 1 });
                }

                if (fn == 5 || fn == 6)
                {
                    n = 1;
                }

                if (a + n > 65536)
                {
                    throw new IOException("Invalid demo range.");
                }

                var units = new List<int>();
                if (unit == 0)
                {
                    for (int u = 1; u <= 247; u++)
                    {
                        units.Add(u);
                    }
                }
                else
                {
                    units.Add(unit);
                }

                foreach (int u in units)
                {
                    var m = Memory(u, fn == 5 || fn == 15);
                    for (int i = 0; i < n; i++)
                    {
                        m[a + i] = fn == 5 ? (ushort)(q[4] == 0 ? 0 : 1) : fn == 6
                            ? (ushort)(q[4] * 256 + q[5]) : fn == 15 ? (ushort)((q[7 + i / 8] >> (i % 8)) & 1)
                            : (ushort)(q[7 + 2 * i] * 256 + q[8 + 2 * i]);
                    }
                }

                var echo = new byte[6];
                Array.Copy(q, echo, 6);
                return Modbus.AddCrc(echo);
            }
        }

        public async Task<byte[]> Exchange(byte[] request, bool raw, bool enron = false)
        {
            await gate.WaitAsync();
            try
            {
                if (!connected)
                {
                    throw new IOException("Not connected.");
                }

                return await Task.Run(() =>
                {
                    double charMs = (1 + settings.DataBits + (settings.Parity == Parity.None
                        ? 0 : 1) + (settings.StopBits == StopBits.Two ? 2 : 1)) * 1000.0 / settings.Baud;
                    int gap = Math.Max(settings.Delay, settings.Baud > 19200 ? 2 : (int)Math.Ceiling(3.5 * charMs));
                    int wait = gap - (int)(DateTime.UtcNow - lastEnd).TotalMilliseconds;
                    if (wait > 0)
                    {
                        Thread.Sleep(wait);
                    }

                    Log("TX: " + Modbus.Hex(request));
                    if (settings.Demo)
                    {
                        Thread.Sleep(15);
                        byte[] demo = Simulate(request, enron);
                        if (request[0] == 0)
                        {
                            Log("Broadcast sent (no response).");
                            return new byte[0];
                        }

                        Log("RX: " + Modbus.Hex(demo));
                        return demo;
                    }

                    port.DiscardInBuffer();
                    try
                    {
                        if (settings.RtsToggle)
                        {
                            port.RtsEnable = true;
                            Thread.Sleep(2);
                        }

                        port.Write(request, 0, request.Length);
                        if (settings.RtsToggle)
                        {
                            var txWatch = System.Diagnostics.Stopwatch.StartNew();
                            while (port.BytesToWrite > 0)
                            {
                                if (txWatch.ElapsedMilliseconds > settings.Timeout)
                                {
                                    throw new TimeoutException("Transmit timeout.");
                                }

                                Thread.Sleep(1);
                            }

                            Thread.Sleep((int)Math.Ceiling(2 * charMs));
                            port.RtsEnable = false;
                        }

                        if (request[0] == 0)
                        {
                            Thread.Sleep((int)Math.Ceiling(request.Length * charMs));
                            Log("Broadcast sent (no response).");
                            return new byte[0];
                        }

                        var data = new List<byte>();
                        var watch = System.Diagnostics.Stopwatch.StartNew();
                        int expected = 0;
                        bool echoDone = !settings.RemoveEcho;
                        while (true)
                        {
                            int remain = settings.Timeout - (int)watch.ElapsedMilliseconds;
                            if (remain <= 0)
                            {
                                throw new TimeoutException("Response timeout.");
                            }

                            port.ReadTimeout = raw && data.Count > 0 ? Math.Min(remain, Math.Max(20, gap)) : remain;
                            int value;
                            try
                            {
                                value = port.ReadByte();
                            }
                            catch (TimeoutException)
                            {
                                if (raw && data.Count > 0)
                                {
                                    break;
                                }

                                if (data.Count > 0)
                                {
                                    Log("RX partial: " + Modbus.Hex(data.ToArray()));
                                }

                                throw;
                            }

                            data.Add((byte)value);
                            if (!echoDone)
                            {
                                if (data.Count <= request.Length && data[data.Count - 1] == request[data.Count - 1])
                                {
                                    if (data.Count == request.Length)
                                    {
                                        data.Clear();
                                        echoDone = true;
                                    }

                                    continue;
                                }

                                echoDone = true;
                            }

                            if (data.Count >= 3 && !raw)
                            {
                                int fn = data[1];
                                expected = (fn & 128) != 0 ? 5 : fn <= 4 ? data[2] + 5 : 8;
                                if (expected > 256)
                                {
                                    throw new IOException("Oversized RTU response.");
                                }
                            }

                            if (!raw && expected > 0 && data.Count == expected)
                            {
                                break;
                            }

                            if (data.Count > 256)
                            {
                                throw new IOException("Oversized RTU frame.");
                            }
                        }

                        byte[] response = data.ToArray();
                        Log("RX: " + Modbus.Hex(response));
                        return response;
                    }
                    finally
                    {
                        if (settings.RtsToggle)
                        {
                            port.RtsEnable = false;
                        }
                    }

                });
            }
            catch (Exception ex)
            {
                Log("ERR: ID=" + request[0] + " F=" + request[1].ToString("00") + " — " + ex.Message);
                throw;
            }
            finally
            {
                lastEnd = DateTime.UtcNow;
                gate.Release();
            }
        }

        public void Dispose()
        {
            connected = false;
            if (port != null)
            {
                port.Dispose();
            }
        }
    }
}
