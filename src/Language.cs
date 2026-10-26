using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;

namespace ModbusGetFree
{
    // Only presentation text is translated. Editable fields and register cells are never changed.
    public static partial class Language
    {
        static readonly Dictionary<string, string> zh = new Dictionary<string, string>();
        static readonly Dictionary<string, string> en = new Dictionary<string, string>();
        static DateTime lastRefresh;
        static Language()
        {
            foreach (string line in Catalog.Split('\n'))
            {
                int split = line.IndexOf('=');
                if (line.Contains(".=")) split = line.IndexOf(".=", StringComparison.Ordinal) + 1;
                if (split < 1) continue;
                string key = line.Substring(0, split).TrimEnd('\r');
                string value = line.Substring(split + 1).TrimEnd('\r');
                zh[key] = value;
                en[value] = key;
                // Tooltips use the same captions without menu access-key markers.
                string plainKey = key.Replace("&", "").Trim();
                string plainValue = Regex.Replace(value, @"\(&.\)", "").Replace("&", "");
                if (plainKey != key && !zh.ContainsKey(plainKey)) zh[plainKey] = plainValue;
                if (plainKey != key && !en.ContainsKey(plainValue)) en[plainValue] = plainKey;
            }
            InitializeUpdates();
            InitializeAdditional();
        }
        public static string Text(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            string original;
            if (!zh.ContainsKey(value) && en.TryGetValue(value, out original)) value = original;
            var prefixes = new List<string> { "Error: ", "错误: " };
            foreach (string label in additional["error"]) prefixes.Add(label + ": ");
            foreach (string prefix in prefixes)
                if (value.StartsWith(prefix, StringComparison.Ordinal))
                    return Text("Error") + ": " + Text(value.Substring(prefix.Length));
            string translated;
            if (Chinese && zh.TryGetValue(value, out translated)) return translated;
            Dictionary<string, string> catalog;
            if (catalogs.TryGetValue(Code, out catalog))
            {
                if (catalog.TryGetValue(value, out translated)) return translated;
                return TranslateAdditional(value, Array.IndexOf(Codes, Code) - 1);
            }
            if (value.EndsWith(":")) return Text(value.Substring(0, value.Length - 1)) + ":";
            return value;
        }
        public static void Initialize()
        {
            Application.Idle += (s, e) =>
            {
                if ((DateTime.UtcNow - lastRefresh).TotalMilliseconds < 100) return;
                Refresh();
            };
        }
        public static void Refresh()
        {
            lastRefresh = DateTime.UtcNow;
            foreach (Form form in Application.OpenForms) Apply(form);
        }
        sealed class ItemText
        {
            public string OriginalText, OriginalTip, LastText, LastTip;
            public bool FollowText;
            public bool WidthAdjusted;
        }
        static readonly ConditionalWeakTable<ToolStripItem, ItemText> itemTexts =
            new ConditionalWeakTable<ToolStripItem, ItemText>();
        static readonly ConditionalWeakTable<Control, ItemText> controlTexts =
            new ConditionalWeakTable<Control, ItemText>();
        static readonly ConditionalWeakTable<ComboBox, Dictionary<int, ItemText>> comboTexts =
            new ConditionalWeakTable<ComboBox, Dictionary<int, ItemText>>();
        static readonly ConditionalWeakTable<DataGridViewColumn, ItemText> columnTexts =
            new ConditionalWeakTable<DataGridViewColumn, ItemText>();
        public static void Items(ToolStripItemCollection items)
        {
            foreach (ToolStripItem item in items)
            {
                var source = itemTexts.GetOrCreateValue(item);
                if (source.OriginalText == null) source.FollowText = item.AutoToolTip && item.ToolTipText == item.Text;
                if (source.OriginalText == null || item.Text != source.LastText)
                    source.OriginalText = item.Text;
                if (source.OriginalTip == null || item.ToolTipText != source.LastTip)
                    source.OriginalTip = item.ToolTipText;
                if (source.FollowText) source.OriginalTip = source.OriginalText;
                source.LastText = Text(source.OriginalText);
                source.LastTip = Text(source.OriginalTip);
                if (item.Text != source.LastText) item.Text = source.LastText;
                if (item.ToolTipText != source.LastTip) item.ToolTipText = source.LastTip;
                var drop = item as ToolStripDropDownItem;
                if (drop != null) Items(drop.DropDownItems);
            }
        }
        public static void Apply(Control control)
        {
            var dialog = control as Form;
            if (dialog != null && dialog.FormBorderStyle == FormBorderStyle.FixedDialog
                && Code != "en" && Code != "zh-CN")
            {
                var layout = controlTexts.GetOrCreateValue(dialog);
                if (!layout.WidthAdjusted)
                {
                    layout.WidthAdjusted = true;
                    dialog.Scale(new System.Drawing.SizeF(1.4f, 1));
                }
            }
            var number = control as NumericUpDown;
            if (number != null && number.FindForm() is FunctionDialog && number.Controls.Count > 1)
            {
                number.Controls[0].Visible = false;
                number.Controls[1].Bounds = new System.Drawing.Rectangle(1, 1,
                    number.ClientSize.Width - 2, number.ClientSize.Height - 2);
            }
            if (!(control is TextBoxBase) && !(control is ComboBox) && !(control is NumericUpDown)
                && !(control is DataWindow) && !(control is MainWindow))
            {
                var source = controlTexts.GetOrCreateValue(control);
                if (source.OriginalText == null || control.Text != source.LastText) source.OriginalText = control.Text;
                source.LastText = Text(source.OriginalText);
                if (control.Text != source.LastText) control.Text = source.LastText;
            }
            var strip = control as ToolStrip;
            if (strip != null) Items(strip.Items);
            if (control.ContextMenuStrip != null) Items(control.ContextMenuStrip.Items);
            var grid = control as DataGridView;
            if (grid != null)
                foreach (DataGridViewColumn column in grid.Columns)
                {
                    var source = columnTexts.GetOrCreateValue(column);
                    if (source.OriginalText == null || column.HeaderText != source.LastText) source.OriginalText = column.HeaderText;
                    source.LastText = Text(source.OriginalText);
                    if (column.HeaderText != source.LastText) column.HeaderText = source.LastText;
                }
            var combo = control as ComboBox;
            if (combo != null && combo.DropDownStyle == ComboBoxStyle.DropDownList)
                for (int i = 0; i < combo.Items.Count; i++)
                    if (combo.Items[i] is string)
                    {
                        string current = (string)combo.Items[i];
                        var entries = comboTexts.GetOrCreateValue(combo);
                        ItemText source;
                        if (!entries.TryGetValue(i, out source)) entries[i] = source = new ItemText();
                        if (source.OriginalText == null || current != source.LastText) source.OriginalText = current;
                        string next = Text(source.OriginalText);
                        source.LastText = next;
                        if (current != next) combo.Items[i] = next;
                    }
            // Translate only application output lists, never editable commands or register values.
            var list = control as ListBox;
            if (list != null && (list.Name == "TrafficLog" || list.Name == "ReadValues"))
                for (int i = 0; i < list.Items.Count; i++)
                {
                    string current = list.Items[i] as string;
                    if (current != null && Text(current) != current) list.Items[i] = Text(current);
                }
            var response = control as TextBox;
            if (response != null && response.ReadOnly && response.Name == "SlaveResponse")
                response.Text = Text(response.Text);
            foreach (Control child in control.Controls) Apply(child);
        }
        const string Catalog = @"Error=错误
Coil=线圈
PLC address=PLC 地址
Mask bits (15 to 0)=掩码位（15 到 0）
Mask bit=掩码位
Values text=数值文本
Command list (*.txt)=命令列表 (*.txt)
All files=所有文件
Tx=发送
Err=错误
ID=从站
F=功能码
SR=周期
Last=耗时
Disabled=已禁用
N/A=暂无
Write Single Coil=写单线圈
Write Single Register=写单寄存器
15: Write Multiple Coils=15：写多个线圈
16: Write Multiple Registers=16：写多个寄存器
17: Report Slave ID=17：报告从站标识
22: Mask Write Register=22：掩码写寄存器
23: Read/Write Multiple Registers=23：读写多个寄存器
Modbus Function=Modbus 功能
Write int 32=写入32位有符号整数
Write unsigned int 32=写入32位无符号整数
Write float 32=写入32位浮点数
Write int 64=写入64位有符号整数
Write unsigned int 64=写入64位无符号整数
Write double 64=写入64位双精度数
Open list=打开列表
Save list=保存列表
Add to list=添加到列表
Enter 2..256 bytes, starting with slave ID and function.=请输入 2～256 个字节，前两个字节为从站地址和功能码。
Frame exceeds 256 bytes.=报文超过 256 个字节。
Invalid command length.=命令长度无效。
Command list limit is 1000.=命令列表最多允许 1000 条。
Command list exceeds 1 MB.=命令列表文件超过 1 MB。
Connect before sending a command.=发送命令前请先连接。
Bus busy. Please try again.=通信忙，请稍后重试。
Broadcast sent (no response).=广播已发送（无响应）。
Broadcast sent=广播已发送
Waiting for response...=正在等待响应...
Write Address=写入地址
Write Quantity=写入数量
Read Address=读取地址
Read Quantity=读取数量
Edit=编辑
&Open=打开(&O)
AND Mask=AND 掩码
OR Mask=OR 掩码
Signed 16-bit=16位有符号整数
Unsigned 16-bit=16位无符号整数
Signed 32-bit=32位有符号整数
Unsigned 32-bit=32位无符号整数
Float 32-bit=32位浮点数
Signed 64-bit=64位有符号整数
Unsigned 64-bit=64位无符号整数
Double 64-bit=64位双精度数
Hex=十六进制
Values file is too large.=数值文件过大。
Invalid value count, coil value or address range.=数值数量、线圈值或地址范围无效。
Value needs more registers; increase quantity.=该数值需要更多寄存器，请增加数量。
This function requires slave ID 1..247.=此功能要求从站地址为 1～247。
Invalid register address.=寄存器地址无效。
Invalid read/write range.=读写范围无效。
Unsupported function.=不支持此功能码。
Int64 Little-endian byte swap=64位有符号整数 小端字节交换
Int64 Little-endian=64位有符号整数 小端
Int64 Big-endian=64位有符号整数 大端
Int64 Big-endian byte swap=64位有符号整数 大端字节交换
UInt32 Little-endian byte swap=32位无符号整数 小端字节交换
UInt32 Little-endian=32位无符号整数 小端
UInt32 Big-endian=32位无符号整数 大端
UInt32 Big-endian byte swap=32位无符号整数 大端字节交换
Float Little-endian byte swap=32位浮点数 小端字节交换
Float Little-endian=32位浮点数 小端
Float Big-endian=32位浮点数 大端
Float Big-endian byte swap=32位浮点数 大端字节交换
Double Little-endian byte swap=64位双精度数 小端字节交换
Double Little-endian=64位双精度数 小端
Double Big-endian=64位双精度数 大端
Double Big-endian byte swap=64位双精度数 大端字节交换
Int32 Little-endian byte swap=32位有符号整数 小端字节交换
Int32 Little-endian=32位有符号整数 小端
Int32 Big-endian=32位有符号整数 大端
Int32 Big-endian byte swap=32位有符号整数 大端字节交换
UInt64 Little-endian byte swap=64位无符号整数 小端字节交换
UInt64 Little-endian=64位无符号整数 小端
UInt64 Big-endian=64位无符号整数 大端
UInt64 Big-endian byte swap=64位无符号整数 大端字节交换
Fit to Quantity=按数量适配
(No recent files)=（无最近文件）
&File=文件(&F)
&New=新建(&N)
&Open...=打开(&O)...
&Close=关闭(&C)
&Save=保存(&S)
Save &As...=另存为(&A)...
Open &Workspace...=打开工作区(&W)...
Save Workspace...=保存工作区...
&Print...=打印(&P)...
Print Pre&view=打印预览(&V)
P&rint Setup...=打印设置(&R)...
Recent File=最近文件
E&xit=退出(&X)
&Edit=编辑(&E)
Cu&t =剪切(&T)
&Copy =复制(&C)
&Paste=粘贴(&P)
Select &All=全选(&A)
&Connection=连接(&C)
&Connect...=连接(&C)...
&Disconnect=断开(&D)
Auto Connect=自动连接
&Enable=启用(&E)
D&isable=禁用(&I)
&Quick Connect=快速连接(&Q)
&Setup=设置(&S)
&Read/Write Definition...=读写定义(&R)...
Read/Write &Once=单次读写(&O)
Read/&Write Disabled=禁用读写(&W)
E&xcel Log...=Excel 日志(&X)...
Excel Logging Off=停止 Excel 日志
&Log...=日志(&L)...
Logging Off=停止日志
R&eset Counters=重置计数器(&E)
Reset &All Counters=重置全部计数器(&A)
&Use as Default=设为默认(&U)
F&unctions=功能(&U)
0&5: Write Single Coil...=05：写单线圈(&5)...
0&6: Write Single Register...=06：写单寄存器(&6)...
15: Write &Coils...=15：写多个线圈(&C)...
16: Write &Registers...=16：写多个寄存器(&R)...
17: Report &Slave ID...=17：报告从站标识(&S)...
22: &Mask Write Register...=22：掩码写寄存器(&M)...
2&3: Read/Write Registers...=23：读写寄存器(&3)...
&Test Center...=测试中心(&T)...
&Display=显示(&D)
&Signed=有符号(&S)
&Unsigned=无符号(&U)
&Hex - ASCII=十六进制 - ASCII(&H)
&Binary=二进制(&B)
Big-endian=大端
Little-endian=小端
Big-endian byte swap=大端字节交换
Little-endian byte swap=小端字节交换
32 Bit signed=32 位有符号
32 Bit Unsigned=32 位无符号
64 Bit Signed=64 位有符号
64 Bit Unsigned=64 位无符号
32 Bit Float=32 位浮点数
64 Bit Double=64 位双精度
&Communication...=通信记录(&C)...
&Real time Charting...=实时图表(&R)...
&Link to Chart=关联图表(&L)
&Unlink=取消关联(&U)
Unlink &All=取消全部关联(&A)
Colors...=颜色...
&Font...=字体(&F)...
Sc&aling...=数值换算(&A)...
PLC Addresses (Base &1)=PLC 地址（从 1 开始）(&1)
Protocol Addresses (Base &0)=协议地址（从 0 开始）(&0)
&Error Counters=错误计数器(&E)
&View=视图(&V)
&Toolbar=工具栏(&T)
&Status Bar=状态栏(&S)
&Always On Top=窗口置顶(&A)
&Window=窗口(&W)
&Cascade=层叠(&C)
&Tile=平铺(&T)
&Arrange Icons=排列图标(&A)
&Help=帮助(&H)
&Help Topics=帮助主题(&H)
&About ModbusGetFree...=关于 ModbusGetFree(&A)...
OK=确定
Cancel=取消
Apply=应用
Close=关闭
Send=发送
Clear=清除
Start=开始
Stop=停止
Save=保存
Copy=复制
Value=数值
Result=结果
Address=地址
Name=名称
Function=功能
Quantity=数量
Slave ID=从站地址
Scan Rate=扫描周期
Read/Write Definition=读写定义
Read/Write Once=单次读写
Disable=禁用
Read/Write Disabled=禁用读写
Disable on error=发生错误时禁用
Connection Setup — Modbus RTU=连接设置 — Modbus RTU
Connection=连接
Serial Port / Modbus RTU=串口 / Modbus RTU
Serial port=串口
Baud rate=波特率
Data bits=数据位
Parity=校验
Stop bits=停止位
Response timeout (ms)=响应超时（毫秒）
Delay between polls (ms)=轮询间隔（毫秒）
RS-485 direction=RS-485 方向控制
Control line=控制线
Adapter echo=适配器回显
Demo (no hardware)=演示（无需硬件）
RTS toggle=切换 RTS
DTR enable=启用 DTR
Remove local echo=移除本地回显
Use simulated RTU slave=使用模拟 RTU 从站
None=无
Odd=奇校验
Even=偶校验
Mark=标记校验
Space=空格校验
RTU requires 8 data bits. RTS timing depends on the adapter; automatic-direction RS-485 adapters are preferred.=RTU 需要 8 个数据位。RTS 时序取决于适配器，建议使用自动方向控制的 RS-485 适配器。
No Connection — press F3 to connect=未连接 — 按 F3 连接
Connected — RTU Demo=已连接 — RTU 演示
No connection=未连接
Connection Error=连接错误
Application Error=程序错误
Save Error=保存错误
Open Error=打开错误
Workspace=工作区
Save changes to the workspace?=是否保存工作区更改？
Disconnect before loading a workspace.=载入工作区前请断开连接。
Invalid baud rate.=波特率无效。
Modbus RTU requires 8 data bits.=Modbus RTU 需要 8 个数据位。
Select a serial port.=请选择串口。
Use Function=使用功能码
On=开
Off=关
Response ok=响应成功
Communication=通信记录
Test Center=测试中心
Real time Charting=实时图表
Scale=比例
Offset=偏移
Value Scaling — Selected Group=数值换算 — 所选分组
Scaling is available for 16-bit and 32-bit integer groups.=数值换算适用于 16 位和 32 位整数分组。
Displayed value = raw value × scale + offset. Writes apply the inverse transform.=显示值 = 原始值 × 比例 + 偏移。写入时执行反向换算。
Alias=别名
View=视图
Rows=行数
Hide Alias Columns=隐藏别名列
Address in Cell=在单元格显示地址
PLC Addresses (Base 1)=PLC 地址（从 1 开始）
Enron/Daniel Mode=Enron/Daniel 模式
Request=请求帧
Response=响应
Read Result=读取结果
Type=类型
Status=状态
Ready=就绪
Response OK=响应正常
Communication Traffic=通信记录
Log=日志
Log off=停止日志
Stop on Error=出错时停止捕获
Time stamp=时间戳
Exit=退出
Pause=暂停
Resume=继续
Export CSV=导出 CSV
Add Check=添加校验
Enter hex numbers separated by "","" ""."" or space=输入十六进制数，使用逗号、句点或空格分隔
Close dialog on ""Response ok""=响应正常后关闭对话框
05: Write single coil=05：写单线圈
06: Write single register=06：写单寄存器
15: Write multiple coils=15：写多个线圈
16: Write multiple registers=16：写多个寄存器
01 Read Coils (0x)=01 读线圈（0x）
02 Read Discrete Inputs (1x)=02 读离散输入（1x）
03 Read Holding Registers (4x)=03 读保持寄存器（4x）
04 Read Input Registers (3x)=04 读输入寄存器（3x）
05 Write Single Coil=05 写单线圈
06 Write Single Register=06 写单寄存器
15 Write Multiple Coils=15 写多个线圈
16 Write Multiple Registers=16 写多个寄存器
Text color...=文字颜色...
Background...=背景颜色...
Below color...=低于阈值的颜色...
Above color...=高于阈值的颜色...
Foreground=前景颜色
Background=背景颜色
Below threshold (blank: off)=下限阈值（空白为关闭）
Below threshold color=低于下限的颜色
Above threshold (blank: off)=上限阈值（空白为关闭）
Above threshold color=高于上限的颜色
Address (base 0)=地址（从 0 开始）
Value (selected Type)=数值（所选类型）
Discrete inputs and input registers are read-only.=离散输入和输入寄存器只可读取。
Incomplete value: increase register quantity.=数值不完整，请增加寄存器数量。
This data area is read-only.=该数据区只可读取。
Use finite values and nonzero scale.=请输入有限数值，比例不能为零。
Quick Connect=快速连接
Values=数值
Help Topics=帮助主题
About ModbusGetFree=关于 ModbusGetFree
ModbusGetFree — Modbus RTU desktop utility=ModbusGetFree — Modbus RTU 桌面工具
MIT license=MIT 许可证
GitHub project=GitHub 项目
Open GitHub=打开 GitHub
Copy link=复制链接
Context Help=上下文帮助
Format=格式
Read/write Definition...=读写定义...
Cut=剪切
Paste=粘贴
Select All=全选
Font...=字体...
Scaling...=数值换算...
Export CSV...=导出 CSV...
Select a value cell, then Display > Link to Chart > Series 1..12=选择一个数值单元格，然后依次点击“显示 > 关联图表 > 曲线 1..12”
Select a complete value cell.=请选择一个完整的数值单元格。
Series &1=曲线 &1
Series &2=曲线 &2
Series &3=曲线 &3
Series &4=曲线 &4
Series &5=曲线 &5
Series &6=曲线 &6
Series &7=曲线 &7
Series &8=曲线 &8
Series &9=曲线 &9
Series 10=曲线 10
Series 11=曲线 11
Series 12=曲线 12
";
    }
    public static class LocalMessageBox
    {
        public static DialogResult Show(string text, string caption)
        { return MessageBox.Show(Language.Text(text), Language.Text(caption)); }
        public static DialogResult Show(IWin32Window owner, string text)
        { return Show(owner, text, "ModbusGetFree"); }
        public static DialogResult Show(IWin32Window owner, string text, string caption)
        { return Show(owner, text, caption, MessageBoxButtons.OK); }
        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons)
        { return Show(owner, text, caption, buttons, MessageBoxIcon.None); }
        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        { return MessageBox.Show(owner, Language.Text(text), Language.Text(caption), buttons, icon); }
    }
}


