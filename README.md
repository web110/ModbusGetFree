# ModbusGetFree

[English](README.md) | [简体中文](README.zh-CN.md) | [Español](README.es.md) | [Français](README.fr.md) | [Русский](README.ru.md) | [Português](README.pt.md) | [Polski](README.pl.md) | [日本語](README.ja.md) | [Deutsch](README.de.md) | [한국어](README.ko.md)

Windows Modbus RTU desktop utility written in C# / Windows Forms.  Refer to Modbus Poll.

Design by Jim NI. Copyright © 2026 Jim NI. Licensed under the [MIT License](LICENSE).

## Download and use

**For normal use, download only the contents of the [Release folder](Release/); the source code and tests are not needed.** The easiest option is to download `ModbusGetFree-update.zip` from the [latest GitHub Release](https://github.com/web110/ModbusGetFree/releases/latest), extract it completely, and run `ModbusGetFree.exe`. Keep the `assets` folder beside the executable. Windows .NET Framework 4.5 or later is required.

The software supports **automatic upgrades**. Enable the switch in **Help → About ModbusGetFree** to check at startup and choose an immediate upgrade, a background download for the next launch, or skipping this time. A failed GitHub connection is not retried until the next launch.

## Main window

![ModbusGetFree main window in English](assets/screenshots/main-window-en.png)

The screenshot shows the English interface before connecting. Switch languages under **Language**.

## Languages

Default: **English**. Choose **Language** to switch among English, Simplified Chinese, Spanish, French, Russian, Portuguese, Polish, Japanese, German and Korean. The selected language is saved for the next launch. Menu access keys and Modbus data remain unchanged.

## Run and build

Run `ModbusGetFree.exe`. Build with `./build.ps1` in PowerShell using the Windows .NET Framework compiler. No additional runtime packages are required. Open this folder in VS Code to edit the readable C# files under `src`.

## Features

- Serial RTU connection, shared-port scheduling, independent data windows and a hardware-free demo slave.
- FC01/02/03/04 polling and FC05/06/15/16/17/22/23 operation windows.
- Single writes: choose the wire function (05/15 or 06/16), inspect the result, optionally close after success.
- Bulk writes: coil checklists, register value lists, 28 combined data type / byte order choices, typed editing and Open/Save value lists.
- Manual FC05/06/15/16/17/22/23 sends wait for the current transaction, reserve the shared channel against new polls, and restore polling after the reply or an error/timeout. Scheduler ticks continue; missed periods are not replayed. Broadcast writes finish without a reply.
- FC22: AND/OR masks with 32 individual bit controls.
- FC23: separate read/write ranges, typed write editing and separate read results.
- Independent display formats, scaling, fonts, colors, charting, traffic capture and a raw Test Center.
- Save data windows as `.mpdoc`, workspaces as `.mprtu`; import supported V7.x/V9.x `.mbp` register archives. Supported imports include FC03/04 and verified 16-bit, float32 and unsigned32 formats. Unsupported layouts/scaling are rejected.

Help Topics is built into the application. This README is developer documentation and is not linked from Help.

## Assets

The original MBGF four-letter product icon is in `assets/branding`. The toolbar PNG files in `assets/icons/active` are editable application assets. No extracted third-party icons, resource dumps or comparison documents are included.

## Tests

Run `./test-ui-regression.ps1` for the UI and language regression checks, including all 100 language transitions, tooltip restoration, unchanged user data and preference persistence. Run `./test.ps1` for the full suite including RTU protocol tests. Tests use their own temporary windows and the demo slave; they do not write to hardware. The suite covers protocol requests and validation, operation dialogs, serial port discovery, display formats, fonts, icons and window behavior. Generated executables, logs and screenshots stay under dated folders in `tests/run`, which is excluded from Git. The `.cs` files in `tests` are test source code and belong in the published project.

## GitHub

The About dialog reads `github-repository.txt` for the project URL. Once the repository is published, set this file to its HTTPS GitHub URL. No credentials or user project files belong in this repository.

## Automatic updates

In **Help → About ModbusGetFree**, enable **Allow updates: check GitHub at startup** (off by default). A new stable release offers **Upgrade now**, **Upgrade in background** (download now, apply on next launch; session-only choice), or **Skip this time**. See [update and release instructions](UPDATES.md).

GitHub is checked only once per launch. If the connection fails or times out, there is no reconnection or retry during the current session; the next attempt is on the next launch. Serial communication is unaffected.
