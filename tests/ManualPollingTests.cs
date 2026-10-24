using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ModbusGetFree;

class ManualPollingTests
{
    static int checks;
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }
    static async Task Bounded(Task task)
    {
        if (await Task.WhenAny(task, Task.Delay(5000)) != task) throw new Exception("Test timed out");
        await task;
    }
    static T Find<T>(Control parent, string name) where T : Control
    { return parent.Controls.Find(name, true).OfType<T>().Single(); }
    [STAThread] static void Main()
    {
        Application.EnableVisualStyles();
        using (var host = new MainWindow(true))
        {
            host.Shown += async (s, e) =>
            {
                try { await Run(host); Console.WriteLine("PASS " + checks + " manual/polling checks"); Environment.Exit(0); }
                catch (Exception error) { Console.WriteLine(error); Environment.Exit(1); }
            };
            Application.Run(host);
        }
    }
    static async Task Run(MainWindow host)
    {
        host.Settings.Demo = true;
        host.Settings.Delay = 0;
        await host.QuickConnect();
        var scheduler = (System.Windows.Forms.Timer)typeof(MainWindow).GetField("scheduler", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(host);
        int ticks = 0;
        scheduler.Tick += (s, e) => ticks++;
        var poll = host.NewDocument(new DocumentState { Unit = 31, Function = 3, Quantity = 1, ScanRate = 100000 });
        var other = host.NewDocument(new DocumentState { Unit = 32, Function = 4, Quantity = 1, ScanRate = 100000, Disabled = true });
        poll.NextPoll = other.NextPoll = DateTime.UtcNow.AddHours(1);

        foreach (int fn in new[] { 5, 6, 15, 16, 17, 22, 23 })
        {
            poll.NextPoll = DateTime.UtcNow.AddHours(1);
            other.State.Disabled = true;
            using (var dialog = new FunctionDialog(host, fn))
            using (var allowPollTx = new ManualResetEventSlim())
            using (var allowPollRx = new ManualResetEventSlim())
            using (var allowManual = new ManualResetEventSlim())
            {
                Find<NumericUpDown>(dialog, "SlaveId").Value = 55;
                var pollTx = new TaskCompletionSource<bool>();
                var pollRx = new TaskCompletionSource<bool>();
                var manualTx = new TaskCompletionSource<bool>();
                var resumed = new TaskCompletionSource<bool>();
                var frames = new List<string>();
                Action<string> traffic = line =>
                {
                    int prefix = line.IndexOf("  ", StringComparison.Ordinal);
                    if (prefix >= 0) line = line.Substring(prefix + 2);
                    lock (frames) frames.Add(line);
                    if (line.StartsWith("TX: 1F 03")) { pollTx.TrySetResult(true); if (!allowPollTx.Wait(4000)) throw new Exception("Poll TX barrier timed out"); }
                    if (line.StartsWith("RX: 1F 03")) { pollRx.TrySetResult(true); if (!allowPollRx.Wait(4000)) throw new Exception("Poll RX barrier timed out"); }
                    if (line.StartsWith("TX: 37 ")) { manualTx.TrySetResult(true); if (!allowManual.Wait(4000)) throw new Exception("Manual barrier timed out"); }
                    if (line.StartsWith("TX: 20 04")) resumed.TrySetResult(true);
                };
                host.Bus.Traffic += traffic;
                try
                {
                    Task current = poll.Execute(false);
                    await Bounded(pollTx.Task);
                    Task manual = dialog.Transmit();
                    Check(host.PollingPaused && !manual.IsCompleted, "FC" + fn + " waits for active poll TX");
                    Check(!Find<Button>(dialog, "Send").Enabled, "FC" + fn + " disables duplicate sends while waiting");
                    Task duplicate = dialog.Transmit();
                    await Bounded(duplicate);
                    other.State.Disabled = false;
                    other.NextPoll = DateTime.UtcNow.AddMilliseconds(-1000);
                    DateTime due = other.NextPoll;
                    int before = ticks;
                    await Task.Delay(90);
                    Check(scheduler.Enabled && ticks > before, "FC" + fn + " scheduler ticks continue while waiting");
                    Check(!manualTx.Task.IsCompleted && !resumed.Task.IsCompleted, "FC" + fn + " no interleaved manual or second poll TX");
                    allowPollTx.Set();
                    await Bounded(pollRx.Task);
                    Check(host.PollingPaused && !manualTx.Task.IsCompleted, "FC" + fn + " waits through poll RX");
                    allowPollRx.Set();
                    await Bounded(manualTx.Task);
                    Check(!poll.Busy && host.PollingPaused && host.InFlight, "FC" + fn + " starts only after completed poll");
                    before = ticks;
                    await Task.Delay(90);
                    Check(scheduler.Enabled && ticks > before && other.NextPoll == due, "FC" + fn + " timer and due time retained during manual request");
                    Check(!resumed.Task.IsCompleted, "FC" + fn + " all windows pause until manual response");
                    allowManual.Set();
                    await Bounded(manual);
                    await Bounded(current);
                    Check(!host.PollingPaused && Find<Button>(dialog, "Send").Enabled, "FC" + fn + " releases pause after reply");
                    Check(!poll.State.Disabled && !other.State.Disabled, "FC" + fn + " preserves polling preferences");
                    await Bounded(resumed.Task);
                    other.State.Disabled = true;
                    while (host.InFlight) await Task.Delay(1);
                    string[] trace;
                    lock (frames) trace = frames.ToArray();
                    int manualIndex = Array.FindIndex(trace, x => x.StartsWith("TX: 37 "));
                    int responseIndex = Array.FindIndex(trace, x => x.StartsWith("RX: 37 "));
                    int resumeIndex = Array.FindIndex(trace, x => x.StartsWith("TX: 20 04"));
                    Check(Array.FindIndex(trace, x => x.StartsWith("RX: 1F 03")) < manualIndex && responseIndex > manualIndex && resumeIndex > responseIndex,
                        "FC" + fn + " frame order poll TX/RX -> manual TX/RX -> resumed poll");
                    Check(trace.Count(x => x.StartsWith("TX: 37 ")) == 1, "FC" + fn + " one request despite duplicate click");
                    Check(!dialog.ResultText.Contains("Error:"), "FC" + fn + " successful reply validation");
                    Console.WriteLine("FC" + fn + ": TX/RX ordering, timer continuity, duplicate prevention, shared polling pause/resume passed");
                }
                finally
                {
                    allowPollTx.Set(); allowPollRx.Set(); allowManual.Set();
                    host.Bus.Traffic -= traffic;
                }
            }
        }

        // Faults through the actual Exchange path must release both ownership and the pause.
        foreach (Exception error in new Exception[] { new TimeoutException("Response timeout."), new IOException("CRC mismatch.") })
        {
            poll.NextPoll = DateTime.UtcNow.AddHours(1);
            using (var dialog = new FunctionDialog(host, 6))
            {
                Find<NumericUpDown>(dialog, "SlaveId").Value = 55;
                Action<string> fault = line => { if (line.Contains("TX: 37 ")) throw error; };
                host.Bus.Traffic += fault;
                try { await Bounded(dialog.Transmit()); }
                finally { host.Bus.Traffic -= fault; }
                Check(dialog.ResultText.Contains(error.Message), "Fault reaches result: " + error.Message);
                Check(!host.PollingPaused && !host.InFlight && scheduler.Enabled, "Fault restores polling and timer: " + error.Message);
            }
            other.State.Disabled = false; other.NextPoll = DateTime.UtcNow;
            long tx = other.Tx;
            for (int i = 0; i < 200 && other.Tx == tx; i++) await Task.Delay(5);
            Check(other.Tx > tx, "Polling actually resumes after " + error.Message);
            other.State.Disabled = true;
            while (host.InFlight) await Task.Delay(1);
        }

        foreach (int fn in new[] { 5, 6, 15, 16, 22 })
        using (var dialog = new FunctionDialog(host, fn))
        {
            Find<NumericUpDown>(dialog, "SlaveId").Value = 0;
            await Bounded(dialog.Transmit());
            Check(dialog.ResultText.Contains("Broadcast sent") && !host.PollingPaused && !host.InFlight, "FC" + fn + " broadcast completes without waiting for RX");
        }

        // Selected-cell writes use the same pause without double-acquiring InFlight.
        using (var dialog = poll.CreateWriteDialog(0))
        {
            host.InFlight = true;
            Task pending = dialog.Transmit();
            Check(host.PollingPaused && !pending.IsCompleted, "Selected write waits");
            host.InFlight = false;
            await Bounded(pending);
            Check(dialog.ResultText == "Response ok" && !host.PollingPaused, "Selected write completes and resumes");
        }
        using (var invalid = new FunctionDialog(host, 6))
        {
            Find<TextBox>(invalid, "WriteValue").Text = "invalid";
            host.InFlight = true;
            await Bounded(invalid.Transmit());
            Check(host.InFlight && !host.PollingPaused, "Validation failure cannot clear another transaction's busy flag");
            host.InFlight = false;
        }
        using (var closed = new FunctionDialog(host, 5))
        {
            host.InFlight = true;
            Task pending = closed.Transmit();
            closed.Dispose();
            host.InFlight = false;
            await Bounded(pending);
            Check(!host.PollingPaused && !host.InFlight, "Closing a waiting dialog cancels its send and releases pause");
        }
        poll.State.Disabled = true;
        // Queued manual operations keep the reservation until the last one finishes.
        var firstStarted = new TaskCompletionSource<bool>();
        var secondStarted = new TaskCompletionSource<bool>();
        var firstRelease = new TaskCompletionSource<bool>();
        var secondRelease = new TaskCompletionSource<bool>();
        Task first = host.RunManualOperation(async () => { firstStarted.SetResult(true); await firstRelease.Task; });
        Task second = host.RunManualOperation(async () => { secondStarted.SetResult(true); await secondRelease.Task; });
        await Bounded(firstStarted.Task);
        Check(host.PollingPaused && !secondStarted.Task.IsCompleted, "Manual requests serialize");
        firstRelease.SetResult(true);
        await Bounded(first);
        await Bounded(secondStarted.Task);
        Check(host.PollingPaused, "No polling gap between queued manual requests");
        secondRelease.SetResult(true);
        await Bounded(second);
        Check(!host.PollingPaused && scheduler.Enabled, "Final queued request releases pause");
        await host.RunManualOperation(async () => await Task.Delay(30));
        Check(poll.State.Disabled && other.State.Disabled, "Originally disabled polling stays disabled");
        await host.Bus.Close();
    }
}
