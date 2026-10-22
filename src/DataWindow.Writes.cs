using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ModbusGetFree
{
    /// <summary>
    /// 双击写入、格式化值写入和批量写入；实际收发统一交给主窗口的共享 Bus。
    /// </summary>
    public partial class DataWindow : Form
    {
        public void EditSelected()
        {
            if (Grid.CurrentCell == null)
            {
                return;
            }

            EditValue(Index(Grid.CurrentCell.RowIndex, Grid.CurrentCell.ColumnIndex));
        }

        void EditValue(int index)
        {
            if (index < 0 || index >= StorageQuantity || Busy || host.InFlight)
            {
                return;
            }

            if (State.Function == 2 || State.Function == 4)
            {
                MessageBox.Show(this, "Discrete inputs and input registers are read-only.");
                return;
            }

            index = GroupStart(index);
            var definition = DefinitionAt(index).Copy();
            int width = GroupWidth(definition);
            if (index + width > StorageQuantity)
            {
                MessageBox.Show(this, "Incomplete value: increase register quantity.");
                return;
            }

            using (var dialog = CreateWriteDialog(index)) dialog.ShowDialog(this);
        }

        public FunctionDialog CreateWriteDialog(int index)
        {
            if (State.Function == 2 || State.Function == 4)
                throw new InvalidOperationException("This data area is read-only.");
            if (index < 0 || index >= StorageQuantity) throw new ArgumentOutOfRangeException("index");
            index = GroupStart(index);
            var definition = DefinitionAt(index).Copy();
            if (index + GroupWidth(definition) > StorageQuantity)
                throw new ArgumentException("Incomplete register group.");
            var dialog = new FunctionDialog(host, IsBitArea ? 5 : 6);
            dialog.ConfigureSelectedWrite(State.Unit, State.Address + index / RegisterStride,
                FormatGroup(index), definition, async (slave, code, start, words) =>
                {
                    await WriteWords(code, start, words, slave);
                    if (slave != State.Unit) return;
                    int target = (start - State.Address) * RegisterStride;
                    for (int i = 0; i < words.Length; i++)
                        if (target + i >= 0 && target + i < StorageQuantity)
                            State.Values[target + i] = words[i];
                    host.MarkDirty();
                    RefreshValues();
                });
            return dialog;
        }

        public async Task WriteGroup(int index, string text)
        {
            if (State.Function == 2 || State.Function == 4)
            {
                throw new InvalidOperationException("This data area is read-only.");
            }

            index = GroupStart(index);
            ushort[] words = EncodeGroup(index, text);
            if (index < 0 || index + words.Length > StorageQuantity)
            {
                throw new ArgumentException("Incomplete register group.");
            }

            int function = IsBitArea ? 5 : words.Length == 1 ? 6 : 16;
            await WriteWords(function, State.Address + index / RegisterStride, words);
            Array.Copy(words, 0, State.Values, index, words.Length);
            RefreshValues();
            host.MarkDirty();
        }

        public async Task WriteWords(int fn, int address, ushort[] words, int? slaveOverride = null)
        {
            if (host.InFlight)
            {
                throw new IOException("Bus busy.");
            }

            if (!host.Bus.Connected)
            {
                throw new IOException("Not connected. Press F3.");
            }

            int slave = slaveOverride ?? State.Unit;
            Busy = true;
            host.InFlight = true;
            try
            {
                byte[] q = EnronActive ? Modbus.EnronRequest(slave, fn,
                     address, words.Length / 2, words) : Modbus.Request(slave, fn, address,
                     words.Length, words);
                Tx++;
                byte[] r = await host.Bus.Exchange(q, false, EnronActive);
                if (slave != 0)
                {
                    Modbus.Decode(r, q, EnronActive ? words.Length / 2 : words.Length);
                }

                lastMessage = slave == 0 ? "Broadcast sent" : "Response OK";
            }
            catch (Exception ex)
            {
                Errors++;
                lastMessage = "Error: " + ex.Message;
                throw;
            }
            finally
            {
                Busy = false;
                host.InFlight = false;
                NextPoll = DateTime.UtcNow.AddMilliseconds(State.ScanRate);
                UpdateSummary();
            }
        }

        public void WriteMultiple()
        {
            if (Busy || host.InFlight)
            {
                return;
            }

            bool coil = State.Function == 1 || State.Function == 5 || State.Function == 15;
            if (State.Function == 2 || State.Function == 4)
            {
                MessageBox.Show(this, "This data area is read-only.");
                return;
            }

            using (var d = new EditorDialog(coil ? "Write Multiple Coils (15)" : "Write Multiple Registers (16)", 370))
            {
                var start = EditorDialog.Number(State.Address, 0, 65535);
                var text = new TextBox
                {
                    Multiline = true,
                    ScrollBars = ScrollBars.Vertical,
                    Text = string.Join(", ", State.Values.Take(coil ? 1968 : 123).Select(v => v.ToString()).ToArray())
                };
                d.Field("Address (base 0)", start);
                d.Note("Raw 16-bit register values, comma / space separated. Hex values may use 0x. Coil values: 0 or 1.");
                d.tableHeight(text, 150);
                var status = new Label
                {
                    Text = "Ready"
                };
                d.Field("Status", status);
                var send = new Button
                {
                    Text = "Send",
                    Dock = DockStyle.Fill
                };
                d.Field("", send);
                send.Click += async (s, e) =>
                {
                    if (host.InFlight)
                    {
                        return;
                    }

                    try
                    {
                        var words = text.Text.Split(new[] { ',', ' ', '\r', '\n',
                             '\t' }, StringSplitOptions.RemoveEmptyEntries).Select(ValueCodec.ParseWord).ToArray();
                        send.Enabled = false;
                        await WriteWords(coil ? 15 : 16, (int)start.Value, words);
                        for (int i = 0; i < words.Length; i++)
                        {
                            int target = ((int)start.Value - State.Address) * (EnronActive ? 2 : 1) + i;
                            if (target >= 0 && target < State.Values.Length)
                            {
                                State.Values[target] = words[i];
                            }
                        }

                        RefreshValues();
                        host.MarkDirty();
                        status.Text = "Response OK";
                    }
                    catch (Exception ex)
                    {
                        status.Text = ex.Message;
                    }
                    finally
                    {
                        if (!send.IsDisposed)
                        {
                            send.Enabled = true;
                        }
                    }

                };
                d.ShowDialog(this);
            }
        }
    }
}
