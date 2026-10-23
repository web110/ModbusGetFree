using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ModbusGetFree
{
    /// <summary>
    /// 数据窗口的表格布局、显示刷新和周期通信。编辑、写入、日志、打印分别位于同名 partial 文件。
    /// </summary>
    public partial class DataWindow : Form
    {
        public DocumentState State;
        public event Action<DataWindow> DataUpdated;
        readonly MainWindow host;
        public DataGridView Grid = new BufferedDataGridView();
        readonly Label summary = new BufferedLabel();
        readonly Label result = new BufferedLabel();
        public DateTime NextPoll = DateTime.UtcNow;
        public bool Busy;
        public int Tx;
        public int Errors;
        public long LastMs;
        string lastMessage = "No connection";
        bool rebuilding;
        string csvLog;
        public string DocumentPath;
        ExcelLog excelLog;
        PrintDocument printer;
        string[] printLines;
        int printPosition;

        public DataWindow(MainWindow owner, DocumentState state)
        {
            Icon = AppBrand.LoadDocumentIcon();
            host = owner;
            State = state;
            Text = state.Title;
            Size = new Size(state.Width, state.Height);
            StartPosition = FormStartPosition.Manual;
            Location = new Point(state.Left, state.Top);
            Font = UiFonts.Create();
            BackColor = SystemColors.Control;
            summary.Dock = DockStyle.Top;
            summary.Height = 22;
            summary.Padding = new Padding(5, 3, 0, 0);
            summary.Font = UiFonts.Create();
            result.Dock = DockStyle.Top;
            result.Height = 24;
            result.Padding = new Padding(5, 4, 0, 0);
            Grid.Dock = DockStyle.Fill;
            Grid.AllowUserToAddRows = false;
            Grid.AllowUserToDeleteRows = false;
            Grid.RowHeadersVisible = true;
            Grid.RowHeadersWidth = 32;
            Grid.BackgroundColor = Color.White;
            Grid.BorderStyle = BorderStyle.Fixed3D;
            Grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            Grid.MultiSelect = true;
            Grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            Grid.ColumnHeadersHeight = 25;
            Grid.RowTemplate.Height = 24;
            Grid.EnableHeadersVisualStyles = false;
            Grid.ColumnHeadersDefaultCellStyle.BackColor = SystemColors.Control;
            Grid.DefaultCellStyle.Font = UiFonts.Create();
            Grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            Grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 120, 215);
            Grid.DefaultCellStyle.SelectionForeColor = Color.White;
            Grid.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText;
            Controls.Add(Grid);
            Controls.Add(result);
            Controls.Add(summary);
            Grid.RowPostPaint += (sender, e) =>
            {
                var bounds = new Rectangle(e.RowBounds.Left + 1, e.RowBounds.Top
                    + 1, Grid.RowHeadersWidth - 2, e.RowBounds.Height - 2);
                using (var brush = new SolidBrush(SystemColors.Control))
                {
                    e.Graphics.FillRectangle(brush, bounds);
                }

                TextRenderer.DrawText(e.Graphics, e.RowIndex.ToString(), Grid.Font,
                     bounds, SystemColors.ControlText, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            };
            Grid.CellBeginEdit += (s, e) =>
            {
                if (Grid.Columns[e.ColumnIndex].Name.StartsWith("Value"))
                {
                    e.Cancel = true;
                    BeginInvoke(new Action(() => EditValue(Index(e.RowIndex, e.ColumnIndex))));
                }

            };
            Grid.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && Grid.Columns[e.ColumnIndex].Name.StartsWith("Value"))
                {
                    EditValue(Index(e.RowIndex, e.ColumnIndex));
                }

            };
            Grid.CellEndEdit += (s, e) =>
            {
                if (!rebuilding && Grid.Columns[e.ColumnIndex].Name.StartsWith("Name"))
                {
                    int i = Index(e.RowIndex, e.ColumnIndex);
                    if (i < State.Names.Length)
                    {
                        State.Names[i] = Convert.ToString(Grid[e.ColumnIndex, e.RowIndex].Value);
                        host.MarkDirty();
                    }
                }

            };
            Grid.ContextMenuStrip = BuildContextMenu();
            Grid.CellMouseDown += (sender, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    SelectContextCell(e.RowIndex, e.ColumnIndex);
                }
            };
            FormClosed += (sender, args) =>
            {
                foreach (var font in groupFonts.Values)
                {
                    font.Dispose();
                }
            };
            Rebuild();
        }

        public void Define()
        {
            if (Busy)
            {
                return;
            }

            Busy = true;
            try
            {
                using (var d = new DefinitionDialog(State, () =>
                {
                    Rebuild();
                    host.MarkDirty();
                }, async () =>
                {
                    Busy = false;
                    try
                    {
                        await Execute(true);
                    }
                    finally
                    {
                        Busy = true;
                    }

                }, () => host.Bus.Connected && !host.InFlight))
                {
                    d.ShowDialog(this);
                }
            }
            finally
            {
                Busy = false;
                NextPoll = DateTime.UtcNow;
            }
        }

        public int ValueWidth
        {
            get
            {
                return GroupWidth(SelectedDefinition);
            }
        }

        public bool EnronActive
        {
            get
            {
                return State.Enron && (State.Function == 3 || State.Function == 4
                    || State.Function == 16) && ((State.Address >= 5000 && State.Address < 6000)
                    || (State.Address >= 7000 && State.Address < 8000));
            }
        }

        public int StorageQuantity
        {
            get
            {
                return State.Quantity * (EnronActive ? 2 : 1);
            }
        }

        public void Rebuild()
        {
            rebuilding = true;
            InvalidateGroupMap();
            Array.Resize(ref State.Values, StorageQuantity);
            Array.Resize(ref State.Names, StorageQuantity);
            Grid.Columns.Clear();
            Grid.Rows.Clear();
            int width = RegisterStride;
            int cells = State.Quantity;
            int rows = State.Rows == 0 ? Math.Max(1, cells) : State.Rows;
            int columns = Math.Max(1, (cells + rows - 1) / rows);
            Grid.DefaultCellStyle.ForeColor = Color.FromArgb(State.Foreground);
            Grid.DefaultCellStyle.BackColor = Color.FromArgb(State.Background);
            // 默认容纳约 13 个数字字符，仍可由用户拖动列边界加宽。
            int compactWidth = TextRenderer.MeasureText("0000000000000", Grid.DefaultCellStyle.Font,
                Size.Empty, TextFormatFlags.NoPadding).Width + 14;
            for (int c = 0; c < columns; c++)
            {
                Grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "Name"
                    + c,
                    HeaderText = "Alias",
                    Width = compactWidth,
                    DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight },
                    Visible = !State.HideNames
                });
                Grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "Value" + c,
                    HeaderText = DisplayAddress(c * rows * width),
                    ReadOnly = true,
                    Width = compactWidth,
                    DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
                });
            }

            Grid.Rows.Add(rows);
            for (int r = 0; r < rows; r++)
            {
                Grid.Rows[r].HeaderCell.Value = r.ToString();
                for (int c = 0; c < columns; c++)
                {
                    int i = (c * rows + r) * width;
                    if (i >= StorageQuantity)
                    {
                        Grid[c * 2, r].ReadOnly = true;
                        continue;
                    }

                    Grid[c * 2, r].Value = State.Names[i];
                    Grid[c * 2 + 1, r].Tag = i;
                }
            }

            rebuilding = false;
            RefreshValues();
        }

        public string DisplayAddress(int i)
        {
            int a = State.Address + (EnronActive ? i / 2 : i);
            if (!State.BaseOne)
            {
                return a.ToString("D5");
            }

            return DefinitionDialog.PlcAddress(State.Function, a);
        }

        int Index(int row, int column)
        {
            return (column / 2 * Grid.Rows.Count + row) * RegisterStride;
        }

        public void RefreshValues()
        {
            for (int c = 0; c < Grid.Columns.Count; c += 2)
            {
                for (int r = 0; r < Grid.Rows.Count; r++)
                {
                    int i = Index(r, c);
                    if (i >= StorageQuantity)
                    {
                        continue;
                    }

                    string v = DisplayGroup(i);
                    var cell = Grid[c + 1, r];
                    string text = (State.AddressInCell ? DisplayAddress(i) + " = " : "") + v;
                    if (!object.Equals(cell.Value, text)) cell.Value = text;
                    var definition = DefinitionAt(i);
                    double number;
                    Grid[c + 1, r].Style.BackColor = Color.FromArgb(definition.Background);
                    Grid[c + 1, r].Style.ForeColor = Color.FromArgb(definition.Foreground);
                    if (double.TryParse(FormatGroup(i), NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                    {
                        Grid[c + 1, r].Style.ForeColor = number < definition.ColorBelow
                            ? Color.FromArgb(definition.LowColor) : number > definition.ColorAbove
                            ? Color.FromArgb(definition.HighColor) : Color.FromArgb(definition.Foreground);
                    }

                    Grid[c + 1, r].Style.Font = GroupFont(definition);

                }
            }

            UpdateSummary();
        }

        public void UpdateSummary()
        {
            string summaryText = "Tx = " + Tx + ": Err = " + Errors + ": ID = " + State.Unit
                + ": F = " + State.Function.ToString("00") + ": SR = " + State.ScanRate + "ms"
                + (State.Disabled ? "  Disabled" : "") + (State.ShowErrors ? "  Last = " + LastMs
                + "ms" : "");
            if (Language.Chinese) summaryText = summaryText.Replace("Tx = ", "发送 = ").Replace("Err = ", "错误 = ").Replace("ID = ", "从站 = ").Replace("F = ", "功能码 = ").Replace("SR = ", "周期 = ").Replace("Disabled", "已禁用").Replace("Last = ", "耗时 = ");
            if (summary.Text != summaryText) summary.Text = summaryText;
            string resultText = host.Bus.Connected ? lastMessage : "No connection";
            resultText = Language.Text(resultText);
            if (result.Text != resultText) result.Text = resultText;
            Color color = !host.Bus.Connected || lastMessage.StartsWith("Error")
                ? Color.Red : SystemColors.ControlText;
            if (result.ForeColor != color) result.ForeColor = color;
        }

        public async Task Execute(bool once)
        {
            if (Busy || host.InFlight || (!once && State.Disabled) || !host.Bus.Connected)
            {
                return;
            }

            Busy = true;
            host.InFlight = true;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                int fn = State.Function;
                ushort[] write = fn > 4 ? (ushort[])State.Values.Clone() : null;
                byte[] q = EnronActive ? Modbus.EnronRequest(State.Unit, fn,
                     State.Address, State.Quantity, write) : Modbus.Request(State.Unit, fn, State.Address,
                     State.Quantity, write);
                Tx++;
                byte[] r = await host.Bus.Exchange(q, false, EnronActive);
                if (State.Unit != 0)
                {
                    ushort[] values = Modbus.Decode(r, q, EnronActive && fn <= 4 ? State.Quantity * 2 : State.Quantity);
                    if (fn <= 4)
                    {
                        State.Values = values;
                    }
                }

                lastMessage = State.Unit == 0 ? "Broadcast sent" : "Response OK";
                if (csvLog != null)
                {
                    try
                    {
                        AppendLog();
                    }
                    catch (Exception logError)
                    {
                        csvLog = null;
                        lastMessage = "Data log stopped: " + logError.Message;
                    }
                }

                if (excelLog != null)
                {
                    try
                    {
                        excelLog.Append(this);
                    }
                    catch (Exception logError)
                    {
                        StopExcelLog();
                        lastMessage = "Excel log stopped: " + logError.Message;
                    }
                }

                var updated = DataUpdated;
                if (updated != null)
                {
                    updated(this);
                }
            }
            catch (Exception ex)
            {
                Errors++;
                lastMessage = "Error: " + ex.Message;
                if (State.DisableOnError)
                {
                    State.Disabled = true;
                }
            }
            finally
            {
                watch.Stop();
                LastMs = watch.ElapsedMilliseconds;
                NextPoll = DateTime.UtcNow.AddMilliseconds(State.ScanRate);
                Busy = false;
                host.InFlight = false;
                if (!IsDisposed)
                {
                    RefreshValues();
                }
            }
        }

        public void ToggleDisabled()
        {
            State.Disabled = !State.Disabled;
            NextPoll = DateTime.UtcNow;
            UpdateSummary();
            host.MarkDirty();
        }

        public void ChooseFormat(int format, int? order = null)
        {
            SetGroupFormat(SelectedGroupStarts(), format, order);
        }

        public void ChooseFont()
        {
            using (var dialog = new FontDialog { Font = GroupFont(SelectedDefinition) })
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    UpdateSelectedDefinitions(definition =>
                    {
                        definition.FontName = dialog.Font.Name;
                        definition.FontSize = dialog.Font.Size;
                        definition.FontStyle = (int)dialog.Font.Style;
                    });
                }
            }
        }

        public void Copy()
        {
            try
            {
                var content = Grid.GetClipboardContent();
                if (content != null)
                {
                    Clipboard.SetDataObject(content);
                }
            }
            catch (Exception ex)
            {
                LocalMessageBox.Show(this, ex.Message);
            }
        }

        public DocumentState Snapshot()
        {
            State.Left = Left;
            State.Top = Top;
            State.Width = Width;
            State.Height = Height;
            return State;
        }
        public int SelectedRawIndex
        {
            get
            {
                return Grid.CurrentCell == null ? 0 : GroupStart(Index(Grid.CurrentCell.RowIndex, Grid.CurrentCell.ColumnIndex));
            }
        }
    }
}
