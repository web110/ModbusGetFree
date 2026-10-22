using System.Drawing;
namespace ModbusGetFree
{
    /// <summary>在本机兼容文件 Font 对话框核对：Microsoft YaHei UI，常规，小五（9 pt）。</summary>
    public static class UiFonts
    {
        public const string Name = "Microsoft YaHei UI";
        public const float Size = 9;
        public static Font Create()
        {
            return new Font(Name, Size, FontStyle.Regular, GraphicsUnit.Point);
        }
    }
}
