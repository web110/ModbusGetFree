using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Windows.Forms;

namespace ModbusGetFree
{
    /// <summary>
    /// 打印文档生成、分页、打印预览和页面设置。
    /// </summary>
    public partial class DataWindow
    {
        void EnsurePrinter()
        {
            if (printer != null)
            {
                return;
            }

            printer = new PrintDocument();
            printer.BeginPrint += (s, e) =>
            {
                printPosition = 0;
                printLines = new[]
                {
                    State.Title + "    Slave " + State.Unit + "    Function " + State.Function,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    "Address     Alias                         Value"
                }.Concat(GroupStarts().Select(i =>
                {
                    return DisplayAddress(i).PadRight(12) + (State.Names[i] ?? "").PadRight(30) + FormatGroup(i);
                })).ToArray();
            };
            printer.PrintPage += (s, e) =>
            {
                using (var font = new Font("Courier New", 10))
                {
                    float y = e.MarginBounds.Top;
                    while (printPosition < printLines.Length && y + font.GetHeight(e.Graphics) < e.MarginBounds.Bottom)
                    {
                        e.Graphics.DrawString(printLines[printPosition++], font, Brushes.Black, e.MarginBounds.Left, y);
                        y += font.GetHeight(e.Graphics) + 3;
                    }

                    e.HasMorePages = printPosition < printLines.Length;
                }

            };
            FormClosed += (s, e) => printer.Dispose();
        }

        public void PrintSetup()
        {
            EnsurePrinter();
            using (var d = new PageSetupDialog
            {
                Document = printer
            }

            )
            {
                d.ShowDialog(this);
            }
        }

        public void Print(bool preview)
        {
            EnsurePrinter();
            if (preview)
            {
                using (var d = new PrintPreviewDialog
                {
                    Document = printer,
                    Width = 950,
                    Height = 750
                }

                )
                {
                    d.ShowDialog(this);
                }
            }
            else
            {
                using (var d = new PrintDialog
                {
                    Document = printer,
                    UseEXDialog = true
                }

                )
                {
                    if (d.ShowDialog(this) == DialogResult.OK)
                    {
                        printer.Print();
                    }
                }
            }
        }
    }
}
