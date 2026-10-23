using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ModbusGetFree
{
    /// <summary>
    /// classic 风格读写定义窗口：编辑参数、应用配置、单次通信以及请求预览。
    /// </summary>
    public class DefinitionDialog : Form
    {
        public static readonly int[] Codes =
        {
            1,
            2,
            3,
            4,
            5,
            6,
            15,
            16
        };
        public static readonly string[] Functions =
        {
            "01 Read Coils (0x)",
            "02 Read Discrete Inputs (1x)",
            "03 Read Holding Registers (4x)",
            "04 Read Input Registers (3x)",
            "05 Write Single Coil",
            "06 Write Single Register",
            "15 Write Multiple Coils",
            "16 Write Multiple Registers"
        };
        readonly DocumentState state;
        readonly Action changed;
        readonly Func<Task> once;
        readonly Func<bool> canSend;
        TextBox slave = new TextBox();
        TextBox address = new TextBox();
        TextBox quantity = new TextBox();
        TextBox rate = new TextBox();
        TextBox rtu = new TextBox();
        TextBox ascii = new TextBox();
        ComboBox function;
        Label plc = new Label();
        CheckBox disabled = new CheckBox();
        CheckBox onError = new CheckBox();
        CheckBox hide = new CheckBox();
        CheckBox inCell = new CheckBox();
        CheckBox baseOne = new CheckBox();
        CheckBox enron = new CheckBox();
        RadioButton[] rows;
        Button send = new Button();

        public DefinitionDialog(DocumentState state) : this(state, null, null, null)
        {
        }

        public DefinitionDialog(DocumentState s, Action applied, Func<Task> sendOnce, Func<bool> available)
        {
            Icon = AppBrand.LoadIcon();
            state = s;
            changed = applied;
            once = sendOnce;
            canSend = available;
            Text = "Read/Write Definition";
            Font = UiFonts.Create();
            ClientSize = new Size(560, 700);
            AutoScaleMode = AutoScaleMode.Font;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            slave.Name = "SlaveID";
            address.Name = "Address";
            quantity.Name = "Quantity";
            rate.Name = "ScanRate";
            enron.Name = "Enron";
            rtu.Name = "RTUPreview";
            ascii.Name = "ASCIIPreview";
            function = EditorDialog.Options(Functions, Array.IndexOf(Codes, s.Function));
            slave.Text = s.Unit.ToString();
            address.Text = s.Address.ToString();
            quantity.Text = s.Quantity.ToString();
            rate.Text = s.ScanRate.ToString();
            Field("Slave ID:", slave, 28, 95);
            Field("Function:", function, 76, 280);
            Field("Address:", address, 124, 95);
            plc.SetBounds(210, 125, 225, 26);
            Controls.Add(plc);
            Field("Quantity:", quantity, 172, 95);
            Field("Scan Rate:", rate, 220, 95);
            var ms = new Label
            {
                Text = "[ms]",
                AutoSize = true,
                Location = new Point(210, 222)
            };
            Controls.Add(ms);
            var ok = Button("OK", 435, 28, () =>
            {
                if (ApplyState())
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }

            });
            var cancel = Button("Cancel", 435, 76, () =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            });
            Button("Apply", 435, 220, () => ApplyState());
            AcceptButton = ok;
            CancelButton = cancel;
            var disableGroup = Group("Disable", 12, 268, 300, 100);
            Check(disableGroup, disabled, "Read/Write Disabled", 16, 26, s.Disabled);
            Check(disableGroup, onError, "Disable on error", 16, 60, s.DisableOnError);
            send.Text = "Read/Write Once";
            send.SetBounds(385, 320, 160, 34);
            send.Enabled = once != null && canSend != null && canSend();
            send.Click += async (sender, e) =>
            {
                if (once == null || !ApplyState())
                {
                    return;
                }

                send.Enabled = false;
                try
                {
                    await once();
                }
                finally
                {
                    send.Enabled = canSend != null && canSend();
                }

            };
            Controls.Add(send);
            var view = Group("View", 12, 382, 535, 165);
            var rowGroup = new GroupBox
            {
                Text = "Rows",
                Bounds = new Rectangle(16, 23, 501, 60)
            };
            view.Controls.Add(rowGroup);
            int[] counts =
            {
                10,
                20,
                50,
                100,
                0
            };
            string[] titles =
            {
                "10",
                "20",
                "50",
                "100",
                "Fit to Quantity"
            };
            rows = new RadioButton[5];
            int[] xs =
            {
                14,
                78,
                143,
                208,
                283
            };
            for (int i = 0; i < 5; i++)
            {
                rows[i] = new RadioButton
                {
                    Text = titles[i],
                    AutoSize = true,
                    Location = new Point(xs[i], 24),
                    Checked = s.Rows == counts[i],
                    Tag = counts[i],
                    Name = "Rows" + counts[i]
                };
                rowGroup.Controls.Add(rows[i]);
            }

            Check(view, hide, "Hide Alias Columns", 18, 95, s.HideNames);
            Check(view, inCell, "Address in Cell", 18, 128, s.AddressInCell);
            Check(view, baseOne, "PLC Addresses (Base 1)", 270, 95, s.BaseOne);
            Check(view, enron, "Enron/Daniel Mode", 270, 128, s.Enron);
            var request = Group("Request", 12, 561, 535, 125);
            request.Controls.Add(new Label { Text = "RTU", Location = new Point(12, 31), AutoSize = true });
            request.Controls.Add(new Label { Text = "ASCII", Location = new Point(12, 78), AutoSize = true });
            rtu.SetBounds(72, 26, 448, 30);
            ascii.SetBounds(72, 73, 448, 30);
            foreach (var box in new[]
            {
                rtu,
                ascii
            }

            )
            {
                box.ReadOnly = true;
                box.Font = new Font("Courier New", 9);
                box.WordWrap = false;
            }

            request.Controls.Add(rtu);
            request.Controls.Add(ascii);
            foreach (var box in new[]
            {
                slave,
                address,
                quantity
            }

            )
            {
                box.TextChanged += (sender, e) => Preview();
            }

            function.SelectedIndexChanged += (sender, e) => Preview();
            enron.CheckedChanged += (sender, e) => Preview();
            Preview();
        }

        void Field(string text, Control input, int y, int width)
        {
            Controls.Add(new Label { Text = text, AutoSize = true, Location = new Point(12, y + 3) });
            input.SetBounds(105, y, width, 30);
            Controls.Add(input);
        }

        GroupBox Group(string title, int x, int y, int w, int h)
        {
            var g = new GroupBox
            {
                Text = title,
                Bounds = new Rectangle(x, y, w, h)
            };
            Controls.Add(g);
            return g;
        }

        void Check(Control parent, CheckBox box, string title, int x, int y, bool value)
        {
            box.Text = title;
            box.AutoSize = true;
            box.Location = new Point(x, y);
            box.Checked = value;
            parent.Controls.Add(box);
        }

        Button Button(string text, int x, int y, Action action)
        {
            var b = new Button
            {
                Text = text,
                Bounds = new Rectangle(x, y, 110, 35)
            };
            b.Click += (sender, e) => action();
            Controls.Add(b);
            return b;
        }

        public static string PlcAddress(int fn, int address)
        {
            int p = fn == 1 || fn == 5 || fn == 15 ? 0 : fn == 2 ? 1 : fn == 4 ? 3 : 4;
            int digits = address < 9999 ? 10000 : 100000;
            return (p * digits + address + 1).ToString(p == 0 ? (digits == 10000 ? "D5" : "D6") : "0");
        }

        public static string AsciiPreview(byte[] rtu)
        {
            byte sum = 0;
            var content = rtu.Take(rtu.Length - 2).ToArray();
            foreach (byte b in content)
            {
                sum += b;
            }

            string frame = ":" + string.Concat(content.Select(v => v.ToString("X2")))
                + unchecked((byte)-sum).ToString("X2") + "\r\n";
            return Modbus.Hex(System.Text.Encoding.ASCII.GetBytes(frame));
        }

        byte[] Build()
        {
            int unit = int.Parse(slave.Text), fn = Codes[function.SelectedIndex],
                 a = int.Parse(address.Text), n = int.Parse(quantity.Text);
            bool active = enron.Checked && (fn == 3 || fn == 4 || fn == 16)
                && ((a >= 5000 && a < 6000) || (a >= 7000 && a < 8000));
            ushort[] values = fn > 4 ? new ushort[n * (active ? 2 : 1)] : null;
            if (fn > 4)
            {
                Array.Copy(state.Values, values, Math.Min(values.Length, state.Values.Length));
            }

            return active ? Modbus.EnronRequest(unit, fn, a, n, values) : Modbus.Request(unit, fn, a, n, values);
        }

        void Preview()
        {
            try
            {
                byte[] bytes = Build();
                rtu.Text = Modbus.Hex(bytes);
                ascii.Text = AsciiPreview(bytes);
                plc.Text = (Language.Chinese ? "PLC 地址 = " : "PLC address = ") + PlcAddress(Codes[function.SelectedIndex], int.Parse(address.Text));
            }
            catch
            {
                rtu.Text = "Invalid definition";
                ascii.Text = "";
                plc.Text = "";
            }
        }

        public bool ApplyState()
        {
            try
            {
                Build();
                int scan = int.Parse(rate.Text);
                if (scan < 0 || scan > 3600000)
                {
                    throw new ArgumentException("Scan Rate must be 0..3600000 ms.");
                }

                state.Unit = int.Parse(slave.Text);
                state.Function = Codes[function.SelectedIndex];
                state.Address = int.Parse(address.Text);
                state.Quantity = int.Parse(quantity.Text);
                state.ScanRate = scan;
                state.Rows = (int)rows.First(r => r.Checked).Tag;
                state.Disabled = disabled.Checked;
                state.DisableOnError = onError.Checked;
                state.HideNames = hide.Checked;
                state.AddressInCell = inCell.Checked;
                state.BaseOne = baseOne.Checked;
                state.Enron = enron.Checked;
                if (state.Enron && ((state.Address >= 5000 && state.Address < 6000)
                    || (state.Address >= 7000 && state.Address < 8000)) && (state.Function == 3
                    || state.Function == 4 || state.Function == 16))
                {
                    state.Format = state.Address < 6000 ? 4 : 6;
                }

                if (changed != null)
                {
                    changed();
                }

                Preview();
                return true;
            }
            catch (Exception ex)
            {
                LocalMessageBox.Show(this, ex.Message, "Read/Write Definition", MessageBoxButtons.OK,
                     MessageBoxIcon.Warning);
                return false;
            }
        }
    }
}
