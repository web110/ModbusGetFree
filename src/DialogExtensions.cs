using System.Windows.Forms;

namespace ModbusGetFree
{
    /// <summary>
    /// 通用对话框中插入多行内容区域的布局辅助方法。
    /// </summary>
    public static class DialogExtensions
    {
        public static void tableHeight(this EditorDialog d, Control c, int height)
        {
            var panel = (TableLayoutPanel)d.Controls[0];
            int row = panel.RowStyles.Count;
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            panel.Controls.Add(c, 0, row);
            panel.SetColumnSpan(c, 2);
            c.Dock = DockStyle.Fill;
            d.AdvanceRow();
        }
    }
}
