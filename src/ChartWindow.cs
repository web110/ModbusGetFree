using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ModbusGetFree
{
    /// <summary>
    /// 12 路实时图表，负责绑定、采样、绘制和 CSV 导出。
    /// </summary>
    public class ChartWindow : Form
    {
        class LinkInfo
        {
            public DataWindow Document;
            public int Index;
            public string Name;
            public List<Tuple<DateTime, double>> Points = new List<Tuple<DateTime, double>>();
        }

        LinkInfo[] links = new LinkInfo[12];
        Color[] palette =
        {
            Color.Lime,
            Color.Yellow,
            Color.Cyan,
            Color.Orange,
            Color.Magenta,
            Color.White,
            Color.Red,
            Color.DodgerBlue,
            Color.Pink,
            Color.LightGreen,
            Color.Gold,
            Color.Silver
        };
        Panel canvas;
        bool paused;
        public int SampleCount
        {
            get
            {
                return links.Where(l => l != null).Sum(l => l.Points.Count);
            }
        }

        public ChartWindow()
        {
            Icon = AppBrand.LoadIcon();
            Text = "Real time Charting";
            Size = new Size(950, 550);
            Font = UiFonts.Create();
            var bar = new ToolStrip();
            var pause = new ToolStripButton("Pause");
            pause.Click += (s, e) =>
            {
                paused = !paused;
                pause.Text = paused ? "Resume" : "Pause";
            };
            var clear = new ToolStripButton("Clear");
            clear.Click += (s, e) =>
            {
                foreach (var l in links)
                {
                    if (l != null)
                    {
                        l.Points.Clear();
                    }
                }

                canvas.Invalidate();
            };
            var export = new ToolStripButton("Export CSV...");
            export.Click += (s, e) =>
            {
                using (var d = new SaveFileDialog
                {
                    Filter = "CSV|*.csv"
                }

                )
                {
                    if (d.ShowDialog(this) == DialogResult.OK)
                    {
                        try
                        {
                            Export(d.FileName);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(this, ex.Message);
                        }
                    }
                }

            };
            bar.Items.AddRange(new ToolStripItem[] { pause, clear, export });
            canvas = new BufferedCanvas
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black
            };
            canvas.Paint += (s, e) => Draw(e.Graphics);
            Controls.Add(canvas);
            Controls.Add(bar);
        }

        class BufferedCanvas : Panel
        {
            public BufferedCanvas()
            {
                DoubleBuffered = true;
                ResizeRedraw = true;
            }
        }

        public void Link(int series, DataWindow doc, int index)
        {
            if (series < 0 || series >= 12 || index < 0 || index + doc.GroupWidth(doc.DefinitionAt(index)) > doc.StorageQuantity)
            {
                throw new ArgumentException("Select a complete value cell.");
            }

            index = doc.GroupStart(index);
            links[series] = new LinkInfo
            {
                Document = doc,
                Index = index,
                Name = doc.State.Title + " / " + doc.DisplayAddress(index)
            };
            Sample(doc);
        }

        public void Unlink(DataWindow doc, int index)
        {
            for (int i = 0; i < links.Length; i++)
            {
                if (links[i] != null && links[i].Document == doc && links[i].Index == index)
                {
                    links[i] = null;
                }
            }

            canvas.Invalidate();
        }

        public void UnlinkAll()
        {
            links = new LinkInfo[12];
            canvas.Invalidate();
        }

        public void Sample(DataWindow doc)
        {
            if (paused)
            {
                return;
            }

            foreach (var link in links)
            {
                if (link == null || link.Document != doc || link.Index + doc.GroupWidth(doc.DefinitionAt(link.Index)) > doc.State.Values.Length)
                {
                    continue;
                }

                string text = doc.FormatGroup(link.Index);
                double value;
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                {
                    value = doc.State.Values[link.Index];
                }

                if (double.IsNaN(value) || double.IsInfinity(value))
                {
                    continue;
                }

                link.Points.Add(Tuple.Create(DateTime.Now, value));
                if (link.Points.Count > 5000)
                {
                    link.Points.RemoveAt(0);
                }
            }

            canvas.Invalidate();
        }

        public void Export(string file)
        {
            var lines = new List<string>
            {
                "Series,Timestamp,Value"
            };
            for (int i = 0; i < links.Length; i++)
            {
                if (links[i] != null)
                {
                    foreach (var p in links[i].Points)
                    {
                        lines.Add((i + 1) + "," + p.Item1.ToString("o") + ","
                            + p.Item2.ToString("R", CultureInfo.InvariantCulture));
                    }
                }
            }

            File.WriteAllLines(file, lines);
        }

        void Draw(Graphics g)
        {
            var all = links.Where(l => l != null).SelectMany(l => l.Points).ToArray();
            var bounds = new Rectangle(70, 25, Math.Max(10, canvas.Width - 95), Math.Max(10, canvas.Height - 125));
            using (var gridPen = new Pen(Color.FromArgb(55, 65, 55)))
            {
                for (int i = 0; i <= 10; i++)
                {
                    int x = bounds.Left + bounds.Width * i / 10;
                    g.DrawLine(gridPen, x, bounds.Top, x, bounds.Bottom);
                    int y = bounds.Top + bounds.Height * i / 10;
                    g.DrawLine(gridPen, bounds.Left, y, bounds.Right, y);
                }
            }

            if (all.Length == 0)
            {
                g.DrawString("Select a value cell, then Display > Link to Chart > Series 1..12",
                     Font, Brushes.White, 80, 45);
                return;
            }

            double min = all.Min(p => p.Item2), max = all.Max(p => p.Item2);
            if (min == max)
            {
                min -= 1;
                max += 1;
            }

            DateTime start = all.Min(p => p.Item1), end = all.Max(p => p.Item1);
            double span = Math.Max(1, (end - start).TotalMilliseconds);
            for (int i = 0; i <= 5; i++)
            {
                double v = max - (max - min) * i / 5;
                g.DrawString(v.ToString("G6"), Font, Brushes.Silver, 2, bounds.Top + bounds.Height * i / 5 - 7);
            }

            g.DrawString(start.ToString("HH:mm:ss"), Font, Brushes.Silver, bounds.Left, bounds.Bottom + 4);
            g.DrawString(end.ToString("HH:mm:ss"), Font, Brushes.Silver, bounds.Right - 55, bounds.Bottom + 4);
            for (int i = 0; i < 12; i++)
            {
                var l = links[i];
                if (l == null)
                {
                    continue;
                }

                using (var pen = new Pen(palette[i], 1.5f))
                {
                    var pts = l.Points.Select(p => new PointF(bounds.Left + (float)((p.Item1 - start).TotalMilliseconds / span * bounds.Width),

                         bounds.Bottom - (float)((p.Item2 - min) / (max - min) * bounds.Height))).ToArray();
                    if (pts.Length >= 2)
                    {
                        g.DrawLines(pen, pts);
                    }
                    else if (pts.Length == 1)
                    {
                        g.DrawEllipse(pen, pts[0].X - 2, pts[0].Y - 2, 4, 4);
                    }
                }

                using (var brush = new SolidBrush(palette[i]))
                {
                    g.DrawString((i + 1) + ": " + l.Name, Font, brush, bounds.Left
                        + i % 3 * 250, bounds.Bottom + 28 + i / 3 * 18);
                }
            }
        }
    }
}
