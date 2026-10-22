using ModbusGetFree;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Serialization;
class RtuTests
{
    static int checks; static List<string> lines = new List<string>(); static string output;
    static void Assert(bool condition, string name)
    {
        checks++;
        if (!condition)
            throw new Exception("FAIL: " + name);
        lines.Add("PASS: " + name);
    }
    static void Reject(Action action, string name)
    {
        bool rejected = false;
        try
        {
            action();
        }
        catch { rejected = true; }
        Assert(rejected, name);
    }
    static byte[] Reply(params byte[] b)
    {
        return Modbus.AddCrc(b);
    }
    [STAThread]
    static void Main(string[] args)
    {
        output = args.Length > 0 ? args[0] : Environment.CurrentDirectory;
        Directory.CreateDirectory(output);
        try
        {
            ProtocolTests();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var host = new MainWindow(true);
            host.Shown += async (s, e) => { try { await Integration(host); Finish(0); } catch (Exception ex) { lines.Add(ex.ToString()); Finish(1); } };
            Application.Run(host);
        }
        catch (Exception ex) { lines.Add(ex.ToString()); Finish(1); }
    }
    static void Finish(int code)
    {
        lines.Add("Checks: " + checks + "; Exit: " + code);
        File.WriteAllLines(Path.Combine(output, "verification.txt"), lines);
        Console.WriteLine(string.Join(Environment.NewLine, lines.ToArray()));
        Environment.Exit(code);
    }
    static void ProtocolTests()
    {
        byte[] q = Modbus.Request(1, 3, 0, 10, null);
        Assert(Modbus.Hex(q) == "01 03 00 00 00 0A C5 CD", "standard FC03 CRC vector");
        Assert(Modbus.Hex(Modbus.Request(1, 6, 1, 1, new ushort[] { 3 })) == "01 06 00 01 00 03 98 0B",
             "standard FC06 vector");
        ushort[] values = Modbus.Decode(Reply(1, 3, 4, 0, 42, 255, 255), Modbus.Request(1, 3, 0, 2, null), 2);
        Assert(values.SequenceEqual(new ushort[] { 42, 65535 }), "register decode");
        values = Modbus.Decode(Reply(1, 1, 2, 129, 1), Modbus.Request(1, 1, 0, 9, null), 9);
        Assert(values[0] == 1 && values[7] == 1 && values[8] == 1 && values[1] == 0, "coil cross-byte unpacking");
        byte[] packed = Modbus.Request(1, 15, 0, 9, new ushort[] { 1, 0, 0, 0, 0, 0, 0, 1, 1 });
        Assert(packed[6] == 2 && packed[7] == 129 && packed[8] == 1, "FC15 bit packing");
        foreach (int fn in new[] { 5, 6, 15, 16 })
        {
            ushort[] v = fn == 15 || fn == 16 ? new ushort[] { 1, 0, 1 } : new ushort[] { 1 };
            q = Modbus.Request(1, fn, 9, v.Length, v);
            Assert(Modbus.Decode(Modbus.AddCrc(q.Take(6).ToArray()), q, v.Length).Length == 0, "write echo FC" + fn);
        }
        q = Modbus.Request(1, 3, 0, 2, null);
        var broken = Reply(1, 3, 4, 0, 42, 0, 43);
        broken[3] ^= 1;
        Reject(() => Modbus.Decode(broken, q, 2), "reject CRC corruption");
        Reject(() => Modbus.Decode(Reply(2, 3, 4, 0, 42, 0, 43), q, 2), "reject wrong slave");
        Reject(() => Modbus.Decode(Reply(1, 4, 4, 0, 42, 0, 43), q, 2), "reject wrong function");
        Reject(() => Modbus.Decode(Reply(1, 131, 2), q, 2), "decode Modbus exception");
        Reject(() => Modbus.Decode(Reply(1, 3, 2, 0, 42), q, 2), "reject byte count mismatch");
        Reject(() => Modbus.Request(1, 3, 0, 126, null), "FC03 maximum 125");
        Reject(() => Modbus.Request(1, 1, 0, 2001, null), "FC01 maximum 2000");
        Reject(() => Modbus.Request(1, 16, 0, 124, new ushort[124]), "FC16 maximum 123");
        Reject(() => Modbus.Request(1, 15, 0, 1969, new ushort[1969]), "FC15 maximum 1968");
        Reject(() => Modbus.Request(1, 3, 65535, 2, null), "reject address overflow");
        Reject(() => Modbus.Request(0, 3, 0, 1, null), "reject broadcast read");
        Reject(() => Modbus.Request(1, 5, 0, 1, new ushort[] { 2 }), "reject invalid coil value");
        Reject(() => Modbus.Request(1, 16, 0, 2, new ushort[1]), "reject missing write values");
        string[] numbers = {
            "-123",
            "65535",
            "0x89AB",
            "0101010101010101",
            "-2147483648",
            "4294967295",
            "1.25",
            "-9223372036854775808",
            "18446744073709551615",
            "3.1415926535897931"
        };
        for (int f = 0; f < numbers.Length; f++)
            for (int order = 0; order < 4; order++)
            {
                ushort[] words = ValueCodec.Encode(numbers[f], f, order, 1, 0);
                string display = ValueCodec.Format(words, 0, f, order, 1, 0);
                ushort[] again = ValueCodec.Encode(display, f, order, 1, 0);
                Assert(words.SequenceEqual(again), "format roundtrip " + ValueCodec.Formats[f] + " order " + order);
            }
        Assert(ValueCodec.Format(new ushort[] { 0x3f80, 0 }, 0, 6, 0, 1, 0) == "1", "float big endian");
        Assert(ValueCodec.Format(new ushort[] { 0, 0x3f80 }, 0, 6, 1, 1, 0) == "1", "float word swap");
        Assert(ValueCodec.Format(new ushort[] { 0x803f, 0 }, 0, 6, 2, 1, 0) == "1", "float byte swap");
        Assert(ValueCodec.Format(new ushort[] { 0, 0x803f }, 0, 6, 3, 1, 0) == "1", "float full swap");
        Assert(ValueCodec.Format(new ushort[] { 0 }, 0, 6, 0, 1, 0) == "—", "incomplete float");
        Assert(ValueCodec.Format(new ushort[] { 100 }, 0, 1, 0, 0.1, 5) == "15", "scale display");
        Assert(ValueCodec.Encode("15", 1, 0, 0.1, 5)[0] == 100, "scale inverse write");
        Reject(() => ValueCodec.Encode("1.5", 1, 0, 1, 0), "reject fractional integer");
    }
    static async Task Integration(MainWindow host)
    {
        var bus = host.Bus;
        host.Settings.Demo = true;
        bus.Open(host.Settings);
        var requests = new List<Task<byte[]>>();
        for (int i = 1; i <= 12; i++)
            requests.Add(bus.Exchange(Modbus.Request(i, 3, 0, 10, null), false));
        var responses = await Task.WhenAll(requests);
        Assert(responses.Length == 12 && responses.Select(r => r[0]).Distinct().Count() == 12,
             "concurrent callers serialized with correct slave responses");
        foreach (int fn in new[] { 5, 6, 15, 16 })
        {
            ushort[] w = fn == 5 ? new ushort[] { 1 } : fn == 6 ? new ushort[] { 4321 }
                : fn == 15 ? new ushort[] { 1, 0, 1, 0, 1, 0, 1, 1, 1 } : new ushort[] { 12,
                 34, 65535 };
            byte[] q = Modbus.Request(2, fn, 40, w.Length, w);
            Modbus.Decode(await bus.Exchange(q, false), q, w.Length);
            q = Modbus.Request(2, fn == 5 || fn == 15 ? 1 : 3, 40, w.Length, null);
            Assert(Modbus.Decode(await bus.Exchange(q, false), q, w.Length).SequenceEqual(w),
                 "demo write/read FC" + fn);
        }
        byte[] read = Modbus.Request(3, 3, 40, 1, null);
        Assert(Modbus.Decode(await bus.Exchange(read, false), read, 1)[0] == 40, "slave storage isolated");
        byte[] broadcast = Modbus.Request(0, 6, 100, 1, new ushort[] { 789 });
        Assert((await bus.Exchange(broadcast, false)).Length == 0, "broadcast does not await response");
        read = Modbus.Request(3, 3, 100, 1, null);
        Assert(Modbus.Decode(await bus.Exchange(read, false), read, 1)[0] == 789, "broadcast reaches demo slaves");
        string[] advanced = {
            "01 08 00 00 12 34",
            "01 0B",
            "01 11",
            "01 16 00 00 FF 00 00 0F",
            "01 17 00 00 00 01 00 00 00 01 02 12 34",
            "01 2B 0E 01 00"
        };
        foreach (string frame in advanced)
        {
            byte[] q = Modbus.AddCrc(Modbus.FromHex(frame));
            byte[] r = await bus.Exchange(q, true);
            Assert(!string.IsNullOrEmpty(Modbus.DescribeResponse(r, q)), "advanced RTU function "
                + q[1] + " response parsed");
        }
        read = Modbus.Request(1, 3, 0, 1, null);
        Assert(Modbus.Decode(await bus.Exchange(read, false), read, 1)[0] == 0x1234, "FC23 writes before read");
        await bus.Exchange(Modbus.AddCrc(Modbus.FromHex("00 16 00 0A 00 00 00 2A")), true);
        read = Modbus.Request(3, 3, 10, 1, null);
        Assert(Modbus.Decode(await bus.Exchange(read, false), read, 1)[0] == 42,
             "broadcast mask write reaches demo slaves");
        var first = host.NewDocument(new DocumentState
        {
            Title = "Mbpoll1",
            Unit = 1,
            Function = 3,
            Quantity = 10,
            Width = 575,
            Height = 420,
            Left = 8,
            Top = 5
        });
        first.State.Names[0] = "Temperature";
        first.State.Names[1] = "Pressure";
        first.Rebuild();
        await first.Execute(true);
        Assert(first.Tx == 1 && first.Errors == 0 && first.State.Values[9] == 9, "document poll fills register grid");
        var second = host.NewDocument(new DocumentState
        {
            Title = "Mbpoll2",
            Unit = 2,
            Function = 1,
            Quantity = 16,
            Columns = 2,
            Width = 575,
            Height = 420,
            Left = 600,
            Top = 5
        });
        await second.Execute(true);
        Assert(second.State.Values[1] == 1 && second.State.Values[0] == 0, "independent coil document");
        first.State.Disabled = true;
        int tx = first.Tx;
        await first.Execute(false);
        Assert(first.Tx == tx, "disabled document skips periodic read");
        await first.Execute(true);
        Assert(first.Tx == tx + 1, "F6 semantics bypass disabled flag");
        first.State.Disabled = false;
        await first.WriteWords(6, 2, new ushort[] { 5678 });
        await first.Execute(true);
        Assert(first.State.Values[2] == 5678, "document write/read");
        first.ChooseFormat(6);
        Assert(first.Grid.Columns.Count == 2 && first.Grid.Rows.Count == 10, "float display uses fixed V9 rows");
        first.ChooseFormat(0);
        first.State.BaseOne = true;
        first.Rebuild();
        Assert(first.Grid.Columns[1].HeaderText == "40001", "PLC address presentation");
        first.State.BaseOne = false;
        first.Rebuild();
        var workspace = new Workspace
        {
            Connection = host.Settings,
            Documents = new List<DocumentState> { first.Snapshot(), second.Snapshot() }
        };
        string file = Path.Combine(output, "Example.mprtu");
        var serializer = new XmlSerializer(typeof(Workspace));
        using (var stream = File.Create(file))
            serializer.Serialize(stream, workspace);
        Workspace restored;
        using (var stream = File.OpenRead(file))
            restored = (Workspace)serializer.Deserialize(stream);
        MainWindow.ValidateWorkspace(restored);
        Assert(restored.Documents.Count == 2 && restored.Documents[0].Names[0] == "Temperature"
            && restored.Documents[0].Values[2] == 5678, "workspace restores windows names values");
        restored.Documents[0].Order = 9;
        Reject(() => MainWindow.ValidateWorkspace(restored), "workspace rejects invalid format order");
        first.State.ScanRate = 20;
        second.State.ScanRate = 20;
        first.NextPoll = second.NextPoll = DateTime.UtcNow;
        int beforeFirst = first.Tx, beforeSecond = second.Tx;
        await Task.Delay(450);
        Assert(first.Tx > beforeFirst && second.Tx > beforeSecond, "scheduler polls multiple windows fairly");
        first.State.Disabled = second.State.Disabled = true;
        while (host.InFlight)
            await Task.Delay(10);
        first.State.Names[0] = "Temperature";
        first.Rebuild();
        host.LayoutMdi(MdiLayout.TileVertical);
        Application.DoEvents();
        Capture(host, "preview.png");
        using (var d = new ConnectionDialog(host.Settings))
        {
            d.Show(host);
            Application.DoEvents();
            Capture(d, "connection-preview.png");
            d.Close();
        }
        using (var d = new DefinitionDialog(first.State))
        {
            d.Show(host);
            Application.DoEvents();
            Capture(d, "definition-preview.png");
            d.Close();
        }
        string[] inventory = host.MenuInventory();
        Assert(inventory.Any(x => x == "File / Save As...") && inventory.Any(x => x == "Connection / Quick Connect")
            && inventory.Any(x => x == "Setup / Reset All Counters"), "V9 file connection setup menu commands");
        Assert(inventory.Any(x => x == "Functions / 05: Write Single Coil...")
            && inventory.Any(x => x == "Functions / 06: Write Single Register...") && inventory.Any(x => x == "Functions / 15: Write Coils...")
            && inventory.Any(x => x == "Functions / 16: Write Registers..."), "V9 separate write function entries");
        Assert(inventory.Any(x => x == "Display / 64 Bit Double / Little-endian byte swap")
            && inventory.Any(x => x == "Display / Link to Chart / Series 12") && inventory.Any(x => x == "View / Always On Top"),
             "V9 format chart and view submenus");
        var definitionState = new DocumentState();
        int applied = 0;
        using (var d = new DefinitionDialog(definitionState, () => applied++, null, null))
        {
            d.Show(host);
            Application.DoEvents();
            Assert(((TextBox)d.Controls.Find("RTUPreview", true)[0]).Text == "01 03 00 00 00 0A C5 CD",
                 "definition live RTU preview");
            Assert(((TextBox)d.Controls.Find("ASCIIPreview", true)[0]).Text == "3A 30 31 30 33 30 30 30 30 30 30 30 41 46 32 0D 0A",
                 "definition ASCII LRC preview");
            ((TextBox)d.Controls.Find("Quantity", true)[0]).Text = "25";
            ((RadioButton)d.Controls.Find("Rows20", true)[0]).Checked = true;
            Assert(d.ApplyState() && applied == 1 && definitionState.Rows == 20
                && definitionState.Quantity == 25, "Apply commits definition without closing");
            Assert(d.Visible, "Apply keeps dialog open");
            d.Close();
        }
        var layout = host.NewDocument(new DocumentState
        {
            Title = "RowsTest",
            Quantity = 25,
            Rows = 20,
            Disabled = true
        });
        Assert(layout.Grid.Rows.Count == 20 && layout.Grid.Columns.Count == 4
            && layout.Grid.Columns[3].HeaderText == "00020", "Rows controls column grouping and start headers");
        layout.Grid.CurrentCell = layout.Grid[0, 0];
        layout.PasteText("Alias A\t42\nAlias B\t43");
        Assert(layout.State.Names[0] == "Alias A" && layout.State.Values[0] == 42
            && layout.State.Values[1] == 43, "TSV paste stages aliases and values");
        layout.State.HideNames = true;
        layout.Rebuild();
        Assert(!layout.Grid.Columns[0].Visible && !layout.Grid.Columns[2].Visible,
             "Hide Alias Columns applies all blocks");
        var request = FunctionDialog.BuildRequest(1, 22, 10, new ushort[0], 0xff00, 0xf, 0, 1);
        Assert(request.Length == 10 && request[1] == 22 && request[2] == 0 && request[3] == 10,
             "dedicated mask-write request");
        request = FunctionDialog.BuildRequest(1, 23, 10, new ushort[] { 123, 456 }, 0, 0, 20, 2);
        Assert(request[1] == 23 && request[4] == 0 && request[5] == 2 && request[10] == 4
            && request.Length == 17, "dedicated read/write request");
        Reject(() => FunctionDialog.BuildRequest(1, 23, 0, new ushort[122], 0, 0, 0, 1), "FC23 max write quantity 121");
        byte[] enron = Modbus.EnronRequest(1, 16, 5000, 2, new ushort[] { 0x1234, 0x5678, 0xabcd, 0xef01 });
        Assert(enron[5] == 2 && enron[6] == 8, "Enron logical quantity and four-byte payload");
        await bus.Exchange(enron, false, true);
        enron = Modbus.EnronRequest(1, 3, 5000, 2, null);
        Assert(Modbus.Decode(await bus.Exchange(enron, false, true), enron, 4).SequenceEqual(new ushort[] { 0x1234,
             0x5678, 0xabcd, 0xef01 }), "Enron 32-bit write/read roundtrip");
        Reject(() => Modbus.EnronRequest(1, 3, 5999, 2, null), "Enron rejects range crossing");
        var enronDoc = host.NewDocument(new DocumentState
        {
            Title = "Enron",
            Address = 5000,
            Quantity = 2,
            Enron = true,
            Format = 4,
            Disabled = true
        });
        await enronDoc.Execute(true);
        Assert(enronDoc.State.Values.Length == 4 && enronDoc.DisplayAddress(2) == "05001",
             "Enron document storage and logical addressing");
        using (var chart = new ChartWindow())
        {
            chart.Link(0, first, 0);
            int count = chart.SampleCount;
            first.State.Values[0]++;
            chart.Sample(first);
            Assert(chart.SampleCount == count + 1, "chart samples linked register");
            chart.Export(Path.Combine(output, "chart-test.csv"));
            Assert(File.ReadAllLines(Path.Combine(output, "chart-test.csv")).Length == chart.SampleCount
                + 1, "chart CSV export");
            chart.Show(host);
            Application.DoEvents();
            Capture(chart, "chart-preview.png");
            chart.UnlinkAll();
            Assert(chart.SampleCount == 0, "chart unlink all");
            chart.Close();
        }
        using (var d = new FunctionDialog(host, 22))
        {
            d.Show(host);
            Application.DoEvents();
            Capture(d, "mask-write-preview.png");
            d.Close();
        }
        var clone = MainWindow.CloneDocument(layout.State);
        clone.Names[0] = "Changed";
        Assert(layout.State.Names[0] == "Alias A", "default/document clone is independent");
        var context = first.Grid.ContextMenuStrip;
        string[] contextNames = context.Items.OfType<ToolStripMenuItem>().Select(item => item.Text.Replace("&", "")).ToArray();
        Assert(contextNames.SequenceEqual(new[] { "Format", "Read/write Definition...", "Cut", "Copy", "Paste", "Select All", "Colors...", "Font...", "Scaling...", "Link to Chart" }), "context menu matches V9 screenshot order");
        var formatMenu = (ToolStripMenuItem)context.Items[0];
        Assert(formatMenu.DropDownItems.OfType<ToolStripMenuItem>().Count() == 10, "context format has four native and six multi-register formats");
        Assert(FindContext(context.Items, 32924) != null && FindContext(context.Items, 32957) != null, "context includes 64-bit endian choices and twelve chart series");
        first.ChooseFormat(0);
        first.RefreshContextMenu();
        Assert(FindContext(context.Items, 32775).Checked && !FindContext(context.Items, 32776).Checked, "context checks current signed format");
        second.Activate();
        FindContext(context.Items, 32908).PerformClick();
        Assert(host.Active == first && first.SelectedDefinition.Format == 4 && first.SelectedDefinition.Order == 3 && second.SelectedDefinition.Format == 0, "context format targets its own MDI document");
        first.RefreshContextMenu();
        Assert(FindContext(context.Items, 32908).Checked && !FindContext(context.Items, 32775).Checked, "context checks current endian leaf");
        FindContext(context.Items, 32777).PerformClick();
        Assert(first.SelectedDefinition.Format == 2, "context Hex ASCII command executes");
        first.ChooseFormat(0);
        first.Grid.ClearSelection();
        first.Grid[1, 0].Selected = true;
        first.Grid[1, 1].Selected = true;
        Assert(first.SelectContextCell(1, 1) && first.SelectedRawIndex == 1 && first.Grid.SelectedCells.Count == 2, "right click preserves selected group and moves current cell");
        Assert(first.SelectContextCell(3, 1) && first.SelectedRawIndex == 3 && first.Grid.SelectedCells.Count == 1, "right click selects unselected target cell");
        Assert(!first.SelectContextCell(-1, 1), "right click header does not select invalid row");
        enronDoc.RefreshContextMenu();
        Assert(!FindContext(enronDoc.Grid.ContextMenuStrip.Items, 32775).Enabled && FindContext(enronDoc.Grid.ContextMenuStrip.Items, 32905).Enabled, "context respects Enron format constraints");
        first.Activate();
        context.Show(first.Grid, new Point(50, 40));
        Application.DoEvents();
        CaptureMenu(context, "context-menu-preview.png");
        formatMenu.ShowDropDown();
        Application.DoEvents();
        CaptureMenu(formatMenu.DropDown, "context-format-preview.png");
        formatMenu.HideDropDown();
        context.Close();
        File.WriteAllLines(Path.Combine(output, "menu-inventory.txt"), inventory);
        Assert(first.Grid.Columns.Count == 2 && second.Grid.Columns.Count == 4,
             "MDI grids have alias/value groups and address headers");
        var mixed = host.NewDocument(new DocumentState
        {
            Title = "Mixed formats example", Unit = 1, Quantity = 60, Rows = 10,
            Disabled = true, Width = 1120, Height = 480
        });
        mixed.State.Values[0] = 249;
        mixed.State.Values[7] = 0x0232;
        mixed.State.Values[41] = 0x3fa0;
        mixed.State.Values[42] = 0;
        mixed.State.Values[43] = 0x0001;
        mixed.State.Values[44] = 0x86a0;
        ushort[] doubleWords = ValueCodec.Encode("9.5", 9, 0, 1, 0);
        Array.Copy(doubleWords, 0, mixed.State.Values, 45, 4);
        mixed.State.Values[49] = 65535;
        mixed.State.Names[0] = "Temperature";
        mixed.State.Names[7] = "Version";
        mixed.State.Names[41] = "EC Float";
        mixed.State.Names[43] = "Counter U32";
        mixed.State.Names[45] = "Double value";
        var rawBefore = (ushort[])mixed.State.Values.Clone();
        mixed.SetGroupFormat(new[] { 7 }, 2);
        mixed.SetGroupFormat(new[] { 41 }, 6, 0);
        mixed.SetGroupFormat(new[] { 43 }, 5, 0);
        mixed.SetGroupFormat(new[] { 45 }, 9, 0);
        mixed.SetGroupFormat(new[] { 49 }, 1);
        Assert(mixed.State.Values.SequenceEqual(rawBefore), "format changes preserve raw registers");
        Assert(mixed.Grid.Columns.Count == 12 && mixed.Grid.Rows.Count == 10, "mixed formats keep physical register layout");
        Assert(mixed.FormatGroup(0) == "249" && mixed.FormatGroup(41) == "1.25" && mixed.FormatGroup(43) == "100000" && mixed.FormatGroup(45) == "9.5", "integers floats counters and doubles coexist");
        Assert(mixed.FormatGroup(7) == "(?2) 0x0232" && mixed.FormatGroup(49) == "65535", "hex and unsigned coexist with float groups");
        Assert(mixed.FormatGroup(42) == "--" && mixed.FormatGroup(44) == "--" && mixed.FormatGroup(46) == "--" && mixed.FormatGroup(48) == "--", "multi-register continuation slots show markers");
        Assert(mixed.DefinitionAt(40).Format == 0 && mixed.DefinitionAt(43).Format == 5, "float format does not affect neighboring groups");
        Assert(mixed.Grid.Columns[9].HeaderText == "00040" && Convert.ToString(mixed.Grid[9, 1].Value) == "1.25", "mixed grid headers retain protocol addresses");
        mixed.SetGroupFormat(new[] { 9 }, 6, 0);
        Assert(mixed.GroupStart(10) == 9 && Convert.ToString(mixed.Grid[3, 0].Value) == "--", "multi-register group crosses a column boundary without reflow");
        mixed.SelectContextCell(1, 9);
        mixed.RefreshContextMenu();
        Assert(FindContext(mixed.Grid.ContextMenuStrip.Items, 32913).Checked, "float group context checks its own format");
        mixed.SelectContextCell(0, 9);
        mixed.RefreshContextMenu();
        Assert(FindContext(mixed.Grid.ContextMenuStrip.Items, 32775).Checked && !FindContext(mixed.Grid.ContextMenuStrip.Items, 32913).Checked, "adjacent integer context has independent format checks");
        mixed.SelectContextCell(0, 1);
        mixed.UpdateSelectedDefinitions(definition => { definition.Scale = 0.1; definition.Offset = 1; });
        Assert(mixed.FormatGroup(0) == "25.9" && mixed.FormatGroup(41) == "1.25", "scaling applies only to selected group");
        Assert(mixed.EncodeGroup(0, "31")[0] == 300, "group scaling inverse conversion");
        mixed.SelectContextCell(1, 9);
        mixed.UpdateSelectedDefinitions(definition => { definition.Foreground = Color.Green.ToArgb(); definition.FontSize = 12; });
        Assert(mixed.Grid[9, 1].Style.ForeColor.ToArgb() == Color.Green.ToArgb() && mixed.Grid[1, 0].Style.ForeColor.ToArgb() != Color.Green.ToArgb(), "colors apply only to selected group");
        Assert(mixed.Grid[9, 1].Style.Font.Size == 12 && mixed.Grid[1, 0].Style.Font.Size == UiFonts.Size, "font applies only to selected group");
        var storedNeighbor = mixed.DefinitionAt(43).Copy();
        mixed.SetGroupFormat(new[] { 41 }, 6, 3);
        Assert(mixed.DefinitionAt(41).Order == 3 && mixed.DefinitionAt(43).Order == storedNeighbor.Order, "byte order applies only to selected group");
        mixed.SetGroupFormat(new[] { 41 }, 6, 0);
        int definitionCount = mixed.State.Definitions.Count;
        Reject(() => mixed.SetGroupFormat(new[] { 59 }, 9), "reject incomplete selected group at read boundary");
        Assert(mixed.State.Definitions.Count == definitionCount, "failed format change is atomic");
        mixed.SetGroupFormat(new[] { 20, 21, 22, 23 }, 5, 0);
        Assert(mixed.GroupStart(21) == 20 && mixed.GroupStart(23) == 22 && mixed.DefinitionAt(24).Format == 0, "selected range forms independent register pairs");
        mixed.SetGroupFormat(new[] { 20 }, 0);
        Assert(mixed.GroupStart(21) == 21 && mixed.GroupStart(23) == 22, "splitting one group preserves the other group");
        var mixedState = MainWindow.CloneDocument(mixed.Snapshot());
        MainWindow.ValidateWorkspace(new Workspace { Documents = new List<DocumentState> { mixedState } });
        var restoredMixed = host.NewDocument(mixedState);
        Assert(restoredMixed.FormatGroup(41) == "1.25" && restoredMixed.DefinitionAt(41).FontSize == 12 && restoredMixed.FormatGroup(0) == "25.9", "saved mixed formats styles and scaling restore");
        var invalidMixed = MainWindow.CloneDocument(mixed.State);
        invalidMixed.Definitions.Add(new RegisterDefinition { Address = 42, Format = 0 });
        Reject(() => MainWindow.ValidateWorkspace(new Workspace { Documents = new List<DocumentState> { invalidMixed } }), "workspace rejects overlapping register groups");
        var legacy = host.NewDocument(new DocumentState { Title = "Legacy float", Quantity = 6, Format = 6, Disabled = true });
        legacy.SetGroupFormat(new[] { 0 }, 0);
        Assert(legacy.DefinitionAt(0).Format == 0 && legacy.DefinitionAt(2).Format == 6 && legacy.GroupStart(3) == 2, "legacy window-wide formats migrate without changing other groups");
        restoredMixed.Close();
        legacy.Close();
        mixed.SelectContextCell(1, 9);
        var beforeWrite = (ushort[])mixed.State.Values.Clone();
        await mixed.WriteGroup(41, "2.5");
        Assert(mixed.State.Values[41] == 0x4020 && mixed.State.Values[42] == 0 && mixed.State.Values[40] == beforeWrite[40] && mixed.State.Values[43] == beforeWrite[43], "formatted write touches only the selected register group");
        await mixed.Execute(true);
        Assert(mixed.FormatGroup(41) == "2.5" && mixed.DefinitionAt(43).Format == 5, "poll refresh preserves mixed definitions after formatted write");
        mixed.SelectContextCell(2, 9);
        Assert(mixed.SelectedRawIndex == 41, "continuation selection resolves to group start");
        mixed.SelectContextCell(1, 9);
        mixed.PasteText("3.75");
        Assert(mixed.FormatGroup(41) == "3.75" && mixed.DefinitionAt(0).Scale == 0.1, "paste encodes using the selected group definition");
        mixed.SelectContextCell(2, 9);
        Reject(() => mixed.PasteText("123"), "paste rejects independent write into continuation register");
        using (var mixedChart = new ChartWindow())
        {
            mixedChart.Link(0, mixed, 41);
            mixedChart.Export(Path.Combine(output, "mixed-chart-test.csv"));
            Assert(File.ReadAllLines(Path.Combine(output, "mixed-chart-test.csv")).Last().EndsWith(",3.75"), "chart samples decoded group format rather than window defaults");
        }
        mixed.State.Values[0] = 249;
        mixed.State.Values[7] = 0x0232;
        Array.Copy(doubleWords, 0, mixed.State.Values, 45, 4);
        mixed.Rebuild();
        var mixedExample = new Workspace { Connection = host.Settings, Documents = new List<DocumentState> { mixed.Snapshot() } };
        using (var stream = File.Create(Path.Combine(output, "MixedFormatsExample.mprtu")))
        {
            new XmlSerializer(typeof(Workspace)).Serialize(stream, mixedExample);
        }
        foreach (var child in host.MdiChildren) child.WindowState = FormWindowState.Minimized;
        mixed.WindowState = FormWindowState.Maximized;
        mixed.Activate();
        Application.DoEvents();
        foreach (DataGridViewColumn column in mixed.Grid.Columns) column.Width = column.Index % 2 == 0 ? 85 : 110;
        Capture(host, "mixed-formats-preview.png");
        await bus.Close();
        Reject(() => bus.Exchange(Modbus.Request(1, 3, 0, 1, null), false).GetAwaiter().GetResult(),
             "closed bus rejects transactions");
        Assert(!bus.Connected, "disconnect completes");
    }
    static ToolStripMenuItem FindContext(ToolStripItemCollection items, int id)
    {
        foreach (ToolStripMenuItem item in items.OfType<ToolStripMenuItem>())
        {
            if (item.Tag is int && (int)item.Tag == id)
            {
                return item;
            }

            var child = FindContext(item.DropDownItems, id);
            if (child != null)
            {
                return child;
            }
        }

        return null;
    }

    static void CaptureMenu(ToolStripDropDown menu, string name)
    {
        using (var bitmap = new Bitmap(menu.Width, menu.Height))
        {
            menu.DrawToBitmap(bitmap, new Rectangle(0, 0, menu.Width, menu.Height));
            bitmap.Save(Path.Combine(output, name), System.Drawing.Imaging.ImageFormat.Png);
        }
    }

    static void Capture(Form form, string name)
    {
        using (var bitmap = new Bitmap(form.Width, form.Height))
        {
            form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height));
            bitmap.Save(Path.Combine(output, name), System.Drawing.Imaging.ImageFormat.Png);
        }
    }
}





