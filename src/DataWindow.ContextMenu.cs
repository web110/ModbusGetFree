using System;
using System.Linq;
using System.Windows.Forms;

namespace ModbusGetFree
{
    public partial class MainWindow
    {
        // 右键菜单复用主菜单命令；先激活来源窗口，防止操作到其他 MDI 子窗口。
        internal async System.Threading.Tasks.Task ExecuteContextCommand(DataWindow document, int command)
        {
            if (document.IsDisposed || document.MdiParent != this)
            {
                return;
            }

            document.Activate();
            await ExecuteCommand(command, document);
        }
    }

    public partial class DataWindow
    {
        /// <summary>
        /// 按 classic 排列数据表右键菜单，与主菜单共享命令和格式定义。
        /// </summary>
        ContextMenuStrip BuildContextMenu()
        {
            var context = new ContextMenuStrip { Font = UiFonts.Create() };
            var display = AppMenus.All().First(node => node.Text.Replace("&", "") == "Display");
            var format = new ToolStripMenuItem("Format");

            foreach (var node in display.Children)
            {
                if (node.Id == 32825)
                {
                    break;
                }

                format.DropDownItems.Add(BuildContextItem(node));
            }

            if (format.DropDownItems.Count > 0 &&
                format.DropDownItems[format.DropDownItems.Count - 1] is ToolStripSeparator)
            {
                format.DropDownItems.RemoveAt(format.DropDownItems.Count - 1);
            }

            context.Items.Add(format);
            context.Items.Add(new ToolStripSeparator());
            context.Items.Add(BuildContextItem(new MenuNode("Read/write Definition...\tF8", 32771)));
            context.Items.Add(new ToolStripSeparator());
            context.Items.Add(BuildContextItem(new MenuNode("Cut\tCtrl+X", 57635)));
            context.Items.Add(BuildContextItem(new MenuNode("Copy\tCtrl+C", 57634)));
            context.Items.Add(BuildContextItem(new MenuNode("Paste\tCtrl+V", 57637)));
            context.Items.Add(BuildContextItem(new MenuNode("Select All\tCtrl+A", 57642)));
            context.Items.Add(new ToolStripSeparator());
            context.Items.Add(BuildContextItem(new MenuNode("Colors...\tAlt+Shift+C", 32901)));
            context.Items.Add(BuildContextItem(new MenuNode("Font...\tAlt+Shift+F", 32876)));
            context.Items.Add(BuildContextItem(new MenuNode("Scaling...\tCtrl+Shift+S", 32929)));
            context.Items.Add(new ToolStripSeparator());
            context.Items.Add(BuildContextItem(display.Children.First(node => node.Text == "&Link to Chart")));
            context.Opening += (sender, args) => RefreshContextMenu();
            return context;
        }

        ToolStripItem BuildContextItem(MenuNode node)
        {
            if (node.Text.Length == 0)
            {
                return new ToolStripSeparator();
            }

            string[] parts = node.Text.Split('\t');
            var item = new ToolStripMenuItem(parts[0]) { Tag = node.Id };
            if (parts.Length > 1)
            {
                // 热键由主菜单统一处理；此处只显示提示，避免多个数据窗口注册重复热键。
                item.ShortcutKeyDisplayString = parts[1];
            }

            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    item.DropDownItems.Add(BuildContextItem(child));
                }
            }
            else
            {
                item.Click += async (sender, args) => await host.ExecuteContextCommand(this, node.Id);
            }

            return item;
        }

        public bool SelectContextCell(int row, int column)
        {
            if (row < 0 || column < 0 || row >= Grid.Rows.Count || column >= Grid.Columns.Count)
            {
                return false;
            }

            Activate();
            var cell = Grid[column, row];
            var selection = cell.Selected
                ? Grid.SelectedCells.Cast<DataGridViewCell>().ToArray()
                : new DataGridViewCell[0];
            Grid.ClearSelection();
            Grid.CurrentCell = cell;
            foreach (var selected in selection)
            {
                selected.Selected = true;
            }

            cell.Selected = true;
            return true;
        }

        public void RefreshContextMenu()
        {
            RefreshContextItems(Grid.ContextMenuStrip.Items);
            Language.Items(Grid.ContextMenuStrip.Items);
        }

        void RefreshContextItems(ToolStripItemCollection items)
        {
            foreach (ToolStripItem entry in items)
            {
                var item = entry as ToolStripMenuItem;
                if (item == null)
                {
                    continue;
                }

                int id = item.Tag is int ? (int)item.Tag : 0;
                int format;
                int order;
                if (id == 32775 || id == 32776 || id == 32777 || id == 32780)
                {
                    int basic = id == 32775 ? 0 : id == 32776 ? 1 : id == 32777 ? 2 : 3;
                    item.Checked = SelectedDefinition.Format == basic;
                    item.Enabled = !EnronActive;
                }
                else if (host.FindFormat(id, out format, out order))
                {
                    item.Checked = SelectedDefinition.Format == format && SelectedDefinition.Order == order;
                    item.Enabled = !EnronActive || ValueCodec.Width(format) == 2;
                }
                else if (id == 57637)
                {
                    try
                    {
                        item.Enabled = Clipboard.ContainsText();
                    }
                    catch (System.Runtime.InteropServices.ExternalException)
                    {
                        // 剪贴板可能正被其他程序占用；不应阻止整个右键菜单打开。
                        item.Enabled = false;
                    }
                }
                else if ((id >= 32942 && id <= 32945) || (id >= 32950 && id <= 32957))
                {
                    item.Enabled = SelectedRawIndex >= 0 && SelectedRawIndex + ValueWidth <= StorageQuantity;
                }

                RefreshContextItems(item.DropDownItems);
            }
        }
    }
}
