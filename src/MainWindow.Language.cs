using System.Windows.Forms;
namespace ModbusGetFree
{
    public partial class MainWindow
    {
        void BuildLanguageMenu()
        {
            var languages = new ToolStripMenuItem("Language/ 语言");
            var chinese = new ToolStripMenuItem("简体中文");
            var english = new ToolStripMenuItem("English");
            chinese.Click += (s, e) => ChangeLanguage(true);
            english.Click += (s, e) => ChangeLanguage(false);
            languages.DropDownItems.Add(chinese);
            languages.DropDownItems.Add(english);
            languages.DropDownOpening += (s, e) =>
            {
                chinese.Checked = Language.Chinese;
                english.Checked = !Language.Chinese;
            };
            ((ToolStripMenuItem)menu.Items[6]).DropDownItems.Add(languages);
        }
        void ChangeLanguage(bool chinese)
        {
            Language.Chinese = chinese;
            UpdateConnection();
            Language.Refresh();
            foreach (Form form in Application.OpenForms)
                if (form is ChartWindow) form.Invalidate(true);
            SavePreferences();
        }
    }
}
