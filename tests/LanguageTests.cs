using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.Xml.Serialization;
using ModbusGetFree;
class LanguageTests
{
    [STAThread] static int Main()
    {
        try
        {
            Application.EnableVisualStyles();
            Language.Chinese = false;
            using (var main = new MainWindow(true))
            using (var connection = new ConnectionDialog(new SerialSettings()))
            using (var definition = new DefinitionDialog(new DocumentState()))
            using (var traffic = new TrafficWindow())
            using (var editor = new EditorDialog("Value", 300))
            {
                var input = new TextBox { Text = "Value" };
                editor.Field("Value", input);
                var toolbar = main.Controls.OfType<ToolStrip>().First(strip => !(strip is MenuStrip) && !(strip is StatusStrip));
                var originalTips = toolbar.Items.OfType<ToolStripButton>().ToDictionary(button => button, button => button.ToolTipText);
                Language.Chinese = true;
                Language.Apply(main); Language.Apply(connection); Language.Apply(definition); Language.Apply(traffic); Language.Apply(editor);
                foreach (var tip in originalTips)
                    Check(tip.Key.ToolTipText != tip.Value && tip.Key.ToolTipText.Any(c => c > 127), "translated tooltip: " + tip.Value);
                Check(Language.Text("Format") == "格式" && Language.Text("Read/write Definition...") == "读写定义...", "context captions");
                Check(Language.Text("Export CSV...") == "导出 CSV...", "chart export caption");
                Check(Language.Text("Select a value cell, then Display > Link to Chart > Series 1..12").StartsWith("选择"), "chart canvas hint");
                Check(connection.Text == "连接设置 — Modbus RTU", "connection Chinese");
                Check(definition.Text == "读写定义", "definition Chinese");
                Check(traffic.Text == "通信记录", "traffic Chinese");
                Check(input.Text == "Value", "user input preserved");
                var menu = main.MainMenuStrip;
                Check(menu.Items[0].Text == "文件(&F)", "Chinese menu");
                var view = (ToolStripMenuItem)menu.Items[6];
                var languages = (ToolStripMenuItem)view.DropDownItems[view.DropDownItems.Count - 1];
                languages.DropDownItems[1].PerformClick();
                Language.Apply(connection); Language.Apply(definition); Language.Apply(editor);
                Language.Apply(main);
                foreach (var tip in originalTips) Check(tip.Key.ToolTipText == tip.Value, "restored tooltip: " + tip.Value);
                Check(!Language.Chinese && connection.Text == "Connection Setup — Modbus RTU", "English switch");
                languages.DropDownItems[0].PerformClick();
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "preferences.xml");
                using (var reader = File.OpenRead(path))
                    Check(((Preferences)new XmlSerializer(typeof(Preferences)).Deserialize(reader)).Language == "zh-CN", "language saved");
                Language.Chinese = false;
                typeof(MainWindow).GetMethod("LoadPreferences", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(main, null);
                Check(Language.Chinese, "language loaded");
                Language.Chinese = false;
                Language.Apply(definition);
                Check(definition.Text == "Read/Write Definition", "reverse translation");
                languages.DropDownItems[1].PerformClick();
            }
            Console.WriteLine("Language tests passed: menus, dialogs, both directions, editable text, persistence.");
            return 0;
        }
        catch(Exception e) { Console.WriteLine(e); return 1; }
    }
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); }
}
