namespace ModbusGetFree
{
    /// <summary>
    /// 声明式 classic 菜单树。数字为菜单命令标识，不是 Modbus 功能码。
    /// </summary>
    public static class AppMenus
    {
        public static MenuNode[] All()
        {
            return new MenuNode[]
            {
                new MenuNode("&File", 0, new MenuNode[] {
                    new MenuNode("&New\tCtrl+N", 57600),
                    new MenuNode("&Open...\tCtrl+O", 57601),
                    new MenuNode("&Close", 57602),
                    new MenuNode("&Save\tCtrl+S", 57603),
                    new MenuNode("Save &As...", 57604),
                    new MenuNode("", 0),
                    new MenuNode("Open &Workspace...", 32859),
                    new MenuNode("Save Workspace...", 32858),
                    new MenuNode("", 0),
                    new MenuNode("&Print...\tCtrl+P", 57607),
                    new MenuNode("Print Pre&view", 57609),
                    new MenuNode("P&rint Setup...", 57606),
                    new MenuNode("", 0),
                    new MenuNode("Recent File", 57616),
                    new MenuNode("", 0),
                    new MenuNode("E&xit", 57665)
                }),
                new MenuNode("&Edit", 0, new MenuNode[] {
                    new MenuNode("Cu&t \tCtrl+X", 57635),
                    new MenuNode("&Copy \tCtrl+C", 57634),
                    new MenuNode("&Paste\tCtrl+V", 57637),
                    new MenuNode("Select &All\tCtrl+A", 57642)
                }),
                new MenuNode("&Connection", 0, new MenuNode[] {
                    new MenuNode("&Connect...\tF3", 32772),
                    new MenuNode("&Disconnect\tF4", 32773),
                    new MenuNode("", 0),
                    new MenuNode("Auto Connect", 0, new MenuNode[] { new MenuNode("&Enable",
                         32835), new MenuNode("D&isable", 32836) }),
                    new MenuNode("&Quick Connect\tF5", 32838)
                }),
                new MenuNode("&Setup", 0, new MenuNode[] {
                    new MenuNode("&Read/Write Definition...\tF8", 32771),
                    new MenuNode("Read/Write &Once\tF6", 32854),
                    new MenuNode("Read/&Write Disabled\tShift+F6", 32880),
                    new MenuNode("", 0),
                    new MenuNode("E&xcel Log...\tAlt+X", 32881),
                    new MenuNode("Excel Logging Off\tAlt+Q", 32882),
                    new MenuNode("", 0),
                    new MenuNode("&Log...\tAlt+L", 32808),
                    new MenuNode("Logging Off\tAlt+O", 32809),
                    new MenuNode("", 0),
                    new MenuNode("R&eset Counters\tF12", 32801),
                    new MenuNode("Reset &All Counters\t Shift+F12", 32900),
                    new MenuNode("", 0),
                    new MenuNode("&Use as Default", 32815)
                }),
                new MenuNode("F&unctions", 0, new MenuNode[] {
                    new MenuNode("0&5: Write Single Coil...\tAlt+F5", 32842),
                    new MenuNode("0&6: Write Single Register...\tAlt+F6", 32844),
                    new MenuNode("15: Write &Coils...\tAlt+F7", 32846),
                    new MenuNode("16: Write &Registers...\tAlt+F8", 32848),
                    new MenuNode("17: Report &Slave ID...", 32894),
                    new MenuNode("22: &Mask Write Register...", 32849),
                    new MenuNode("2&3: Read/Write Registers...", 32839),
                    new MenuNode("", 0),
                    new MenuNode("&Test Center...\tAlt+T", 32826)
                }),
                new MenuNode("&Display", 0, new MenuNode[] {
                    new MenuNode("&Signed\tAlt+Shift+S", 32775),
                    new MenuNode("&Unsigned\tAlt+Shift+U", 32776),
                    new MenuNode("&Hex - ASCII\tAlt+Shift+H", 32777),
                    new MenuNode("&Binary\tAlt+Shift+B", 32780),
                    new MenuNode("", 0),
                    new MenuNode("32 Bit signed", 0, new MenuNode[] {
                        new MenuNode("Big-endian", 32905),
                        new MenuNode("Little-endian", 32908),
                        new MenuNode("", 0),
                        new MenuNode("Big-endian byte swap", 32907),
                        new MenuNode("Little-endian byte swap", 32906)
                    }),
                    new MenuNode("32 Bit Unsigned", 0, new MenuNode[] {
                        new MenuNode("Big-endian", 32909),
                        new MenuNode("Little-endian", 32912),
                        new MenuNode("", 0),
                        new MenuNode("Big-endian byte swap", 32911),
                        new MenuNode("Little-endian byte swap", 32910)
                    }),
                    new MenuNode("64 Bit Signed", 0, new MenuNode[] {
                        new MenuNode("Big-endian", 32925),
                        new MenuNode("Little-endian", 32928),
                        new MenuNode("", 0),
                        new MenuNode("Big-endian byte swap", 32927),
                        new MenuNode("Little-endian byte swap", 32926)
                    }),
                    new MenuNode("64 Bit Unsigned", 0, new MenuNode[] {
                        new MenuNode("Big-endian", 32917),
                        new MenuNode("Little-endian", 32920),
                        new MenuNode("", 0),
                        new MenuNode("Big-endian byte swap", 32919),
                        new MenuNode("Little-endian byte swap", 32918)
                    }),
                    new MenuNode("", 0),
                    new MenuNode("32 Bit Float", 0, new MenuNode[] {
                        new MenuNode("Big-endian", 32913),
                        new MenuNode("Little-endian", 32916),
                        new MenuNode("", 0),
                        new MenuNode("Big-endian byte swap", 32915),
                        new MenuNode("Little-endian byte swap", 32914)
                    }),
                    new MenuNode("64 Bit Double", 0, new MenuNode[] {
                        new MenuNode("Big-endian", 32921),
                        new MenuNode("Little-endian", 32924),
                        new MenuNode("", 0),
                        new MenuNode("Big-endian byte swap", 32923),
                        new MenuNode("Little-endian byte swap", 32922)
                    }),
                    new MenuNode("", 0),
                    new MenuNode("&Communication...", 32825),
                    new MenuNode("", 0),
                    new MenuNode("&Real time Charting...\tAlt + R", 32936),
                    new MenuNode("&Link to Chart", 0, new MenuNode[] {
                        new MenuNode("Series &1", 32942),
                        new MenuNode("Series &2", 32943),
                        new MenuNode("Series &3", 32944),
                        new MenuNode("Series &4", 32945),
                        new MenuNode("Series &5", 32950),
                        new MenuNode("Series &6", 32951),
                        new MenuNode("Series &7", 32952),
                        new MenuNode("Series &8", 32953),
                        new MenuNode("Series &9", 32954),
                        new MenuNode("Series 10", 32955),
                        new MenuNode("Series 11", 32956),
                        new MenuNode("Series 12", 32957),
                        new MenuNode("", 0),
                        new MenuNode("&Unlink", 32946),
                        new MenuNode("Unlink &All", 32949)
                    }),
                    new MenuNode("", 0),
                    new MenuNode("Colors...\tAlt+Shift+C", 32901),
                    new MenuNode("&Font...\tAlt+Shift+F", 32876),
                    new MenuNode("Sc&aling...\tCtrl+Shift+S", 32929),
                    new MenuNode("", 0),
                    new MenuNode("PLC Addresses (Base &1)", 32778),
                    new MenuNode("Protocol Addresses (Base &0)", 32779),
                    new MenuNode("", 0),
                    new MenuNode("&Error Counters\tF11", 32862)
                }),
                new MenuNode("&View", 0, new MenuNode[] {
                    new MenuNode("&Toolbar", 59392),
                    new MenuNode("&Status Bar", 59393),
                    new MenuNode("&Always On Top", 32861)
                }),
                new MenuNode("&Window", 0, new MenuNode[] {
                    new MenuNode("&Cascade", 57650),
                    new MenuNode("&Tile", 57651),
                    new MenuNode("&Arrange Icons", 57649)
                }),
                new MenuNode("&Help", 0, new MenuNode[] {
                    new MenuNode("&Help Topics", 57667),
                    new MenuNode("", 0),
                    new MenuNode("&About ModbusGetFree...", 57664)
                })
            };
        }
    }
}
