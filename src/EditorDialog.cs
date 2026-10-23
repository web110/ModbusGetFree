using System;
using System.Drawing;
using System.Windows.Forms;

namespace ModbusGetFree
{
    /// <summary>
    /// 通用参数对话框的布局辅助基类。
    /// </summary>
    public class EditorDialog : Form
    {
        protected TableLayoutPanel table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(16),
            AutoScroll = true
        };
        protected int row;

        public EditorDialog(string title, int height)
        {
            Icon = AppBrand.LoadIcon();
            Text = title;
            Size = new Size(480, height);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            Font = UiFonts.Create();
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            Controls.Add(table);
        }

        public void Field(string title, Control control)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            control.Dock = DockStyle.Fill;
            control.MaximumSize = new Size(0, 32);
            table.Controls.Add(new Label { Text = title, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            table.Controls.Add(control, 1, row++);
        }

        public void AdvanceRow()
        {
            row++;
        }

        public void Note(string text)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            var l = new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                ForeColor = SystemColors.GrayText
            };
            table.Controls.Add(l, 0, row++);
            table.SetColumnSpan(l, 2);
        }

        public void Buttons(Func<bool> accept)
        {
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };
            var ok = new Button
            {
                Text = "OK",
                Width = 85
            };
            var cancel = new Button
            {
                Text = "Cancel",
                Width = 85,
                DialogResult = DialogResult.Cancel
            };
            ok.Click += (s, e) =>
            {
                try
                {
                    if (accept())
                    {
                        DialogResult = DialogResult.OK;
                        Close();
                    }
                }
                catch (Exception ex)
                {
                    LocalMessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

            };
            panel.Controls.Add(cancel);
            panel.Controls.Add(ok);
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            table.Controls.Add(panel, 0, row++);
            table.SetColumnSpan(panel, 2);
            AcceptButton = ok;
            CancelButton = cancel;
        }

        public static NumericUpDown Number(int value, int min, int max)
        {
            return new NumericUpDown
            {
                Minimum = min,
                Maximum = max,
                Value = value
            };
        }

        public static ComboBox Options(string[] values, int selected)
        {
            var c = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            c.Items.AddRange(values);
            c.SelectedIndex = selected;
            return c;
        }
    }
}
