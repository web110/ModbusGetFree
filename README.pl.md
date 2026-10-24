# ModbusGetFree

[English](README.md) | [简体中文](README.zh-CN.md) | [Español](README.es.md) | [Français](README.fr.md) | [Русский](README.ru.md) | [Português](README.pt.md) | [Polski](README.pl.md) | [日本語](README.ja.md) | [Deutsch](README.de.md) | [한국어](README.ko.md)

Narzędzie Modbus RTU dla Windows napisane w C# / Windows Forms, wzorowane na Modbus Poll.

Projekt: Jim NI. Copyright © 2026 Jim NI. Licencja [MIT](LICENSE).

## Okno główne

![Okno główne w języku angielskim](assets/screenshots/main-window-en.png)

Zrzut przedstawia przykładowe dane i angielski interfejs.

## Języki

Domyślnym językiem jest **angielski**. Menu **Language**, przed menu pomocy, umożliwia wybór języka angielskiego, chińskiego uproszczonego, hiszpańskiego, francuskiego, rosyjskiego, portugalskiego, polskiego, japońskiego, niemieckiego lub koreańskiego. Wybór jest zapisywany na następne uruchomienie.

## Uruchamianie i kompilacja

Uruchom `ModbusGetFree.exe`. Aby skompilować program, wykonaj `./build.ps1` w PowerShell z kompilatorem Windows .NET Framework. Kod C# znajduje się w `src`.

## Funkcje

- Połączenie szeregowe RTU, niezależne okna danych i demonstracyjne urządzenie podrzędne bez sprzętu.
- Cykliczne odpytywanie FC01/02/03/04 oraz operacje FC05/06/15/16/17/22/23.
- Ręczne wysyłanie czeka na zakończenie bieżącej transakcji i wstrzymuje nowe zapytania na wspólnym porcie. Odpytywanie jest wznawiane po odpowiedzi, błędzie lub przekroczeniu czasu. Zegar nadal działa, a pominięte zapytania nie są nadrabiane. Zapis rozgłoszeniowy nie czeka na odpowiedź.
- Zapis cewek i rejestrów, maski AND/OR, jednoczesny odczyt i zapis oraz edycja według typu danych.
- Formaty wyświetlania, skalowanie, czcionki, kolory, wykresy, rejestrowanie ruchu i centrum testowe.
- Dokumenty `.mpdoc`, obszary robocze `.mprtu` i import zgodnych archiwów `.mbp` V7.x/V9.x.

## Testy i dokumentacja

`./test-ui-regression.ps1` sprawdza interfejs i języki; `./test.ps1` obejmuje testy RTU. Testy korzystają z urządzenia demonstracyjnego i nie zapisują danych w sprzęcie. Pomoc jest wbudowana w aplikację. Szczegóły importu, zasobów i testów zawiera [angielski README](README.md).

## GitHub

Wpisz adres HTTPS projektu do `github-repository.txt`, aby wyświetlać go w oknie informacji o programie.
