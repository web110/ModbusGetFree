using System;
using System.Drawing;
using System.Windows.Forms;

namespace ModbusGetFree
{
    public partial class MainWindow
    {
        /// <summary>非模态工具窗口首次显示和再次唤起时，均放在主窗口所在屏幕内。</summary>
        void ShowToolWindow(Form window)
        {
            if (window.WindowState != FormWindowState.Normal)
                window.WindowState = FormWindowState.Normal;
            window.StartPosition = FormStartPosition.Manual;
            window.Bounds = ToolWindowBounds(Bounds, Screen.FromControl(this).WorkingArea, window.Size);
            if (!window.Visible) window.Show(this);
            // 首次 Show 会按系统 DPI 自动缩放，必须再按缩放后的实际尺寸限制位置。
            window.Bounds = ToolWindowBounds(Bounds, Screen.FromControl(this).WorkingArea, window.Size);
            window.BringToFront();
            window.Activate();
        }

        public static Rectangle ToolWindowBounds(Rectangle owner, Rectangle work, Size requested)
        {
            int width = Math.Min(requested.Width, work.Width);
            int height = Math.Min(requested.Height, work.Height);
            Rectangle anchor = Rectangle.Intersect(owner, work);
            if (anchor.IsEmpty) anchor = work;
            int left = anchor.Left + (anchor.Width - width) / 2;
            int top = anchor.Top + (anchor.Height - height) / 2;
            left = Math.Max(work.Left, Math.Min(left, work.Right - width));
            top = Math.Max(work.Top, Math.Min(top, work.Bottom - height));
            return new Rectangle(left, top, width, height);
        }
    }
}
