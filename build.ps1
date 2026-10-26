$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$sourceFiles = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName }
& $compiler /nologo /utf8output /warnaserror /target:winexe "/win32icon:$PSScriptRoot\assets\branding\app.ico" "/out:$PSScriptRoot\ModbusGetFree.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Xml.dll /reference:System.Core.dll /reference:Microsoft.CSharp.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll $sourceFiles
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Output 'Build succeeded: ModbusGetFree.exe'

