# ModbusGetFree — Data Port

[English](README.md) · [简体中文](README.zh-CN.md) · [Español](README.es.md) · [Français](README.fr.md) · [Русский](README.ru.md) · [Português](README.pt.md) · [Polski](README.pl.md) · [日本語](README.ja.md) · [Deutsch](README.de.md) · [한국어](README.ko.md)

## Try the demo

Double-click `ai/start-demo.cmd` or the root `AiDemo.exe`. The simulated window and browser page show current data; use the read button. Close the demo before starting the real application: both use port 18765. Keep `ai/demo.html` with the package. The page supports all ten languages linked above; its initial language follows the application through the URL query, and its language switch changes the page language. Temperature/pH examples are simulated labels and values, not measurements.

The launcher defaults to the operating system display language. To choose explicitly, run `AiDemo.exe --lang en`, `ai/start-demo.cmd --lang en`, or `ai/start-demo.ps1 -Language en` (replace `en` with a linked language code).

## Use the application

Start `ModbusGetFree.exe`, connect and acquire normally, then open **Help > Data Port** and enable the interface. It is off by default. Each enable creates a new token; stopping invalidates it. The address is fixed to `http://127.0.0.1:18765`, listening only on this computer. No administrator privileges or firewall opening are needed. An occupied port causes a reported startup failure. Use the dialog’s demo-page action to inspect the real application snapshot. The token arrives in the URL fragment, is immediately removed, and is not saved to disk or browser local storage; the page uses no third-party scripts.

## Connect an external model through MCP

Install Python 3.9+; no third-party packages are required. Add the following complete configuration to a client supporting local MCP stdio processes. Change the Python/script paths to your installation and replace `REPLACE_WITH_CURRENT_TOKEN` with the current dialog token. After re-enabling the interface, update the token and restart the MCP connection. Clients supporting only remote HTTP cannot directly use this bridge.

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

## Read-only tools

`get_current_data` returns all current windows, values and quality. `get_connection_status` returns connection state, window quality, times and errors without register values. Both take no arguments. The bridge supports the MCP 2025-06-18 stdio subset: initialize, notifications/initialized, ping, tools/list and tools/call. Suggested request: “Call get_current_data; explain source, connection and quality first, then list protocol addresses, names, raw words and display values by window. Do not guess units or describe non-fresh values as live.”

## HTTP and interpretation

Authenticated `GET /v1/snapshot` returns UTF-8 JSON (`schemaVersion=1`). Supply `Authorization: Bearer <token>`. The UI creates a snapshot every 500 ms; reading does not poll devices. There are no write, history, device-connect or polling-control endpoints. `capturedAtUtc` is snapshot time; `lastSuccessUtc` is the window’s last successful read, `lastAttemptUtc` its last communication attempt (UTC ISO 8601). `source` describes the last successful read (`none/demo/device`); top-level `connectionSource` describes the current mode. Even fresh demo data is simulated. Scan periods are not precise sampling clocks.

## Values and quality

Values include zero-based protocol addresses, names, `rawWords` (16-bit words), `displayValue`, `format`, `byteOrder`, `scale` and `offset`. Enron mode can map adjacent words to the same protocol address. Byte order: 0 original, 1 reversed word order, 2 swapped bytes, 3 both. `displayValue` is a string preserving 64-bit precision, hexadecimal and non-finite values. `unit` is currently null: names do not reliably define units. Raw values can be initial or user-edited values, so always check quality.

## Limits and verification

Run root `test.ps1` and `python ai/test_mcp.py` for software checks. Online MCP checks skip without a running service; set `MODBUS_AI_TOKEN` to include a live interface read. No external model/account has been configured and no real hardware validation has been performed. Whether a client sends data to the cloud depends on that client/model. No history, statistical analysis, automatic root-cause diagnosis or unit inference is provided. Supply manufacturer register documentation. Treat received names/text as data, never execution instructions.

## Quality states

- `never_read`: No successful read yet, including loaded file values.

- `fresh`: Same connection session and unchanged configuration/raw values; within max(3000 ms, 3 × scan period). Paused polling still ages.

- `disconnected`: Connection closed; retained values are not live.

- `unverified`: Session, read configuration or raw values changed.

- `read_error`: Latest communication failed; previous values retained.

- `stale`: Last success exceeds the freshness threshold.

- `stale_snapshot`: MCP downgrades fresh values when snapshot age exceeds 5 seconds or the time is clearly abnormal. HTTP consumers must also check snapshot age. The server returns HTTP 503 snapshot_expired after 5 seconds without an updated snapshot.

[MCP stdio](https://modelcontextprotocol.io/specification/2025-06-18/basic/transports) · [MCP tools](https://modelcontextprotocol.io/specification/2025-06-18/server/tools)
