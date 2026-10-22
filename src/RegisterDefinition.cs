using System.Collections.Generic;

namespace ModbusGetFree
{
    /// <summary>
    /// 一个数据组的独立显示定义。Address 是协议地址；原始寄存器始终按读取顺序保存。
    /// </summary>
    public class RegisterDefinition
    {
        public int Address;
        public int Format;
        public int Order;
        public double Scale = 1;
        public double Offset;
        public int Foreground = System.Drawing.Color.Black.ToArgb();
        public int Background = System.Drawing.Color.White.ToArgb();
        public double ColorBelow = double.MinValue;
        public double ColorAbove = double.MaxValue;
        public int LowColor = System.Drawing.Color.Blue.ToArgb();
        public int HighColor = System.Drawing.Color.Red.ToArgb();
        public string FontName = UiFonts.Name;
        public float FontSize = UiFonts.Size;
        public int FontStyle;

        public RegisterDefinition Copy()
        {
            return (RegisterDefinition)MemberwiseClone();
        }
    }
}
