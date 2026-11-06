param([string]$Language = '')
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$executable = Join-Path $repo 'AiDemo.exe'
& $compiler /nologo /utf8output /target:winexe "/out:$executable" "/reference:$repo\ModbusGetFree.exe" /reference:System.Windows.Forms.dll /reference:System.Core.dll (Join-Path $PSScriptRoot 'Demo.cs')
if ($LASTEXITCODE -ne 0) { throw 'Demo build failed.' }
if ($Language) { Start-Process -FilePath $executable -ArgumentList @('--lang', $Language) }
else { Start-Process -FilePath $executable }
