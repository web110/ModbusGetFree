using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ModbusGetFree
{
    /// <summary>
    /// 05/06/15/16/17/22/23 功能码的独立参数与发送窗口。
    /// </summary>
    public partial class FunctionDialog : EditorDialog
    {
        readonly MainWindow host;
        readonly int function;
        NumericUpDown unit;
        NumericUpDown address;
        NumericUpDown readAddress;
        NumericUpDown readQuantity;
        TextBox values = new TextBox();
        TextBox andMask = new TextBox
        {
            Text = "0xFFFF"
        };
        TextBox orMask = new TextBox
        {
            Text = "0"
        };
        TextBox output = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both
        };
        Button send;
        ComboBox useFunction;
        ComboBox dataType;
        ComboBox byteOrder;
        CheckBox closeOnSuccess;
        Label readResult;

        NumericUpDown quantity;
        DataGridView writeGrid;
        CheckBox coilOn;
        RegisterDefinition selectedDefinition;
        Func<int, int, int, ushort[], Task> selectedWriter;
        public string ResultText { get { return output.Text; } }

        public void ConfigureSelectedWrite(int slave, int start, string text, RegisterDefinition definition,
            Func<int, int, int, ushort[], Task> writer)
        {
            unit.Value = slave;
            address.Value = start;
            values.Text = text;
            selectedDefinition = definition.Copy();
            selectedWriter = writer;
            if (function == 5) coilOn.Checked = ValueCodec.ParseWord(text) != 0;
            if (function == 6 && ValueCodec.Width(definition.Format) > 1)
            {
                string[] titles = { "Write int 32", "Write unsigned int 32", "Write float 32", "Write int 64", "Write unsigned int 64", "Write double 64" };
                Text = titles[definition.Format - 4];
                singleFunctionGroup.Visible = false;
                ClientSize = new Size(340, 270);
                values.Width = ClientSize.Width - values.Left - 14;
            }
            Shown += (s, e) =>
            {
                if (function == 6) { values.Focus(); values.SelectAll(); }
            };
        }

        async Task TransmitSelected()
        {
            if (host.InFlight) { ShowResult("Bus busy. Please try again."); return; }
            try
            {
                ushort[] words = function == 5 ? new[] { (ushort)(coilOn.Checked ? 1 : 0) }
                    : ValueCodec.Encode(values.Text, selectedDefinition.Format, selectedDefinition.Order, selectedDefinition.Scale, selectedDefinition.Offset);
                int code = function == 5 ? (useFunction.SelectedIndex == 0 ? 5 : 15)
                    : words.Length > 1 || useFunction.SelectedIndex == 1 ? 16 : 6;
                send.Enabled = false;
                ShowResult("Waiting for response...");
                await selectedWriter((int)unit.Value, code, (int)address.Value, words);
                if (IsDisposed) return;
                string result = unit.Value == 0 ? "Broadcast sent" : "Response ok";
                output.Text = result;
                ShowResult(result);
                if (closeOnSuccess.Checked && unit.Value != 0) Close();
            }
            catch (Exception ex)
            {
                if (!IsDisposed) { output.Text = "Error: " + ex.Message; ShowResult(ex.Message); }
            }
            finally { if (!IsDisposed) send.Enabled = true; }
        }

        public FunctionDialog(MainWindow owner, int fn) : base(TitleFor(fn), fn == 23 ? 860 : fn == 15 || fn == 16 ? 800 : fn == 22 ? 700 : 570)
        {
            host = owner;
            function = fn;
            int id = owner.Active == null ? 1 : owner.Active.State.Unit, start = owner.Active == null
                ? 0 : owner.Active.State.Address;
            unit = Number(Math.Max(fn == 17 || fn == 23 ? 1 : 0, id), fn == 17 || fn == 23 ? 1 : 0, 247);
            unit.Name = "SlaveId";
            address = Number(start, 0, 65535);
            address.Name = "WriteAddress";
            readAddress = Number(start, 0, 65535);
            readAddress.Name = "ReadAddress";
            readQuantity = Number(1, 1, 125);
            readQuantity.Name = "ReadQuantity";
            andMask.Name = "AndMask";
            orMask.Name = "OrMask";
            // Keep request values separate from the visible, function-specific layout.
            table.Controls.AddRange(new Control[] { unit, address, readAddress, readQuantity, values, andMask, orMask, output });
            output.Name = "Response";
            if (fn == 5 || fn == 6)
            {
                useFunction = Options(fn == 5 ? new[] { "05: Write single coil", "15: Write multiple coils" }
                    : new[] { "06: Write single register", "16: Write multiple registers" }, 0);
                useFunction.Name = "UseFunction";
                closeOnSuccess = new CheckBox { Name = "CloseOnSuccess" };
                table.Controls.AddRange(new Control[] { useFunction, closeOnSuccess });
            }
            if (fn == 5)
            {
                coilOn = new CheckBox { Name = "CoilOn" };
                table.Controls.Add(coilOn);
            }
            values.Name = "WriteValue";
            values.Text = "0";
            if (fn == 15 || fn == 16 || fn == 23)
            {
                quantity = Number(1, 1, fn == 15 ? 1968 : fn == 23 ? 121 : 123);
                quantity.Name = "WriteQuantity";
                // Raw words back the formatted register list and coil check list.
                writeGrid = new DataGridView { Name = "WriteValues", AllowUserToAddRows = false };
                writeGrid.Columns.Add("Address", "Address");
                if (fn == 15)
                    writeGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Value", TrueValue = "1", FalseValue = "0", ValueType = typeof(string) });
                else writeGrid.Columns.Add("Value", "Value");
                table.Controls.AddRange(new Control[] { quantity, writeGrid });
                quantity.ValueChanged += (s, e) => UpdateWriteRows();
                address.ValueChanged += (s, e) => UpdateWriteRows();
                UpdateWriteRows();
            }
            if (fn == 16 || fn == 23)
            {
                dataType = Options(ValueCodec.Formats, 0); dataType.Name = "DataType";
                byteOrder = Options(new[] { "ABCD", "CDAB", "BADC", "DCBA" }, 0); byteOrder.Name = "ByteOrder";
                table.Controls.AddRange(new Control[] { dataType, byteOrder });
            }
            if (fn == 22)
            {
                table.Controls.Add(MaskBits(andMask, "AND"));
                table.Controls.Add(MaskBits(orMask, "OR"));
            }
            if (fn == 23)
            {
                readResult = new Label { Name = "ReadResult" };
                table.Controls.Add(readResult);
            }
            send = new Button { Name = "Send", Text = "Send", UseVisualStyleBackColor = true };
            send.Click += async (s, e) =>
            {
                await Transmit();
                if (!IsDisposed && (function == 15 || function == 16 || function == 22))
                {
                    string message = output.Text.Contains("Error:") || output.Text.StartsWith("Bus busy")
                        ? output.Text : unit.Value == 0 ? "Broadcast sent" : "Response ok";
                    LocalMessageBox.Show(this, message, Text, MessageBoxButtons.OK,
                        message == "Response ok" || message == "Broadcast sent" ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                }
            };
            AcceptButton = send;
            var cancel = new Button { Name = "Cancel", Text = "Cancel", UseVisualStyleBackColor = true };
            cancel.Click += (s, e) => Close();
            CancelButton = cancel;
            BuildReferenceLayout(cancel);
        }

        Control MaskBits(TextBox source, string name)
        {
            var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true };
            var caption = new Label { Text = name + (Language.Chinese ? " 掩码位（15 到 0）" : " Mask bits (15 to 0)"), AutoSize = true };
            panel.Controls.Add(caption); panel.SetFlowBreak(caption, true);
            var bits = new CheckBox[16]; bool syncing = false;
            for (int i = 15; i >= 0; i--)
            {
                var box = new CheckBox { Name = name + "Bit" + i, Text = i.ToString(), AutoSize = true };
                bits[i] = box; panel.Controls.Add(box);
                box.CheckedChanged += (sender, args) =>
                {
                    if (syncing) return;
                    int value = 0; for (int k = 0; k < 16; k++) if (bits[k] != null && bits[k].Checked) value |= 1 << k;
                    source.Text = "0x" + value.ToString("X4");
                };
            }
            Action refresh = () =>
            {
                ushort value; try { value = ValueCodec.ParseWord(source.Text); } catch { return; }
                syncing = true; for (int i = 0; i < 16; i++) bits[i].Checked = (value & (1 << i)) != 0; syncing = false;
            };
            source.TextChanged += (sender, args) => refresh(); refresh(); return panel;
        }

        public void SaveValues(string path)
        {
            writeGrid.EndEdit();
            File.WriteAllLines(path, writeGrid.Rows.Cast<DataGridViewRow>().Select(item => "0x" + ValueCodec.ParseWord(Convert.ToString(item.Cells[1].Value)).ToString("X4")));
        }
        public void LoadValues(string path)
        {
            if (new FileInfo(path).Length > 65536) throw new IOException("Values file is too large.");
            ushort[] words = File.ReadLines(path).Where(line => !string.IsNullOrWhiteSpace(line)).Select(ValueCodec.ParseWord).ToArray();
            if (words.Length < 1 || words.Length > quantity.Maximum || address.Value + words.Length > 65536 || (function == 15 && words.Any(word => word > 1)))
                throw new ArgumentException("Invalid value count, coil value or address range.");
            quantity.Value = words.Length;
            for (int i = 0; i < words.Length; i++) writeGrid.Rows[i].Cells[1].Value = words[i].ToString();
            RefreshValueList();
        }
        void ChooseValues(bool save)
        {
            using (FileDialog dialog = save ? (FileDialog)new SaveFileDialog() : new OpenFileDialog())
            {
                dialog.Filter = Language.Chinese ? "数值文本|*.txt" : "Values text|*.txt";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try { if (save) SaveValues(dialog.FileName); else LoadValues(dialog.FileName); }
                catch (Exception ex) { LocalMessageBox.Show(this, ex.Message, "Values"); }
            }
        }
        public void EditSelectedValue(string text)
        {
            int index = registerList != null && registerList.SelectedIndex >= 0
                ? registerList.SelectedIndex * ValueCodec.Width(dataType.SelectedIndex)
                : writeGrid.CurrentCell == null ? 0 : writeGrid.CurrentCell.RowIndex;
            ushort[] words = ValueCodec.Encode(text, dataType.SelectedIndex, byteOrder.SelectedIndex, 1, 0);
            if (index + words.Length > writeGrid.Rows.Count) throw new ArgumentException("Value needs more registers; increase quantity.");
            for (int i = 0; i < words.Length; i++) writeGrid.Rows[index + i].Cells[1].Value = words[i].ToString();
            RefreshValueList();
        }
        void EditValue()
        {
            using (var dialog = new EditorDialog("Edit value", 220))
            {
                var text = new TextBox { Text = "0" };
                dialog.Field("Value (selected Type)", text);
                dialog.Buttons(() => { EditSelectedValue(text.Text); return true; }); dialog.ShowDialog(this);
            }
        }

        void UpdateWriteRows()
        {
            while (writeGrid.Rows.Count < quantity.Value) writeGrid.Rows.Add(0, "0");
            while (writeGrid.Rows.Count > quantity.Value) writeGrid.Rows.RemoveAt(writeGrid.Rows.Count - 1);
            for (int i = 0; i < writeGrid.Rows.Count; i++) writeGrid.Rows[i].Cells[0].Value = address.Value + i;
            RefreshValueList();
        }

        static string TitleFor(int fn)
        {
            switch (fn)
            {
                case 5:
                    return "Write Single Coil";
                case 6:
                    return "Write Single Register";
                case 15:
                    return "15: Write Multiple Coils";
                case 16:
                    return "16: Write Multiple Registers";
                case 17:
                    return "17: Report Slave ID";
                case 22:
                    return "22: Mask Write Register";
                case 23:
                    return "23: Read/Write Multiple Registers";
                default:
                    return "Modbus Function";
            }
        }

        public static byte[] BuildRequest(int unit, int fn, int address, ushort[] values,
             ushort andMask, ushort orMask, int readAddress, int readCount)
        {
            if (fn == 5 || fn == 6 || fn == 15 || fn == 16)
            {
                return Modbus.Request(unit, fn, address, values.Length, values);
            }

            if (unit < 0 || unit > 247 || (unit == 0 && fn != 22))
            {
                throw new ArgumentException("This function requires slave ID 1..247.");
            }

            var p = new List<byte>
            {
                (byte)unit,
                (byte)fn
            };
            if (fn == 22)
            {
                if (address < 0 || address > 65535) throw new ArgumentException("Invalid register address.");
                p.AddRange(Modbus.Words(address, andMask, orMask));
            }
            else if (fn == 23)
            {
                if (address < 0 || address > 65535 || values.Length < 1 || values.Length > 121 || address + values.Length > 65536
                    || readAddress < 0 || readCount < 1 || readCount > 125 || readAddress + readCount > 65536)
                {
                    throw new ArgumentException("Invalid read/write range.");
                }

                p.AddRange(Modbus.Words(readAddress, readCount, address, values.Length));
                p.Add((byte)(values.Length * 2));
                foreach (ushort v in values)
                {
                    p.AddRange(Modbus.Words(v));
                }
            }
            else if (fn != 17)
            {
                throw new ArgumentException("Unsupported function.");
            }

            return Modbus.AddCrc(p.ToArray());
        }

        public async Task Transmit()
        {
            if (selectedWriter != null) { await TransmitSelected(); return; }
            if (host.InFlight)
            {
                output.Text = "Bus busy. Please try again.";
                return;
            }

            try
            {
                if (!host.Bus.Connected) throw new InvalidOperationException("Connect before sending a command.");
                if (writeGrid != null) writeGrid.EndEdit();
                ushort[] words = writeGrid != null
                    ? writeGrid.Rows.Cast<DataGridViewRow>().Select(item => ValueCodec.ParseWord(Convert.ToString(item.Cells[1].Value))).ToArray()
                    : function == 5 ? new ushort[] { (ushort)(coilOn.Checked ? 1 : 0) }
                    : function == 17 || function == 22 ? new ushort[0]
                    : values.Text.Split(new[] { ',', ' ', '\r', '\n', '\t' },
                         StringSplitOptions.RemoveEmptyEntries).Select(ValueCodec.ParseWord).ToArray();
                int selectedFunction = useFunction != null && useFunction.SelectedIndex == 1 ? (function == 5 ? 15 : 16) : function;
                byte[] q = BuildRequest((int)unit.Value, selectedFunction, (int)address.Value,
                     words, ValueCodec.ParseWord(andMask.Text), ValueCodec.ParseWord(orMask.Text),
                     (int)readAddress.Value, (int)readQuantity.Value);
                host.InFlight = true;
                send.Enabled = false;
                output.Text = "TX: " + Modbus.Hex(q) + "\r\n";
                ShowResult("Waiting for response...");
                byte[] r = await host.Bus.Exchange(q, function == 17 || function == 22 || function == 23);
                if (IsDisposed)
                {
                    return;
                }

                output.AppendText("RX: " + Modbus.Hex(r) + "\r\n");
                string description = q[0] == 0 ? "Broadcast sent" : Modbus.DescribeResponse(r, q);
                output.AppendText(description);
                if (readResult != null) readResult.Text = description;
                ShowResult(q[0] == 0 ? "Broadcast sent" : "Response ok");
                if (function == 23) ShowReadValues(r);
                if (function == 17) responseView.Text = description;
                if (closeOnSuccess != null && closeOnSuccess.Checked && q[0] != 0) Close();
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                {
                    output.AppendText("\r\nError: " + ex.Message);
                    ShowResult(ex.Message);
                }
            }
            finally
            {
                host.InFlight = false;
                if (!IsDisposed)
                {
                    send.Enabled = true;
                }
            }
        }
    }
}

