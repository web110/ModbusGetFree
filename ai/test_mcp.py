import json
import os
from pathlib import Path
import subprocess
import sys
import unittest
import modbus_mcp


class ProtocolTests(unittest.TestCase):
    def test_initialization_required(self):
        reply = modbus_mcp.handle({"jsonrpc": "2.0", "id": 1, "method": "tools/list"}, {})
        self.assertEqual(reply["error"]["code"], -32002)

    def test_stdio_lifecycle_and_tools(self):
        messages = [
            {"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {"protocolVersion": "2025-06-18"}},
            {"jsonrpc": "2.0", "method": "notifications/initialized"},
            {"jsonrpc": "2.0", "id": 2, "method": "tools/list"},
            {"jsonrpc": "2.0", "id": 3, "method": "tools/call", "params": {"name": "write_register"}},
            {"jsonrpc": "2.0", "id": 4, "method": "tools/call", "params": {"name": "get_current_data"}},
        ]
        env = dict(os.environ, MODBUS_AI_TOKEN="")
        run = subprocess.run([sys.executable, str(Path(__file__).with_name("modbus_mcp.py"))],
                             input="\n".join(json.dumps(m) for m in messages), text=True,
                             capture_output=True, env=env, timeout=10)
        self.assertEqual(run.returncode, 0, run.stderr)
        replies = [json.loads(line) for line in run.stdout.splitlines()]
        self.assertEqual(len(replies), 4)
        self.assertEqual(replies[0]["result"]["protocolVersion"], "2025-06-18")
        self.assertEqual(len(replies[1]["result"]["tools"]), 2)
        self.assertTrue(all(t["annotations"]["readOnlyHint"] for t in replies[1]["result"]["tools"]))
        self.assertEqual(replies[2]["error"]["code"], -32602)
        self.assertTrue(replies[3]["result"]["isError"])

    def test_invalid_messages(self):
        for message in [None, [], {"jsonrpc": "1.0"}]:
            self.assertEqual(modbus_mcp.handle(message, {})["error"]["code"], -32600)
        run = subprocess.run([sys.executable, str(Path(__file__).with_name("modbus_mcp.py"))],
                             input=b"bad json\n", capture_output=True, timeout=5)
        self.assertEqual(json.loads(run.stdout)["error"]["code"], -32700)

    def test_live_server(self):
        if not os.environ.get("MODBUS_AI_TOKEN"):
            self.skipTest("live demo not running")
        state = {"ready": True}
        for name in ("get_current_data", "get_connection_status"):
            reply = modbus_mcp.handle({"jsonrpc": "2.0", "id": 1, "method": "tools/call", "params": {"name": name}}, state)
            self.assertFalse(reply["result"]["isError"], reply)
            data = json.loads(reply["result"]["content"][0]["text"])
            self.assertTrue(data["readOnly"])
            self.assertTrue(data["connected"])
            self.assertEqual(data["connectionSource"], "demo")
            self.assertGreater(len(data["windows"]), 0)
            if name == "get_connection_status":
                self.assertNotIn("values", data["windows"][0])


if __name__ == "__main__":
    unittest.main()
