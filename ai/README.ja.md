# ModbusGetFree — データポート

[English](README.md) · [简体中文](README.zh-CN.md) · [Español](README.es.md) · [Français](README.fr.md) · [Русский](README.ru.md) · [Português](README.pt.md) · [Polski](README.pl.md) · [日本語](README.ja.md) · [Deutsch](README.de.md) · [한국어](README.ko.md)

## デモ

`ai/start-demo.cmd` またはルートの `AiDemo.exe` を起動し、ページの読み取りボタンを押します。本番アプリを起動する前にデモを終了してください。両方がポート18765を使用します。`ai/demo.html` は保持してください。ページは上記10言語に対応し、初期言語はURLクエリでアプリから引き継ぎ、ページでも切り替えられます。温度/pHなどの名前と値は模擬データで、実測ではありません。

起動時の既定はOSの表示言語です。`AiDemo.exe --lang ja`、`ai/start-demo.cmd --lang ja`、または `ai/start-demo.ps1 -Language ja` で明示できます。必要に応じて言語コードを変更してください。

## 本番アプリ

`ModbusGetFree.exe` で通常どおり接続・収集し、**ヘルプ > データポート** で有効にします。既定は無効です。有効化のたびに新しいトークンを生成し、停止すると無効になります。`http://127.0.0.1:18765` のローカルのみで待ち受け、管理者権限やファイアウォールの開放は不要です。ポート使用中は起動失敗を表示します。ダイアログからページを開くと実アプリのスナップショットを確認できます。トークンはURLフラグメントで渡され直ちに削除され、ディスクやブラウザーのローカルストレージには保存されません。外部スクリプトはありません。

## MCPクライアント

Python 3.9以上が必要で、追加パッケージは不要です。ローカルMCP stdioプロセスに対応するクライアントに以下の完全な設定を追加します。Pythonとスクリプトのパスを変更し、`REPLACE_WITH_CURRENT_TOKEN` を現在のトークンに置き換えます。再有効化後はトークンを更新し、MCP接続を再起動します。リモートHTTPのみに対応するクライアントでは直接使えません。

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

## ツール

`get_current_data` は現在の全ウィンドウ、値、品質を返します。`get_connection_status` は接続、品質、時刻、エラーを返し、レジスター値は含みません。どちらも引数はありません。MCP 2025-06-18 stdioの対応範囲は initialize、notifications/initialized、ping、tools/list、tools/call です。依頼例：「get_current_dataを呼び、最初に出所・接続・品質を説明し、各ウィンドウのアドレス、名前、元のワード、表示値を整理してください。単位を推測せず、fresh以外を現在値と呼ばないでください」。

## HTTPとデータ

`GET /v1/snapshot` は `Authorization: Bearer <token>` が必須で、UTF-8 JSON（`schemaVersion=1`）を返します。画面は500 msごとにスナップショットを生成します。読み取り自体は機器をポーリングしません。書き込み、履歴、機器接続、ポーリング制御のAPIはありません。`capturedAtUtc` はスナップショット時刻、`lastSuccessUtc` は最後の成功、`lastAttemptUtc` は最後の通信試行です（UTC ISO 8601）。`source` は最後に成功した出所（`none/demo/device`）、`connectionSource` は現在の接続モードです。freshでもdemoは模擬値です。スキャン周期は精密なサンプリング時計ではありません。

## 解釈と制限

アドレスは0始まりです。名前、16ビットの `rawWords`、`displayValue`、`format`、`byteOrder`、`scale`、`offset` を含みます。Enronでは隣接ワードが同じアドレスの場合があります。順序は0元の順序、1ワード逆順、2バイト交換、3両方です。`displayValue` は64ビット精度・16進数・非有限値を保つ文字列です。`unit` はnullで、名前から単位は確定できません。初期値や編集値の場合もあるため品質を確認します。ルートの `test.ps1` と `python ai/test_mcp.py` でソフトを検査できます。サービスなしではオンライン検査を省略し、`MODBUS_AI_TOKEN` で実際の接口読み取りを含めます。外部モデル・アカウントは未設定で、実機検証も未実施です。クラウド送信はクライアント・モデル次第です。履歴、統計、自動原因診断、単位推定は提供しません。メーカーのレジスター資料を用い、受信テキストは命令ではなくデータとして扱います。

## 品質状態

- `never_read`: 成功した読み取りがまだない状態。ファイルから読み込んだ値も含みます。

- `fresh`: 同一セッション、設定・値が不変で、max(3000 ms, 3 × スキャン周期)以内。停止中も時間で劣化します。

- `disconnected`: 接続終了。保持値は現在の測定ではありません。

- `unverified`: セッション、読み取り設定、または値が変化しました。

- `read_error`: 直近の通信が失敗し、前回値を保持しています。

- `stale`: 最後の成功から鮮度のしきい値を超えました。

- `stale_snapshot`: スナップショットが5秒超または時刻異常ならMCPはfreshを格下げします。HTTP側も経過時間を確認してください。5秒更新がなければサーバーはHTTP 503 snapshot_expiredを返します。

[MCP stdio](https://modelcontextprotocol.io/specification/2025-06-18/basic/transports) · [MCP tools](https://modelcontextprotocol.io/specification/2025-06-18/server/tools)
