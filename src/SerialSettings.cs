using System.IO.Ports;

namespace ModbusGetFree
{
    /// <summary>
    /// 串口连接参数。只保存配置，不负责打开串口。
    /// </summary>
    public class SerialSettings
    {
        public string Port = "COM1";
        public int Baud = 9600;
        public int DataBits = 8;
        public Parity Parity = Parity.None;
        public StopBits StopBits = StopBits.One;
        public int Timeout = 1000;
        public int Delay = 20;
        public bool RtsToggle;
        public bool Dtr;
        public bool RemoveEcho;
        public bool Demo;
    }
}
