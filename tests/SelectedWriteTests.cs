using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ModbusGetFree;

class SelectedWriteTests
{
    static int count;
    static T Find<T>(Control root, string name) where T : Control { return (T)root.Controls.Find(name, true).Single(); }
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); count++; Console.WriteLine("PASS " + name); }
    [STAThread] static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        var host = new MainWindow(true);
        host.Shown += async (s, e) =>
        {
            try { await Run(host, args[0]); Console.WriteLine("Checks: " + count + "; Exit: 0"); Environment.Exit(0); }
            catch (Exception ex) { Console.WriteLine(ex); Environment.Exit(1); }
        };
        Application.Run(host);
    }
    static async Task Run(MainWindow host, string output)
    {
        host.Settings.Demo = true; await host.QuickConnect();
        foreach (int format in new[] { 0, 4, 5, 6, 7, 8, 9 })
        foreach (int order in format == 0 ? new[] { 0 } : new[] { 0, 1, 2, 3 })
        {
            int width = ValueCodec.Width(format);
            var state = new DocumentState { Unit = 4, Address = 23, Quantity = 8, Disabled = true };
            state.Definitions.Add(new RegisterDefinition { Address = 25, Format = format, Order = order });
            var doc = host.NewDocument(state);
            ushort[] initial = ValueCodec.Encode("12", format, order, 1, 0);
            Array.Copy(initial, 0, state.Values, 2, initial.Length);
            using (var dialog = doc.CreateWriteDialog(2 + width - 1))
            {
                dialog.Show(host);
                Check(Find<NumericUpDown>(dialog, "SlaveId").Value == 4 && Find<NumericUpDown>(dialog, "WriteAddress").Value == 25, "group start address and slave " + format + "/" + order);
                Check(Find<TextBox>(dialog, "WriteValue").Text == "12", "current typed value " + format + "/" + order);
                Check(!Find<CheckBox>(dialog, "CloseOnSuccess").Checked, "default keeps dialog open " + format + "/" + order);
                Check(Find<RadioButton>(dialog, "UseSingleFunction").Visible == (width == 1), "function options only for single register " + format + "/" + order);
                Find<TextBox>(dialog, "WriteValue").Text = format == 6 || format == 9 ? "1.25" : "123";
                if (width == 1) Find<RadioButton>(dialog, "UseMultipleFunction").Checked = true;
                await dialog.Transmit();
                string expected = format == 6 || format == 9 ? "1.25" : "123";
                Check(ValueCodec.Format(state.Values, 2, format, order, 1, 0) == expected && dialog.ResultText == "Response ok", "typed write preserves order " + format + "/" + order);
                if (order == 0 && (format == 0 || format == 6))
                    using (var bmp = new Bitmap(dialog.Width, dialog.Height))
                    { dialog.DrawToBitmap(bmp, new Rectangle(Point.Empty, bmp.Size)); bmp.Save(Path.Combine(output, "selected-" + format + ".png")); }
                dialog.Close();
            }
        }
        var scaledState = new DocumentState { Disabled = true, Quantity = 2, Scale = 0.1, Offset = 2 };
        var scaled = host.NewDocument(scaledState);
        using (var dialog = scaled.CreateWriteDialog(0))
        {
            dialog.Show(host); Find<TextBox>(dialog, "WriteValue").Text = "12";
            await dialog.Transmit(); Check(scaledState.Values[0] == 100, "inverse scaling on write"); dialog.Close();
        }
        await host.Bus.Close();
    }
}
