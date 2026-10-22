using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace ModbusGetFree
{
    /// <summary>
    /// 表格粘贴、别名剪切、配色及独立数据窗口文件保存。
    /// </summary>
    public partial class DataWindow
    {
        public void SaveDocument(bool saveAs)
        {
            string file = DocumentPath;
            if (saveAs || file == null)
            {
                using (var d = new SaveFileDialog
                {
                    Filter = "RTU Data Window|*.mpdoc",
                    FileName = State.Title + ".mpdoc"
                }

                )
                {
                    if (d.ShowDialog(this) != DialogResult.OK)
                    {
                        return;
                    }

                    file = d.FileName;
                }
            }

            using (var stream = File.Create(file))
            {
                new XmlSerializer(typeof(DocumentState)).Serialize(stream, Snapshot());
            }

            DocumentPath = file;
            host.RememberFile(file);
        }

        public void Cut()
        {
            Copy();
            foreach (DataGridViewCell cell in Grid.SelectedCells)
            {
                if (cell.OwningColumn.Name.StartsWith("Name"))
                {
                    int i = Index(cell.RowIndex, cell.ColumnIndex);
                    if (i < State.Names.Length)
                    {
                        State.Names[i] = "";
                        cell.Value = "";
                    }
                }
            }

            host.MarkDirty();
        }

        public void Paste()
        {
            if (!Clipboard.ContainsText())
            {
                return;
            }

            PasteText(Clipboard.GetText());
        }

        public void PasteText(string text)
        {
            if (Grid.CurrentCell == null)
            {
                return;
            }

            var values = (ushort[])State.Values.Clone();
            var names = (string[])State.Names.Clone();
            int startRow = Grid.CurrentCell.RowIndex, startColumn = Grid.CurrentCell.ColumnIndex;
            string[] lines = text.Replace("\r", "").TrimEnd('\n').Split('\n');
            for (int r = 0; r < lines.Length && r + startRow < Grid.Rows.Count; r++)
            {
                string[] fields = lines[r].Split('\t');
                for (int c = 0; c < fields.Length && c + startColumn < Grid.Columns.Count; c++)
                {
                    int column = startColumn + c, index = Index(startRow + r, column);
                    if (index >= StorageQuantity)
                    {
                        continue;
                    }

                    if (column % 2 == 0)
                    {
                        names[index] = fields[c];
                    }
                    else
                    {
                        if (fields[c] == "--")
                        {
                            continue;
                        }

                        if (GroupStart(index) != index)
                        {
                            throw new FormatException("This register belongs to the preceding multi-register value.");
                        }

                        ushort[] words = EncodeGroup(index, fields[c]);
                        if ((State.Function <= 2 || State.Function == 5 || State.Function == 15) && words[0] > 1)
                        {
                            throw new FormatException("Coil / input values must be 0 or 1.");
                        }

                        if (index + words.Length > values.Length)
                        {
                            throw new FormatException("Incomplete register group.");
                        }

                        Array.Copy(words, 0, values, index, words.Length);
                    }
                }
            }

            State.Values = values;
            State.Names = names;
            Rebuild();
            host.MarkDirty();
        }

        public void ChooseColors()
        {
            var current = SelectedDefinition;
            using (var d = new EditorDialog("Colors", 360))
            {
                var foreground = new Button
                {
                    Text = "Text color...",
                    BackColor = Color.FromArgb(current.Foreground)
                };
                var background = new Button
                {
                    Text = "Background...",
                    BackColor = Color.FromArgb(current.Background)
                };
                var low = new TextBox
                {
                    Text = current.ColorBelow == double.MinValue ? "" : current.ColorBelow.ToString(CultureInfo.InvariantCulture)
                };
                var high = new TextBox
                {
                    Text = current.ColorAbove == double.MaxValue ? "" : current.ColorAbove.ToString(CultureInfo.InvariantCulture)
                };
                var lowColor = new Button
                {
                    Text = "Below color...",
                    BackColor = Color.FromArgb(current.LowColor)
                };
                var highColor = new Button
                {
                    Text = "Above color...",
                    BackColor = Color.FromArgb(current.HighColor)
                };
                foreach (var button in new[]
                {
                    foreground,
                    background,
                    lowColor,
                    highColor
                }

                )
                {
                    var target = button;
                    target.Click += (s, e) =>
                    {
                        using (var color = new ColorDialog
                        {
                            Color = target.BackColor
                        }

                        )
                        {
                            if (color.ShowDialog(d) == DialogResult.OK)
                            {
                                target.BackColor = color.Color;
                            }
                        }

                    };
                }

                d.Field("Foreground", foreground);
                d.Field("Background", background);
                d.Field("Below threshold (blank: off)", low);
                d.Field("Below threshold color", lowColor);
                d.Field("Above threshold (blank: off)", high);
                d.Field("Above threshold color", highColor);
                d.Buttons(() =>
                {
                    double lo = string.IsNullOrWhiteSpace(low.Text) ? double.MinValue
                        : double.Parse(low.Text, CultureInfo.InvariantCulture),
                             hi = string.IsNullOrWhiteSpace(high.Text)
                        ? double.MaxValue : double.Parse(high.Text, CultureInfo.InvariantCulture);
                    if (double.IsNaN(lo) || double.IsInfinity(lo) || double.IsNaN(hi)
                        || double.IsInfinity(hi) || lo > hi)
                    {
                        throw new ArgumentException("Invalid thresholds.");
                    }

                    UpdateSelectedDefinitions(group =>
                    {
                        group.Foreground = foreground.BackColor.ToArgb();
                        group.Background = background.BackColor.ToArgb();
                        group.LowColor = lowColor.BackColor.ToArgb();
                        group.HighColor = highColor.BackColor.ToArgb();
                        group.ColorBelow = lo;
                        group.ColorAbove = hi;
                    });
                    return true;
                });
                d.ShowDialog(this);
            }
        }
    }
}
