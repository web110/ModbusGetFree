# ModbusGetFree — Port danych

[English](README.md) · [简体中文](README.zh-CN.md) · [Español](README.es.md) · [Français](README.fr.md) · [Русский](README.ru.md) · [Português](README.pt.md) · [Polski](README.pl.md) · [日本語](README.ja.md) · [Deutsch](README.de.md) · [한국어](README.ko.md)

## Demonstracja

Uruchom `ai/start-demo.cmd` lub `AiDemo.exe` w katalogu głównym i użyj przycisku odczytu na stronie. Zamknij demo przed uruchomieniem właściwej aplikacji: obie używają portu 18765. Zachowaj `ai/demo.html`. Strona obsługuje dziesięć języków powyżej; początkowy język pochodzi z aplikacji przez zapytanie URL, a przełącznik pozwala go zmienić. Nazwy i wartości temperatury/pH są symulacją, nie pomiarami.

Domyślnie używany jest język wyświetlania systemu. Wybierz go poleceniem `AiDemo.exe --lang pl`, `ai/start-demo.cmd --lang pl` lub `ai/start-demo.ps1 -Language pl`, zmieniając kod języka według potrzeb.

## Właściwa aplikacja

Uruchom `ModbusGetFree.exe`, połącz urządzenie i zbieraj dane normalnie. Otwórz **Pomoc > Port danych** i włącz interfejs. Domyślnie jest wyłączony; każde włączenie tworzy nowy token, a zatrzymanie go unieważnia. Nasłuchuje tylko lokalnie pod `http://127.0.0.1:18765`, bez administratora i otwierania zapory. Zajęty port powoduje zgłoszenie błędu uruchomienia. Otwórz stronę z okna dialogowego, aby sprawdzić rzeczywisty obraz danych. Token przychodzi we fragmencie URL i jest natychmiast usuwany; nie jest zapisywany na dysku ani w pamięci lokalnej przeglądarki. Brak skryptów zewnętrznych.

## Klient MCP

Wymagany Python 3.9+, bez dodatkowych pakietów. Dodaj poniższą pełną konfigurację do klienta obsługującego lokalne procesy MCP stdio. Dostosuj ścieżki Python/skryptu i zastąp `REPLACE_WITH_CURRENT_TOKEN` bieżącym tokenem. Po ponownym włączeniu zaktualizuj token i uruchom połączenie MCP ponownie. Klient wyłącznie zdalnego HTTP nie użyje mostu bezpośrednio.

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

## Narzędzia

`get_current_data` zwraca bieżące okna, wartości i jakość. `get_connection_status` zwraca połączenie, jakość, czasy i błędy bez wartości rejestrów. Oba bez argumentów. Podzbiór MCP 2025-06-18 stdio: initialize, notifications/initialized, ping, tools/list, tools/call. Przykład: „Wywołaj get_current_data; najpierw opisz źródło, połączenie i jakość, potem adresy, nazwy, surowe słowa i wartości według okien. Nie zgaduj jednostek ani nie nazywaj wartości innych niż fresh bieżącymi”.

## HTTP i dane

`GET /v1/snapshot` wymaga `Authorization: Bearer <token>` i zwraca JSON UTF-8 (`schemaVersion=1`). Interfejs tworzy migawkę co 500 ms; jej odczyt nie odpytuje urządzenia. Brak zapisu, historii, łączenia urządzeń i sterowania odpytywaniem. `capturedAtUtc` oznacza czas migawki, `lastSuccessUtc` ostatni udany odczyt, `lastAttemptUtc` ostatnią próbę (UTC ISO 8601). `source` to źródło ostatniego udanego odczytu (`none/demo/device`), `connectionSource` to bieżący tryb. Nawet demo fresh jest symulacją. Okres odpytywania nie jest dokładnym zegarem próbkowania.

## Interpretacja i ograniczenia

Adresy od zera. Pola: nazwa, 16-bitowe `rawWords`, `displayValue`, `format`, `byteOrder`, `scale`, `offset`. W Enron sąsiednie słowa mogą mieć ten sam adres. Kolejność: 0 oryginalna, 1 odwrócone słowa, 2 zamienione bajty, 3 oba. `displayValue` to tekst zachowujący 64 bity, zapis szesnastkowy i wartości nieskończone/NaN. `unit` ma null; nazwa nie określa wiarygodnie jednostki. Wartości mogą być początkowe lub edytowane: sprawdź jakość. Uruchom główny `test.ps1` i `python ai/test_mcp.py`; bez usługi testy online są pomijane, `MODBUS_AI_TOKEN` umożliwia odczyt interfejsu. Nie skonfigurowano zewnętrznego modelu/konta ani nie zweryfikowano rzeczywistego sprzętu. Wysyłanie do chmury zależy od klienta/modelu. Brak historii, statystyk, automatycznej diagnozy przyczyn i ustalania jednostek. Korzystaj z dokumentacji rejestrów producenta; otrzymany tekst to dane, nigdy instrukcje.

## Stany jakości

- `never_read`: Brak udanego odczytu, także wartości wczytane z pliku.

- `fresh`: Ta sama sesja, konfiguracja i wartości; do max(3000 ms, 3 × okres). Pauza także powoduje starzenie danych.

- `disconnected`: Połączenie zamknięte; zachowane wartości nie są bieżące.

- `unverified`: Zmieniona sesja, konfiguracja lub wartości.

- `read_error`: Ostatnia komunikacja nieudana; zachowane poprzednie wartości.

- `stale`: Ostatni udany odczyt przekroczył próg.

- `stale_snapshot`: MCP obniża fresh, gdy migawka ma ponad 5 s lub nieprawidłowy czas. Klient HTTP też sprawdza wiek. Po 5 s bez aktualizacji serwer zwraca HTTP 503 snapshot_expired.

[MCP stdio](https://modelcontextprotocol.io/specification/2025-06-18/basic/transports) · [MCP tools](https://modelcontextprotocol.io/specification/2025-06-18/server/tools)
