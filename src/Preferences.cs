using System.Collections.Generic;

namespace ModbusGetFree
{
    /// <summary>
    /// 程序偏好配置，包括自动连接、默认窗口定义和最近文件列表。
    /// </summary>
    public class Preferences
    {
        public SerialSettings Connection = new SerialSettings();
        public bool AutoConnect;
        public bool AllowUpdates;
        public string Language = "en";
        public DocumentState Default;
        public List<string> Recent = new List<string>();
    }
}
