namespace ModbusGetFree
{
    /// <summary>
    /// 声明式菜单节点模型：文本、命令标识及可选子菜单。
    /// </summary>
    public class MenuNode
    {
        public string Text;
        public int Id;
        public MenuNode[] Children;

        public MenuNode(string text, int id, MenuNode[] children = null)
        {
            Text = text;
            Id = id;
            Children = children;
        }
    }
}
