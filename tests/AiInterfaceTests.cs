using System;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;
using ModbusGetFree;

class AiInterfaceTests
{
    static int checks;
    static void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
    static string Get(AiReadServer server, string token, string method, string origin)
    {
        var request = (HttpWebRequest)WebRequest.Create(server.BaseUrl + "/v1/snapshot");
        request.Proxy = null; request.Timeout = 3000; request.Method = method;
        if (token != null) request.Headers["Authorization"] = "Bearer " + token;
        if (origin != null) request.Headers["Origin"] = origin;
        try { using (var response = request.GetResponse()) using (var reader = new System.IO.StreamReader(response.GetResponseStream())) return reader.ReadToEnd(); }
        catch (WebException e) { return ((HttpWebResponse)e.Response).StatusCode.ToString(); }
    }
    [STAThread] static void Main()
    {
        using (var host = new MainWindow(true))
        {
            host.Shown += async (s, e) => {
                try { await Run(host); Console.WriteLine("PASS " + checks + " AI checks"); Environment.Exit(0); }
                catch (Exception error) { Console.WriteLine(error); Environment.Exit(1); }
            };
            Application.Run(host);
        }
    }
    static async Task Run(MainWindow host)
    {
        var data = host.NewDocument(new DocumentState { Quantity = 2, Disabled = true });
        Check(data.AiQuality(DateTime.UtcNow) == "never_read", "initial values not measurements");
        host.Settings.Demo = true; host.Settings.Delay = 0; await host.QuickConnect();
        await data.Execute(true);
        Check(data.AiQuality(DateTime.UtcNow) == "fresh", "successful read fresh");
        Check(data.AiQuality(DateTime.UtcNow.AddHours(1)) == "stale", "old values stale");
        Action<string> fail = line => { throw new System.IO.IOException("simulated communication error"); };
        host.Bus.Traffic += fail;
        await data.Execute(true);
        host.Bus.Traffic -= fail;
        Check(data.AiQuality(DateTime.UtcNow) == "read_error", "failed read retains error");
        host.Settings.Delay = 300;
        Task retry = data.Execute(true);
        Check(data.AiQuality(DateTime.UtcNow) == "read_error", "retry must not clear error before success");
        await retry;
        host.Settings.Delay = 0;
        data.State.Values[0]++;
        Check(data.AiQuality(DateTime.UtcNow) == "unverified", "edits not measurements");
        await data.Execute(true);
        string json = host.CaptureAiSnapshot();
        Check(json.Contains("\"source\":\"demo\"") && json.Contains("\"rawWords\""), "snapshot provenance");
        int tx = data.Tx;
        using (var server = new AiReadServer(0))
        {
            server.Publish(json);
            Check(Get(server, null, "GET", null) == "Unauthorized", "token required");
            Check(Get(server, "wrong", "GET", null) == "Unauthorized", "wrong token rejected");
            Check(Get(server, server.Token, "POST", null) == "MethodNotAllowed", "writes rejected");
            Check(Get(server, server.Token, "GET", "https://example.com") == "Forbidden", "foreign origin rejected");
            Check(Get(server, server.Token, "GET", null) == json, "external snapshot read");
            await Task.Delay(5200);
            Check(Get(server, server.Token, "GET", null) == "ServiceUnavailable", "frozen publication rejected");
        }
        Check(data.Tx == tx, "API did not poll device");
        await host.Bus.Close();
        Check(data.AiQuality(DateTime.UtcNow) == "disconnected", "disconnect visible");
        host.Bus.Open(host.Settings);
        Check(data.AiQuality(DateTime.UtcNow) == "unverified", "reconnect invalidates old session");
        await host.Bus.Close();
    }
}
