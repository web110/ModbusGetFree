using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ModbusGetFree
{
    public partial class FunctionDialog
    {
        CheckedListBox coilList;
        ListBox registerList;
        ListBox readValues;
        ComboBox referenceType;
        Label resultLabel;
        TextBox responseView;
        GroupBox singleFunctionGroup;
        bool refreshingValues;

        // Coordinates are logical pixels at 96 DPI. WinForms scales the complete dialog.
        void Place(Control parent, Control control, int x, int y, int width, int height)
        {
            control.Dock = DockStyle.None;
            control.MaximumSize = Size.Empty;
            control.AutoSize = false;
            parent.Controls.Add(control);
            control.SetBounds(x, y, width, height);
        }

        void Caption(Control parent, string text, int x, int y, int width)
        {
            Place(parent, new Label { Text = text, TextAlign = ContentAlignment.MiddleLeft }, x, y, width, 23);
        }

        void Entry(string label, NumericUpDown control, int x, int y, int labelWidth)
        {
            Caption(this, label, x, y, labelWidth);
            Place(this, control, x + labelWidth, y, 82, 23);
            // The original uses plain numeric edit fields, without spinner buttons.
            control.Controls[0].Visible = false;
            control.BackColor = SystemColors.Window;
            control.Controls[1].Bounds = new Rectangle(1, 1, control.ClientSize.Width - 2, control.ClientSize.Height - 2);
        }

        GroupBox Group(string title, int x, int y, int width, int height)
        {
            var group = new GroupBox { Text = title };
            Place(this, group, x, y, width, height);
            return group;
        }

        Button ActionButton(string name, string text, int x, int y, EventHandler action)
        {
            var button = new Button { Name = name, Text = text, UseVisualStyleBackColor = true };
            button.Click += action;
            Place(this, button, x, y, 82, 26);
            return button;
        }

        void BuildReferenceLayout(Button cancel)
        {
            SuspendLayout();
            table.Visible = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            BackColor = SystemColors.Control;
            ShowIcon = false;
            if (function == 5 || function == 6)
            {
                ClientSize = new Size(340, 350);
                Entry("Slave ID:", unit, 14, 20, 105);
                Entry("Address:", address, 14, 62, 105);
                Place(this, send, 238, 20, 102, 32);
                send.Left = 224;
                Place(this, cancel, 224, 64, 102, 32);
                if (function == 5)
                {
                    var valueGroup = Group("Value", 14, 101, 156, 48);
                    var on = new RadioButton { Name = "ValueOn", Text = "On" };
                    var off = new RadioButton { Name = "ValueOff", Text = "Off", Checked = true };
                    Place(valueGroup, on, 15, 20, 62, 23);
                    Place(valueGroup, off, 88, 20, 62, 23);
                    on.CheckedChanged += (s, e) => coilOn.Checked = on.Checked;
                    coilOn.CheckedChanged += (s, e) => { on.Checked = coilOn.Checked; off.Checked = !coilOn.Checked; };
                }
                else
                {
                    Caption(this, "Value:", 14, 104, 105);
                    Place(this, values, 119, 104, 82, 23);
                }
                var result = Group("Result", 14, 162, 312, 88);
                resultLabel = new Label { Name = "ResultStatus", Text = "N/A" };
                Place(result, resultLabel, 10, 22, 290, 24);
                closeOnSuccess.Text = "Close dialog on \"Response ok\"";
                Place(result, closeOnSuccess, 10, 53, 292, 24);
                var use = Group("Use Function", 14, 270, 312, 72);
                singleFunctionGroup = use;
                var first = new RadioButton { Name = "UseSingleFunction", Text = function == 5 ? "05: Write single coil" : "06: Write single register", Checked = true };
                var second = new RadioButton { Name = "UseMultipleFunction", Text = function == 5 ? "15: Write multiple coils" : "16: Write multiple registers" };
                Place(use, first, 10, 20, 290, 23);
                Place(use, second, 10, 45, 290, 23);
                second.CheckedChanged += (s, e) => useFunction.SelectedIndex = second.Checked ? 1 : 0;
                useFunction.SelectedIndexChanged += (s, e) => { first.Checked = useFunction.SelectedIndex == 0; second.Checked = !first.Checked; };
            }
            else if (function == 17)
            {
                ClientSize = new Size(360, 155);
                Entry("Slave ID:", unit, 12, 14, 65);
                Place(this, send, 172, 14, 82, 26);
                Place(this, cancel, 270, 14, 78, 26);
                Caption(this, "Response", 12, 46, 150);
                responseView = new TextBox { Name = "SlaveResponse", Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = SystemColors.Control };
                Place(this, responseView, 12, 70, 336, 75);
            }
            else if (function == 22)
            {
                ClientSize = new Size(310, 205);
                Entry("Slave ID:", unit, 12, 14, 65);
                Entry("Address:", address, 12, 48, 65);
                Place(this, send, 216, 14, 82, 26);
                Place(this, cancel, 216, 48, 82, 26);
                ReferenceMask(andMask, "AND", 86);
                ReferenceMask(orMask, "OR", 150);
            }
            else
            {
                bool coils = function == 15;
                bool readWrite = function == 23;
                ClientSize = new Size(coils ? 480 : 600, readWrite ? 355 : coils ? 390 : 300);
                int inputX = readWrite ? 116 : 76;
                Entry("Slave ID:", unit, 12, 14, inputX - 12);
                Entry(readWrite ? "Write Address:" : "Address:", address, 12, 48, inputX - 12);
                Entry(readWrite ? "Write Quantity:" : "Quantity:", quantity, 12, 82, inputX - 12);
                int buttonX = ClientSize.Width - 94;
                int listX = coils ? 202 : 278;
                int listWidth = buttonX - listX - 14;
                Place(this, send, buttonX, 14, 82, 26);
                Place(this, cancel, buttonX, 48, 82, 26);
                if (coils)
                {
                    coilList = new CheckedListBox { Name = "CoilValues", CheckOnClick = true, IntegralHeight = false };
                    Place(this, coilList, listX, 14, listWidth, 362);
                    coilList.ItemCheck += (s, e) =>
                    {
                        if (!refreshingValues) writeGrid.Rows[e.Index].Cells[1].Value = e.NewValue == CheckState.Checked ? "1" : "0";
                    };
                }
                else
                {
                    Caption(this, "Type:", 12, 116, 64);
                    referenceType = new ComboBox { Name = "RegisterType", DropDownStyle = ComboBoxStyle.DropDownList, DropDownWidth = 270, MaxDropDownItems = 28 };
                    referenceType.Items.AddRange(new[] { "Signed", "Unsigned", "Hex", "Binary" });
                    foreach (string kind in new[] { "Int32", "UInt32", "Int64", "UInt64", "Float", "Double" })
                        foreach (string order in new[] { "Big-endian", "Little-endian", "Big-endian byte swap", "Little-endian byte swap" })
                            referenceType.Items.Add(kind + " " + order);
                    Place(this, referenceType, 76, 116, 192, 24);
                    referenceType.SelectedIndex = 0;
                    referenceType.SelectedIndexChanged += (s, e) =>
                    {
                        int index = referenceType.SelectedIndex;
                        dataType.SelectedIndex = index < 4 ? index : new[] { 4, 5, 7, 8, 6, 9 }[(index - 4) / 4];
                        byteOrder.SelectedIndex = index < 4 ? 0 : (index - 4) % 4;
                        RefreshValueList();
                    };
                    dataType.SelectedIndexChanged += (s, e) => RefreshValueList();
                    byteOrder.SelectedIndexChanged += (s, e) => RefreshValueList();
                    registerList = new ListBox { Name = "RegisterValues", IntegralHeight = false };
                    Place(this, registerList, listX, 14, listWidth, readWrite ? 132 : 274);
                    registerList.DoubleClick += (s, e) => EditValue();
                    ActionButton("EditRegister", "Edit", buttonX, 82, (s, e) => EditValue());
                    if (readWrite)
                    {
                        Entry("Read Address:", readAddress, 12, 198, inputX - 12);
                        Entry("Read Quantity:", readQuantity, 12, 232, inputX - 12);
                        Caption(this, "Read Result", listX, 180, listWidth);
                        readValues = new ListBox { Name = "ReadValues", IntegralHeight = false };
                        Place(this, readValues, listX, 207, listWidth, 132);
                    }
                }
                ActionButton("OpenValuesFile", "&Open", buttonX, coils ? 142 : 116, (s, e) => ChooseValues(false));
                ActionButton("SaveValuesFile", "&Save", buttonX, coils ? 184 : 150, (s, e) => ChooseValues(true));
                int activeQuantity = host.Active == null ? 1 : host.Active.State.Quantity;
                quantity.Value = Math.Max(1, Math.Min((int)quantity.Maximum, activeQuantity));
                RefreshValueList();
            }
            ActiveControl = unit;
            Shown += (s, e) => unit.Select(0, unit.Text.Length);
            ResumeLayout(false);
        }

        void ReferenceMask(TextBox source, string name, int y)
        {
            var group = Group(name + " Mask", 12, y, 286, 44);
            for (int bit = 15; bit >= 0; bit--)
            {
                var box = (CheckBox)table.Controls.Find(name + "Bit" + bit, true).Single();
                int column = 15 - bit;
                box.Text = "";
                box.AccessibleName = name + (" " + Language.Text("Mask bit") + " ") + bit;
                Place(group, box, 7 + column * 16 + (column / 4) * 6, 19, 16, 18);
            }
        }

        void RefreshValueList()
        {
            if (writeGrid == null || refreshingValues) return;
            refreshingValues = true;
            try
            {
                if (coilList != null)
                {
                    coilList.BeginUpdate(); coilList.Items.Clear();
                    foreach (DataGridViewRow item in writeGrid.Rows)
                        coilList.Items.Add((Language.Text("Coil") + " ") + item.Cells[0].Value, Convert.ToString(item.Cells[1].Value) == "1");
                    coilList.EndUpdate();
                }
                if (registerList != null)
                {
                    int selected = Math.Max(0, registerList.SelectedIndex);
                    var words = writeGrid.Rows.Cast<DataGridViewRow>().Select(item => ValueCodec.ParseWord(Convert.ToString(item.Cells[1].Value))).ToArray();
                    registerList.BeginUpdate(); registerList.Items.Clear();
                    int width = ValueCodec.Width(dataType.SelectedIndex);
                    for (int i = 0; i < words.Length; i += width)
                        registerList.Items.Add(((int)address.Value + i).ToString("D3") + " = " + ValueCodec.Display(words, i, dataType.SelectedIndex, byteOrder.SelectedIndex, 1, 0));
                    if (registerList.Items.Count > 0) registerList.SelectedIndex = Math.Min(selected, registerList.Items.Count - 1);
                    registerList.EndUpdate();
                }
            }
            finally { refreshingValues = false; }
        }

        void ShowResult(string text)
        {
            if (resultLabel != null) resultLabel.Text = Language.Text(text);
            else if (responseView != null) responseView.Text = Language.Text(text);
            else if (readValues != null && text != "Response ok") { readValues.Items.Clear(); readValues.Items.Add(Language.Text(text)); }
        }

        void ShowReadValues(byte[] response)
        {
            var words = new ushort[(int)readQuantity.Value];
            for (int i = 0; i < words.Length; i++) words[i] = (ushort)((response[3 + i * 2] << 8) | response[4 + i * 2]);
            readValues.Items.Clear();
            for (int i = 0; i < words.Length; i += ValueCodec.Width(dataType.SelectedIndex))
                readValues.Items.Add(((int)readAddress.Value + i).ToString("D3") + " = " + ValueCodec.Display(words, i, dataType.SelectedIndex, byteOrder.SelectedIndex, 1, 0));
        }
    }
}
