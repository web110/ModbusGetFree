using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ModbusGetFree
{
    /// <summary>
    /// classic 菜单和工具栏的建立、勾选/启用状态，以及菜单命令到实际功能的分发。
    /// </summary>
    public partial class MainWindow
    {
        void BuildAppMenus()
        {
            menu.Items.Clear();
            foreach (var node in AppMenus.All())
            {
                menu.Items.Add(BuildMenu(node));
            }

            connectItem = commands[32772];
            disconnectItem = commands[32773];
            windowMenu = (ToolStripMenuItem)menu.Items[7];
            menu.MdiWindowListItem = windowMenu;
            menu.MenuActivate += (s, e) => RefreshChecks();
            commands[57616].DropDownOpening += (s, e) =>
            {
                commands[57616].DropDownItems.Clear();
                foreach (string file in recentFiles.ToArray())
                {
                    var item = new ToolStripMenuItem(file);
                    item.Click += (sender, args) => OpenDocument(file);
                    commands[57616].DropDownItems.Add(item);
                }

            };
        }

        ToolStripItem BuildMenu(MenuNode node)
        {
            if (node.Text == "")
            {
                return new ToolStripSeparator();
            }

            string[] parts = node.Text.Split('\t');
            var item = new ToolStripMenuItem(parts[0])
            {
                Tag = node.Id
            };
            if (parts.Length > 1)
            {
                Keys shortcut = Shortcut(parts[1]);
                if (shortcut != Keys.None)
                {
                    item.ShortcutKeys = shortcut;
                }
                else
                {
                    item.ShortcutKeyDisplayString = parts[1];
                }
            }

            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    item.DropDownItems.Add(BuildMenu(child));
                }
            }
            else
            {
                commands[node.Id] = item;
                item.Click += async (s, e) => await ExecuteCommand(node.Id);
                if (node.Id == 57616)
                {
                    item.DropDownItems.Add("(No recent files)");
                }
            }

            return item;
        }

        internal static Keys Shortcut(string text)
        {
            text = text.Replace(" ", "").Replace("Ctrl", "Control");
            Keys key = Keys.None;
            foreach (string part in text.Split('+'))
            {
                Keys parsed;
                if (!Enum.TryParse<Keys>(part, true, out parsed))
                {
                    return Keys.None;
                }

                key |= parsed;
            }

            return key;
        }

        void BuildToolbar()
        {
            tools.Items.Clear();
            tools.GripStyle = ToolStripGripStyle.Hidden;
            tools.ImageScalingSize = new Size(18, 18);
            int[] ids =
            {
                57600,
                57601,
                57603,
                57607,
                0,
                32801,
                0,
                32771,
                0,
                32773,
                32838,
                0,
                32854,
                0,
                32842,
                32844,
                32846,
                32848,
                32894,
                32849,
                32839,
                0,
                32826,
                32825,
                32936,
                0,
                57664,
                57670
            };
            foreach (int id in ids)
            {
                if (id == 0)
                {
                    tools.Items.Add(new ToolStripSeparator());
                    continue;
                }

                string label = commands.ContainsKey(id) ? commands[id].Text.Replace("&", "") : "Context Help";
                string text = id == 32842 ? "05" : id == 32844 ? "06" : id == 32846
                    ? "15" : id == 32848 ? "16" : id == 32894 ? "17" : id == 32849 ? "22" : id == 32839
                    ? "23" : id == 32826 ? "TC" : null;
                var button = new ToolStripButton
                {
                    ToolTipText = label,
                    AutoSize = false,
                    Size = new Size(text != null ? 29 : 25, 26),
                    DisplayStyle = ToolStripItemDisplayStyle.Image,
                    Text = text ?? label,
                    Tag = id
                };
                button.Image = ClassicIcons.Create(id);

                int command = id;
                button.Click += async (s, e) => await ExecuteCommand(command);
                tools.Items.Add(button);
                toolbarCommands[id] = button;
            }

            connectButton = toolbarCommands[32838];
        }

        public string[] MenuInventory()
        {
            var list = new List<string>();
            Action<ToolStripItemCollection, string> walk = null;
            walk = (items, path) =>
            {
                foreach (ToolStripItem i in items)
                {
                    var m = i as ToolStripMenuItem;
                    if (m == null)
                    {
                        continue;
                    }

                    string name = path + m.Text.Replace("&", "");
                    list.Add(name);
                    walk(m.DropDownItems, name + " / ");
                }

            };
            walk(menu.Items, "");
            return list.ToArray();
        }

        void RefreshChecks()
        {
            bool doc = Active != null;
            foreach (var pair in commands)
            {
                int id = pair.Key;
                if (id != 32772 && id != 32773 && id != 57616)
                {
                    pair.Value.Enabled = true;
                }
            }

            commands[32772].Enabled = !Bus.Connected;
            commands[32838].Enabled = !Bus.Connected;
            commands[32773].Enabled = Bus.Connected;
            commands[32835].Checked = autoConnect;
            commands[32836].Checked = !autoConnect;
            commands[59392].Checked = tools.Visible;
            commands[59393].Checked = status.Visible;
            commands[32861].Checked = TopMost;
            commands[57616].Enabled = recentFiles.Count > 0;
            foreach (int id in new[]
            {
                57602,
                57603,
                57604,
                57607,
                57609,
                32771,
                32854,
                32880,
                32801,
                32815,
                32775,
                32776,
                32777,
                32780,
                32778,
                32779,
                32862,
                32876,
                32929,
                32901
            }

            )
            {
                commands[id].Enabled = doc;
            }

            commands[32854].Enabled = doc && Bus.Connected;
            if (doc)
            {
                commands[32880].Checked = Active.State.Disabled;
                commands[32778].Checked = Active.State.BaseOne;
                commands[32779].Checked = !Active.State.BaseOne;
                commands[32862].Checked = Active.State.ShowErrors;
                int[] basic =
                {
                    32775,
                    32776,
                    32777,
                    32780
                };
                for (int i = 0; i < 4; i++)
                {
                    commands[basic[i]].Checked = Active.SelectedDefinition.Format == i;
                }
            }

            foreach (var pair in toolbarCommands)
            {
                if (commands.ContainsKey(pair.Key))
                {
                    pair.Value.Enabled = commands[pair.Key].Enabled;
                }
            }
        }

        async Task ExecuteCommand(int id, DataWindow target = null)
        {
            try
            {
                var doc = target ?? Active;
                switch (id)
                {
                    case 57600:
                        NewDocument(null);
                        break;
                    case 57601:
                        ChooseOpenDocument();
                        break;
                    case 57602:
                        if (doc != null)
                        {
                            doc.Close();
                        }

                        break;
                    case 57603:
                        if (doc != null)
                        {
                            doc.SaveDocument(false);
                        }

                        break;
                    case 57604:
                        if (doc != null)
                        {
                            doc.SaveDocument(true);
                        }

                        break;
                    case 32859:
                        OpenWorkspace();
                        break;
                    case 32858:
                        SaveWorkspace(true);
                        break;
                    case 57665:
                        Close();
                        break;
                    case 57607:
                        if (doc != null)
                        {
                            doc.Print(false);
                        }

                        break;
                    case 57609:
                        if (doc != null)
                        {
                            doc.Print(true);
                        }

                        break;
                    case 57606:
                        if (doc != null)
                        {
                            doc.PrintSetup();
                        }

                        break;
                    case 57634:
                        if (doc != null)
                        {
                            doc.Copy();
                        }

                        break;
                    case 57635:
                        if (doc != null)
                        {
                            doc.Cut();
                        }

                        break;
                    case 57637:
                        if (doc != null)
                        {
                            doc.Paste();
                        }

                        break;
                    case 57642:
                        if (doc != null)
                        {
                            doc.Grid.SelectAll();
                        }

                        break;
                    case 32772:
                        await Connect();
                        break;
                    case 32773:
                        await Disconnect();
                        break;
                    case 32838:
                        await QuickConnect();
                        break;
                    case 32835:
                        autoConnect = true;
                        SavePreferences();
                        break;
                    case 32836:
                        autoConnect = false;
                        SavePreferences();
                        break;
                    case 32771:
                        if (doc != null)
                        {
                            doc.Define();
                        }

                        break;
                    case 32854:
                        if (doc != null && !InFlight)
                        {
                            await doc.Execute(true);
                        }

                        break;
                    case 32880:
                        if (doc != null)
                        {
                            doc.ToggleDisabled();
                        }

                        break;
                    case 32881:
                        if (doc != null)
                        {
                            doc.StartExcelLog();
                        }

                        break;
                    case 32882:
                        if (doc != null)
                        {
                            doc.StopExcelLog();
                        }

                        break;
                    case 32808:
                        if (doc != null)
                        {
                            doc.StartTextLog();
                        }

                        break;
                    case 32809:
                        if (doc != null)
                        {
                            doc.StopTextLog();
                        }

                        break;
                    case 32801:
                        if (doc != null)
                        {
                            doc.Tx = doc.Errors = 0;
                            doc.UpdateSummary();
                        }

                        break;
                    case 32900:
                        foreach (var d in MdiChildren.OfType<DataWindow>())
                        {
                            d.Tx = d.Errors = 0;
                            d.UpdateSummary();
                        }

                        break;
                    case 32815:
                        if (doc != null)
                        {
                            defaultDocument = CloneDocument(doc.State);
                            SavePreferences();
                        }

                        break;
                    case 32842:
                        OpenFunction(5);
                        break;
                    case 32844:
                        OpenFunction(6);
                        break;
                    case 32846:
                        OpenFunction(15);
                        break;
                    case 32848:
                        OpenFunction(16);
                        break;
                    case 32894:
                        OpenFunction(17);
                        break;
                    case 32849:
                        OpenFunction(22);
                        break;
                    case 32839:
                        OpenFunction(23);
                        break;
                    case 32826:
                        TestCenter();
                        break;
                    case 32775:
                    case 32776:
                    case 32777:
                    case 32780:
                        if (doc != null)
                        {
                            doc.ChooseFormat(id == 32775 ? 0 : id == 32776 ? 1 : id == 32777 ? 2 : 3);
                        }

                        break;
                    case 32825:
                        ShowTraffic();
                        break;
                    case 32936:
                        ShowChart();
                        break;
                    case 32946:
                        if (chart != null && doc != null)
                        {
                            chart.Unlink(doc, doc.SelectedRawIndex);
                        }

                        break;
                    case 32949:
                        if (chart != null)
                        {
                            chart.UnlinkAll();
                        }

                        break;
                    case 32901:
                        if (doc != null)
                        {
                            doc.ChooseColors();
                        }

                        break;
                    case 32876:
                        if (doc != null)
                        {
                            doc.ChooseFont();
                        }

                        break;
                    case 32929:
                        Scaling(doc);
                        break;
                    case 32778:
                    case 32779:
                        if (doc != null)
                        {
                            doc.State.BaseOne = id == 32778;
                            doc.Rebuild();
                            MarkDirty();
                        }

                        break;
                    case 32862:
                        if (doc != null)
                        {
                            doc.State.ShowErrors = !doc.State.ShowErrors;
                            doc.UpdateSummary();
                        }

                        break;
                    case 59392:
                        tools.Visible = !tools.Visible;
                        break;
                    case 59393:
                        status.Visible = !status.Visible;
                        break;
                    case 32861:
                        TopMost = !TopMost;
                        break;
                    case 57650:
                        LayoutMdi(MdiLayout.Cascade);
                        break;
                    case 57651:
                        LayoutMdi(MdiLayout.TileVertical);
                        break;
                    case 57649:
                        LayoutMdi(MdiLayout.ArrangeIcons);
                        break;
                    case 57667:
                    case 57670:
                        ShowHelpTopics();
                        break;
                    case 57664:
                        ShowAbout();
                        break;
                    default:
                        int format, order;
                        if (FindFormat(id, out format, out order) && doc != null)
                        {
                            doc.ChooseFormat(format, order);
                        }
                        else if (doc != null && ((id >= 32942 && id <= 32945) || (id >= 32950 && id <= 32957)))
                        {
                            ShowChart();
                            chart.Link(id <= 32945 ? id - 32942 : id - 32950 + 4, doc, doc.SelectedRawIndex);
                        }

                        break;
                }

                RefreshChecks();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "ModbusGetFree", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        internal bool FindFormat(int id, out int format, out int order)
        {
            int[] starts =
            {
                32905,
                32909,
                32925,
                32917,
                32913,
                32921
            };
            int[] formats =
            {
                4,
                5,
                7,
                8,
                6,
                9
            };
            int[] orders =
            {
                0,
                1,
                2,
                3
            };
            for (int i = 0; i < starts.Length; i++)
            {
                if (id >= starts[i] && id < starts[i] + 4)
                {
                    format = formats[i];
                    order = orders[id - starts[i]];
                    return true;
                }
            }

            format = order = 0;
            return false;
        }

        public async Task QuickConnect()
        {
            if (Bus.Connected || InFlight)
            {
                return;
            }

            try
            {
                Bus.Open(Settings);
                foreach (var d in MdiChildren.OfType<DataWindow>())
                {
                    d.NextPoll = DateTime.UtcNow;
                }

                UpdateConnection();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Quick Connect");
            }

            await Task.FromResult(0);
        }

        void ShowChart()
        {
            if (chart == null || chart.IsDisposed)
            {
                chart = new ChartWindow();
            }

            ShowToolWindow(chart);
            chart.Activate();
        }

        void SampleChart(DataWindow doc)
        {
            if (chart != null && !chart.IsDisposed)
            {
                chart.Sample(doc);
            }
        }

        void OpenFunction(int fn)
        {
            ShowToolWindow(new FunctionDialog(this, fn));
        }

    }
}
