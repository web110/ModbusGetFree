using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ModbusGetFree
{
    /// <summary>
    /// 优先读取 assets/icons/active 中可编辑的 PNG；缺失时使用自绘图标。
    /// </summary>
    public static class ClassicIcons
    {
        public static string FileName(int id)
        {
            switch (id)
            {
                case 57600: return "new.png";
                case 57601: return "open.png";
                case 57603: return "save.png";
                case 57607: return "print.png";
                case 32801: return "reset-counters.png";
                case 32771: return "definition.png";
                case 32773: return "disconnect.png";
                case 32838: return "quick-connect.png";
                case 32854: return "read-once.png";
                case 32842: return "write-coil-05.png";
                case 32844: return "write-register-06.png";
                case 32846: return "write-coils-15.png";
                case 32848: return "write-registers-16.png";
                case 32894: return "report-id-17.png";
                case 32849: return "mask-write-22.png";
                case 32839: return "read-write-23.png";
                case 32826: return "test-center.png";
                case 32825: return "traffic.png";
                case 32936: return "chart.png";
                case 57664: return "about.png";
                case 57670: return "context-help.png";
                default: return null;
            }
        }

        public static Bitmap Create(int id)
        {
            return Create(id, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "icons", "active"));
        }

        public static Bitmap Create(int id, string iconDirectory)
        {
            string name = FileName(id);
            if (name != null)
            {
                string file = Path.Combine(iconDirectory, name);
                if (File.Exists(file))
                {
                    try
                    {
                        // 复制后立即释放源图片，便于运行时用编辑器修改或替换文件。
                        using (var image = new Bitmap(file))
                        {
                            if (image.Width <= 256 && image.Height <= 256)
                            {
                                return new Bitmap(image);
                            }
                        }
                    }
                    catch (ArgumentException) { }
                    catch (IOException) { }
                    catch (System.Runtime.InteropServices.ExternalException) { }
                }
            }

            return DrawBuiltIn(id);
        }

        static Bitmap DrawBuiltIn(int id)
        {
            var b = new Bitmap(18, 18);
            using (var g = Graphics.FromImage(b))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.None;
                using (var black = new Pen(Color.Black, 1))
                {
                    string token = id == 32842 ? "05" : id == 32844 ? "06" : id == 32846 ? "15"
                        : id == 32848 ? "16" : id == 32894 ? "17" : id == 32849 ? "22"
                        : id == 32839 ? "23" : id == 32826 ? "TC" : null;
                    if (token != null)
                    {
                        using (var font = new Font("Arial", 13, FontStyle.Bold, GraphicsUnit.Pixel))
                        {
                            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
                            // GenericTypographic 去掉默认边距；NoWrap 防止第二个数字换到图标边界外。
                            using (var format = new StringFormat(StringFormat.GenericTypographic))
                            {
                                format.FormatFlags |= StringFormatFlags.NoWrap;
                                format.Alignment = StringAlignment.Center;
                                format.LineAlignment = StringAlignment.Center;
                                g.DrawString(token, font, Brushes.Black, new RectangleF(0, 0, 18, 18), format);
                            }
                        }

                        return b;
                    }

                    switch (id)
                    {
                        case 57600:
                            g.FillRectangle(Brushes.White, 4, 1, 10, 15);
                            g.DrawRectangle(black, 4, 1, 10, 15);
                            g.DrawLines(black, new[] { new Point(10, 1), new Point(10, 5), new Point(14, 5) });
                            break;
                        case 57601:
                            g.FillPolygon(Brushes.Khaki, new[] {
                                new Point(1, 5),
                                new Point(6, 5),
                                new Point(7, 3),
                                new Point(13, 3),
                                new Point(16, 7),
                                new Point(14, 15),
                                new Point(1, 15)
                            });
                            g.DrawPolygon(black, new[] {
                                new Point(1, 5),
                                new Point(6, 5),
                                new Point(7, 3),
                                new Point(13, 3),
                                new Point(16, 7),
                                new Point(14, 15),
                                new Point(1, 15)
                            });
                            g.DrawLine(black, 2, 8, 15, 8);
                            break;
                        case 57603:
                            g.FillRectangle(Brushes.DarkKhaki, 2, 1, 14, 15);
                            g.DrawRectangle(black, 2, 1, 14, 15);
                            g.FillRectangle(Brushes.White, 5, 2, 8, 5);
                            g.DrawRectangle(black, 5, 2, 8, 5);
                            g.FillRectangle(Brushes.White, 5, 10, 8, 6);
                            g.DrawRectangle(black, 5, 10, 8, 6);
                            break;
                        case 57607:
                            g.FillRectangle(Brushes.White, 4, 1, 10, 6);
                            g.DrawRectangle(black, 4, 1, 10, 6);
                            g.FillRectangle(Brushes.LightGray, 1, 6, 16, 7);
                            g.DrawRectangle(black, 1, 6, 16, 7);
                            g.FillRectangle(Brushes.White, 4, 10, 10, 7);
                            g.DrawRectangle(black, 4, 10, 10, 7);
                            g.DrawLine(black, 6, 12, 12, 12);
                            g.DrawLine(black, 6, 14, 12, 14);
                            break;
                        case 32801:
                            using (var red = new Pen(Color.DarkRed, 2))
                            {
                                g.DrawLine(red, 2, 2, 15, 15);
                                g.DrawLine(red, 15, 2, 2, 15);
                            }

                            break;
                        case 32771:
                            g.FillRectangle(Brushes.White, 2, 1, 14, 16);
                            g.DrawRectangle(black, 2, 1, 14, 16);
                            g.FillRectangle(Brushes.Navy, 3, 2, 12, 3);
                            g.DrawRectangle(Pens.Gray, 5, 7, 8, 6);
                            break;
                        case 32773:
                        case 32838:
                            g.DrawRectangle(black, 4, 5, 10, 9);
                            g.DrawLine(black, 7, 1, 7, 5);
                            g.DrawLine(black, 11, 1, 11, 5);
                            g.DrawLine(black, 4, 8, 14, 8);
                            g.DrawLine(black, 9, 14, 9, 17);
                            if (id == 32773)
                            {
                                g.DrawLine(Pens.Red, 1, 16, 17, 1);
                            }

                            break;
                        case 32854:
                            g.DrawLines(Pens.Gray, new[] {
                                new Point(1, 15),
                                new Point(6, 15),
                                new Point(6, 3),
                                new Point(11, 3),
                                new Point(11, 15),
                                new Point(17, 15)
                            });
                            break;
                        case 32825:
                            g.DrawRectangle(black, 1, 1, 13, 15);
                            g.DrawEllipse(Pens.Gray, 4, 3, 9, 9);
                            g.DrawLine(new Pen(Color.DimGray, 2), 11, 11, 16, 16);
                            break;
                        case 32936:
                            g.FillRectangle(Brushes.Black, 0, 0, 18, 18);
                            g.DrawLine(Pens.Gray, 0, 9, 18, 9);
                            g.DrawLines(Pens.Lime, new[] {
                                new Point(0, 8),
                                new Point(4, 7),
                                new Point(7, 3),
                                new Point(11, 6),
                                new Point(15, 4),
                                new Point(17, 5)
                            });
                            g.DrawLines(Pens.Yellow, new[] {
                                new Point(0, 15),
                                new Point(5, 12),
                                new Point(10, 13),
                                new Point(17, 11)
                            });
                            break;
                        default:
                            using (var font = new Font("Arial", 14, FontStyle.Bold, GraphicsUnit.Pixel))
                            {
                                g.DrawString("?", font, id == 57664 ? Brushes.Olive : Brushes.Navy, 1, -1);
                                if (id == 57670)
                                {
                                    g.DrawLines(black, new[] {
                                        new Point(1, 5),
                                        new Point(1, 15),
                                        new Point(4, 12),
                                        new Point(7, 16)
                                    });
                                }
                            }

                            break;
                    }
                }
            }

            return b;
        }
    }
}
