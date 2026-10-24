using System.Windows.Forms;
namespace ModbusGetFree
{
    public partial class MainWindow
    {
        void BuildLanguageMenu()
        {
            var languages = new ToolStripMenuItem("Language");
            for (int i = 0; i < Language.Codes.Length; i++)
            {
                string code = Language.Codes[i];
                var item = new ToolStripMenuItem(Language.Names[i]);
                item.Click += (s, e) => ChangeLanguage(code);
                languages.DropDownItems.Add(item);
            }
            languages.DropDownOpening += (s, e) =>
            {
                for (int i = 0; i < Language.Codes.Length; i++)
                    ((ToolStripMenuItem)languages.DropDownItems[i]).Checked = Language.Code == Language.Codes[i];
            };
            menu.Items.Insert(menu.Items.Count - 1, languages);
        }
        void ChangeLanguage(string code)
        {
            Language.Code = code;
            UpdateConnection();
            Language.Refresh();
            foreach (Form form in Application.OpenForms)
                if (form is ChartWindow) form.Invalidate(true);
            SavePreferences();
        }
    }
}
