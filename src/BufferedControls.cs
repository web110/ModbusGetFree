using System.Windows.Forms;
namespace ModbusGetFree
{
    // 将背景和文字一次性绘制到缓冲区，避免每次更新 Tx 时先擦除背景。
    sealed class BufferedLabel : Label
    {
        public BufferedLabel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer, true);
            UpdateStyles();
        }
    }

    sealed class BufferedDataGridView : DataGridView
    {
        public BufferedDataGridView()
        {
            DoubleBuffered = true;
        }
    }
}
