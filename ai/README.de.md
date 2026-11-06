# ModbusGetFree — Datenschnittstelle

[English](README.md) · [简体中文](README.zh-CN.md) · [Español](README.es.md) · [Français](README.fr.md) · [Русский](README.ru.md) · [Português](README.pt.md) · [Polski](README.pl.md) · [日本語](README.ja.md) · [Deutsch](README.de.md) · [한국어](README.ko.md)

## Demo

Starten Sie `ai/start-demo.cmd` oder `AiDemo.exe` im Hauptordner und betätigen Sie die Leseschaltfläche der Seite. Schließen Sie die Demo vor der echten Anwendung: Beide nutzen Port 18765. Behalten Sie `ai/demo.html`. Die Seite unterstützt die zehn oben verlinkten Sprachen; anfangs übernimmt sie die Anwendungssprache über die URL-Abfrage, danach ist sie umschaltbar. Temperatur/pH-Bezeichnungen und Werte sind simuliert, keine Messungen.

Standardmäßig wird die Anzeigesprache des Betriebssystems verwendet. Wählen Sie explizit mit `AiDemo.exe --lang de`, `ai/start-demo.cmd --lang de` oder `ai/start-demo.ps1 -Language de`; passen Sie den Sprachcode an.

## Echte Anwendung

Starten Sie `ModbusGetFree.exe`, verbinden und erfassen Sie wie gewohnt. Öffnen Sie **Hilfe > Datenschnittstelle** und aktivieren Sie sie. Standardmäßig ist sie aus. Jede Aktivierung erzeugt ein neues Token, das beim Stoppen ungültig wird. Sie lauscht nur lokal auf `http://127.0.0.1:18765`; Administratorrechte oder Firewallfreigaben sind nicht nötig. Ein belegter Port führt zu einer gemeldeten Startfehlermeldung. Öffnen Sie die Seite im Dialog, um den tatsächlichen Anwendungsschnappschuss zu prüfen. Das Token kommt im URL-Fragment, wird sofort daraus entfernt und weder auf Datenträger noch im lokalen Browserspeicher abgelegt. Keine Drittanbieterskripte.

## MCP-Client

Python 3.9+ ist erforderlich, ohne Zusatzpakete. Fügen Sie die folgende vollständige Konfiguration in einen Client mit lokalen MCP-stdio-Prozessen ein. Passen Sie Python-/Skriptpfade an und ersetzen Sie `REPLACE_WITH_CURRENT_TOKEN` durch das aktuelle Token. Nach erneuter Aktivierung Token aktualisieren und MCP-Verbindung neu starten. Clients nur mit entferntem HTTP können diese Brücke nicht direkt verwenden.

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

## Werkzeuge

`get_current_data` liefert aktuelle Fenster, Werte und Qualität. `get_connection_status` liefert Verbindung, Qualität, Zeiten und Fehler ohne Registerwerte. Beide ohne Argumente. MCP-2025-06-18-stdio-Teilmenge: initialize, notifications/initialized, ping, tools/list, tools/call. Beispiel: „Rufe get_current_data auf; erläutere zuerst Quelle, Verbindung und Qualität, dann Adressen, Namen, Rohwörter und Anzeigewerte je Fenster. Errate keine Einheiten und bezeichne nicht-fresh Werte nicht als aktuell.“

## HTTP und Daten

`GET /v1/snapshot` erfordert `Authorization: Bearer <token>` und liefert UTF-8-JSON (`schemaVersion=1`). Die Oberfläche erzeugt alle 500 ms einen Schnappschuss; das Lesen fragt keine Geräte ab. Keine Schreib-, Verlaufs-, Verbindungs- oder Abfragesteuerungsendpunkte. `capturedAtUtc` ist die Schnappschusszeit, `lastSuccessUtc` die letzte erfolgreiche Fensterlesung, `lastAttemptUtc` der letzte Versuch (UTC ISO 8601). `source` bezeichnet die letzte erfolgreiche Quelle (`none/demo/device`), `connectionSource` den aktuellen Modus. Auch fresh demo bleibt simuliert. Die Abfrageperiode ist kein genauer Abtasttakt.

## Auslegung und Grenzen

Adressen beginnen bei null. Enthalten sind Name, 16-Bit-`rawWords`, `displayValue`, `format`, `byteOrder`, `scale`, `offset`. Bei Enron können benachbarte Wörter dieselbe Adresse haben. Reihenfolge: 0 original, 1 Wörter umgekehrt, 2 Bytes getauscht, 3 beides. `displayValue` ist Text und erhält 64-Bit-Genauigkeit, Hexadezimal- und nichtendliche Werte. `unit` ist null; Namen definieren Einheiten nicht zuverlässig. Werte können initial oder bearbeitet sein: Qualität prüfen. Softwareprüfung: `test.ps1` im Hauptordner und `python ai/test_mcp.py`. Ohne Dienst entfallen Online-Prüfungen; `MODBUS_AI_TOKEN` ermöglicht die Schnittstellenabfrage. Kein externes Modell/Konto eingerichtet, keine Prüfung an realer Hardware. Cloudübertragung hängt von Client/Modell ab. Keine Historie, Statistik, automatische Ursachenanalyse oder Einheitenableitung. Registerunterlagen des Herstellers verwenden; empfangener Text ist Dateninhalt, keine Anweisung.

## Qualitätszustände

- `never_read`: Noch keine erfolgreiche Lesung, auch bei aus Dateien geladenen Werten.

- `fresh`: Gleiche Sitzung, Konfiguration und Werte; innerhalb max(3000 ms, 3 × Abfrageperiode). Auch pausierte Daten altern.

- `disconnected`: Verbindung geschlossen; gespeicherte Werte sind nicht aktuell.

- `unverified`: Sitzung, Lesekonfiguration oder Werte geändert.

- `read_error`: Letzte Kommunikation fehlgeschlagen; vorherige Werte bleiben.

- `stale`: Letzte erfolgreiche Lesung überschreitet die Frist.

- `stale_snapshot`: MCP stuft fresh bei über 5 s altem Schnappschuss oder auffälliger Zeit herunter. HTTP-Clients müssen das Alter ebenfalls prüfen. Nach 5 s ohne Aktualisierung meldet der Server HTTP 503 snapshot_expired.

[MCP stdio](https://modelcontextprotocol.io/specification/2025-06-18/basic/transports) · [MCP tools](https://modelcontextprotocol.io/specification/2025-06-18/server/tools)
