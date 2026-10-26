$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$directory = Join-Path $PSScriptRoot ('tests\run\updates-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Force "$directory\payload" | Out-Null
Copy-Item "$PSScriptRoot\ModbusGetFree.exe" "$directory\ModbusGetFree.exe"
# An isolated fixture is never published. Its assembly name/version exercise package validation.
'[assembly:System.Reflection.AssemblyVersion("1.11.0.0")] public class UpdateFixture { public static void Main() {} }' | Set-Content "$directory\payload\Fixture.cs"
& $compiler /nologo /target:winexe "/out:$directory\payload\ModbusGetFree.exe" "$directory\payload\Fixture.cs"
if ($LASTEXITCODE -ne 0) { throw 'Fixture build failed.' }
& $compiler /nologo /utf8output /target:exe "/out:$directory\UpdateTests.exe" "/reference:$PSScriptRoot\ModbusGetFree.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll /reference:System.Xml.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "$PSScriptRoot\tests\UpdateTests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Update test build failed.' }
$process = Start-Process -FilePath "$directory\UpdateTests.exe" -ArgumentList ('"' + $directory + '\results" "' + $directory + '\payload\ModbusGetFree.exe"') -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput "$directory\UpdateTests.txt"
if ($process.ExitCode -ne 0) { throw "Update tests failed: $directory\UpdateTests.txt" }
Get-Content "$directory\UpdateTests.txt" | Select-Object -Last 1

