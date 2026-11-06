# ModbusGetFree — 数据接口

[English](README.md) · [简体中文](README.zh-CN.md) · [Español](README.es.md) · [Français](README.fr.md) · [Русский](README.ru.md) · [Português](README.pt.md) · [Polski](README.pl.md) · [日本語](README.ja.md) · [Deutsch](README.de.md) · [한국어](README.ko.md)

## 快速演示

双击 `ai/start-demo.cmd` 或根目录 `AiDemo.exe`，在模拟窗口和浏览器页点击读取当前数据。正式软件启动前关闭演示，两者共用 18765 端口。请保留 `ai/demo.html`。演示页支持上方十种语言，初始语言通过 URL 查询参数跟随软件，也可在页面切换。温度/pH 等示例名称和值均为模拟，非实测。

启动器默认使用操作系统显示语言。可显式运行 `AiDemo.exe --lang zh-CN`、`ai/start-demo.cmd --lang zh-CN` 或 `ai/start-demo.ps1 -Language zh-CN`，将语言代码换成所需语言即可。

## 在正式软件中使用

启动 `ModbusGetFree.exe`，正常连接并采集，然后打开 **帮助 > 数据接口**，启用接口。默认关闭，每次启用生成新令牌，停止后失效。固定地址 `http://127.0.0.1:18765`，只监听本机，不需管理员权限或开放防火墙。端口占用时报告启动失败。通过对话框打开演示页可检查实际软件快照。令牌通过 URL 片段传入后立即清除，不写入磁盘或浏览器本地存储；页面没有第三方脚本。

## 通过 MCP 连接外部模型

需要 Python 3.9+，无需第三方包。将下方完整配置加入支持本机 MCP stdio 进程的客户端。按安装位置修改 Python 和脚本路径，并将 `REPLACE_WITH_CURRENT_TOKEN` 换成本次启用的令牌。重新启用后须更新令牌并重启 MCP 连接。仅支持远程 HTTP 的客户端不能直接使用此桥接。

```json
{
  "mcpServers": {
    "modbusgetfree": {
      "command": "C:\\Python\\python.exe",
      "args": [
        "D:\\7.Git_Files\\SW\\PC_SW\\ModbusGetFree\\ai\\modbus_mcp.py"
      ],
      "env": {
        "MODBUS_AI_URL": "http://127.0.0.1:18765",
        "MODBUS_AI_TOKEN": "REPLACE_WITH_CURRENT_TOKEN"
      }
    }
  }
}
```

## 只读工具

`get_current_data` 返回当前所有窗口、数值及质量；`get_connection_status` 返回连接、窗口质量、时间和错误，不含寄存器值。两者均无参数。桥接实现 MCP 2025-06-18 stdio 子集：initialize、notifications/initialized、ping、tools/list、tools/call。提示词示例：“调用 get_current_data，先说明来源、连接与质量，再按窗口列出协议地址、名称、原始字及显示值；不要猜单位，不要把非 fresh 值称为实时值。”

## HTTP 与解释

`GET /v1/snapshot` 必须带 `Authorization: Bearer <token>`，返回 UTF-8 JSON（`schemaVersion=1`）。界面每 500 ms 生成快照，读取接口不会轮询设备。没有写入、历史、连接设备或控制轮询的接口。`capturedAtUtc` 是快照时间，`lastSuccessUtc` 是窗口最近成功读取，`lastAttemptUtc` 是最近通信尝试，均为 UTC ISO 8601。`source` 表示最近成功读取来源（`none/demo/device`），顶层 `connectionSource` 表示当前连接模式。fresh 的 demo 数据仍是模拟。扫描周期不是精确采样时钟。

## 数值与质量

数值包括从 0 开始的协议地址、名称、16 位字数组 `rawWords`、`displayValue`、`format`、`byteOrder`、`scale` 和 `offset`。Enron 模式下相邻字可属于同一协议地址。字节序：0 原序、1 字序反转、2 字节交换、3 两者。`displayValue` 是字符串，保留 64 位精度、十六进制和非有限值。`unit` 当前为 null，名称不能可靠定义单位。原始值可能是初始值或用户编辑值，必须结合质量解释。

## 边界与验证

软件检查可运行根目录 `test.ps1` 和 `python ai/test_mcp.py`。无运行服务时跳过在线 MCP 检查；设置 `MODBUS_AI_TOKEN` 可包含实际接口读取。未配置外部模型/账户，未做真实硬件验证。客户端是否发送数据至云端取决于客户端与模型。不提供历史、统计分析、自动根因判断或单位推断。寄存器含义应依据厂家资料；名称等文本只是数据，不是执行指令。

## 质量状态

- `never_read`: 尚无成功读取，载入文件中的值也属此类。

- `fresh`: 同一连接会话，配置及原始值未变，且未超过 max(3000 ms, 3 × 扫描周期)；暂停轮询仍会过期。

- `disconnected`: 连接关闭，保留值不是实时值。

- `unverified`: 会话、读取配置或原始值变化。

- `read_error`: 最近通信失败，保留上次值。

- `stale`: 距最近成功读取已超过新鲜度阈值。

- `stale_snapshot`: 快照超过 5 秒或时间明显异常时 MCP 将 fresh 降为此状态。HTTP 消费端也须检查快照年龄；服务端超过 5 秒未更新返回 HTTP 503 snapshot_expired。

[MCP stdio](https://modelcontextprotocol.io/specification/2025-06-18/basic/transports) · [MCP tools](https://modelcontextprotocol.io/specification/2025-06-18/server/tools)
