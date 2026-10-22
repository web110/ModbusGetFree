using System.Collections.Generic;

namespace ModbusGetFree
{
    /// <summary>
    /// 工作区文件模型：一个连接配置和多个数据窗口状态。
    /// </summary>
    public class Workspace
    {
        public SerialSettings Connection = new SerialSettings();
        public List<DocumentState> Documents = new List<DocumentState>();
    }
}
