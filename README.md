# ModbusGetFree

Windows Modbus RTU desktop utility written in C# / Windows Forms.

Design by Jim NI. Copyright © 2026 Jim NI. Licensed under the [MIT License](LICENSE).

## Run and build

Run `ModbusGetFree.exe`. Build with `./build.ps1` in PowerShell using the Windows .NET Framework compiler. No additional runtime packages are required. Open this folder in VS Code to edit the readable C# files under `src`.

## Features

- Serial RTU connection, shared-port scheduling, independent data windows and a hardware-free demo slave.
- FC01/02/03/04 polling and FC05/06/15/16/17/22/23 operation windows.
- Single writes: choose the wire function (05/15 or 06/16), inspect the result, optionally close after success.
- Bulk writes: coil checklists, register value lists, 28 combined data type / byte order choices, typed editing and Open/Save value lists.
- FC22: AND/OR masks with 32 individual bit controls.
- FC23: separate read/write ranges, typed write editing and separate read results.
- Independent display formats, scaling, fonts, colors, charting, traffic capture and a raw Test Center.
- Save data windows as `.mpdoc`, workspaces as `.mprtu`; import supported 7000/9000 `.mbp` register archives. Supported imports include FC03/04 and verified 16-bit, float32 and unsigned32 formats. Unsupported layouts/scaling are rejected.

Help Topics is built into the application. This README is developer documentation and is not linked from Help.

## Assets

The original MBGF four-letter product icon is in `assets/branding`. The toolbar PNG files in `assets/icons/active` are editable application assets. No extracted third-party icons, resource dumps or comparison documents are included.

## Tests

Run `./test.ps1`. Tests use their own temporary windows and the demo slave; they do not write to hardware. The suite covers protocol requests and validation, operation dialogs, serial port discovery, display formats, fonts, icons and window behavior. Generated executables, logs and screenshots stay under dated folders in `tests/run`, which is excluded from Git. The `.cs` files in `tests` are test source code and belong in the published project.

## GitHub

The About dialog reads `github-repository.txt` for the project URL. Once the repository is published, set this file to its HTTPS GitHub URL. No credentials or user project files belong in this repository.
