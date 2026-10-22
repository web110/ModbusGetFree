using System.Collections.Generic;

namespace ModbusGetFree
{
    /// <summary>
    /// 单个数据窗口的可序列化状态，包含读写定义、显示选项、别名和最近数据。
    /// </summary>
    public class DocumentState
    {
        public List<RegisterDefinition> Definitions = new List<RegisterDefinition>();
        public string Title = "";
        public int Unit = 1;
        public int Function = 3;
        public int Address = 0;
        public int Quantity = 10;
        public int ScanRate = 1000;
        public int Columns = 1;
        public int Format = 0;
        public int Order = 0;
        public bool Disabled;
        public bool DisableOnError;
        public bool HideNames;
        public bool AddressInCell;
        public bool BaseOne;
        public bool Enron;
        public int Rows = 10;
        public bool ShowErrors;
        public int Foreground = System.Drawing.Color.Black.ToArgb();
        public int Background = System.Drawing.Color.White.ToArgb();
        public double ColorBelow = double.MinValue;
        public double ColorAbove = double.MaxValue;
        public int LowColor = System.Drawing.Color.Blue.ToArgb();
        public int HighColor = System.Drawing.Color.Red.ToArgb();
        public double Scale = 1;
        public double Offset;
        public string[] Names = new string[0];
        public ushort[] Values = new ushort[0];
        public int Left = 0;
        public int Top = 0;
        public int Width = 590;
        public int Height = 430;
    }
}
