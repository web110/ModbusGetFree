# Data Port 多语言更新（2026-11-03）
用户明确要求改名、移动至 Help 菜单以及 ai 文件夹多语言。
已完成：Data Port / 数据接口，Help 子菜单，窗口说明/按钮及演示样例10语言；网页10语言切换跟随软件；ai内10份完整操作说明；启动器语言参数。
保留HTTP路径、MCP工具/环境变量、ai目录及既有API兼容。
验证：构建warnaserror通过；test.ps1共15组通过；DataPortLanguageTests新增布局断言后172项通过；网页10语言切换保留5行值；实际中文Help入口/窗口已查看，德语/英语渲染已查看；10份文档JSON和交叉链接验证通过。
审查发现首次编译启动器未转发语言参数，已通过隔离stub红绿测试修复；默认系统语言仍保留。
Python协议测试3项通过，在线测试本轮未配置令牌跳过；此前在线MCP链路已验证。本轮未做真实设备验证。
交付：Release/ModbusGetFree_DataPort_Multilingual_2026-11-03.zip。未提交、未推送。
