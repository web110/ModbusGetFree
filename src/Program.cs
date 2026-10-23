using System;
using System.Drawing;
using System.Windows.Forms;

namespace ModbusGetFree
{
    /// <summary>
    /// 应用程序入口、WinForms 初始化和界面预览生成入口。
    /// </summary>
    public static class Program
    {
        [STAThread]

        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (s, e) => LocalMessageBox.Show(e.Exception.Message, "Application Error");
            if (args.Length > 0 && args[0] == "--render")
            {
                Render(args.Length > 1 ? args[1] : "preview.png");
                return;
            }

            Language.Initialize();
            Application.Run(new MainWindow(false));
        }

        static void Render(string path)
        {
            using (var f = new MainWindow(true))
            {
                f.CreateControl();
                f.Show();
                var a = f.NewDocument(new DocumentState
                {
                    Title = "Data1",
                    Unit = 1,
                    Function = 3,
                    Quantity = 10,
                    Left = 12,
                    Top = 8,
                    Width = 560,
                    Height = 440
                });
                for (int i = 0; i < 10; i++)
                {
                    a.State.Values[i] = (ushort)(100 + i);
                    a.State.Names[i] = new[]
                    {
                        "Temperature",
                        "Pressure",
                        "Flow",
                        "Level",
                        "Setpoint",
                        "Status",
                        "Counter",
                        "Speed",
                        "Output",
                        "Alarm"
                    }[i];
                }

                a.Tx = 128;
                a.Rebuild();
                var b = f.NewDocument(new DocumentState
                {
                    Title = "Data2",
                    Unit = 2,
                    Function = 1,
                    Quantity = 16,
                    Columns = 2,
                    Left = 590,
                    Top = 8,
                    Width = 570,
                    Height = 440
                });
                for (int i = 0; i < 16; i++)
                {
                    b.State.Values[i] = (ushort)(i % 2);
                }

                b.Tx = 64;
                b.RefreshValues();
                f.LayoutMdi(MdiLayout.TileVertical);
                Application.DoEvents();
                using (var bitmap = new Bitmap(f.Width, f.Height))
                {
                    f.DrawToBitmap(bitmap, new Rectangle(0, 0, f.Width, f.Height));
                    bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }

                Environment.Exit(0);
            }
        }
    }
}
