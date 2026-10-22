$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$directory = Join-Path $PSScriptRoot ('tests\run\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Force $directory | Out-Null
Copy-Item -LiteralPath "$PSScriptRoot\ModbusGetFree.exe" "$directory\ModbusGetFree.exe" -Force
foreach ($name in @('SelectedWriteTests', 'SerialPortCatalogTests', 'ProductTests', 'TestCenterFixTests', 'OriginalFontTests', 'SmoothGridTests', 'DisplayFixTests', 'IconAssetsTests', 'RtuTests')) {
    $executable = Join-Path $directory ($name + '.exe')
    & $compiler /nologo /utf8output /target:exe "/out:$executable" "/reference:$PSScriptRoot\ModbusGetFree.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll /reference:System.Xml.dll /reference:Microsoft.CSharp.dll (Join-Path $PSScriptRoot ("tests\" + $name + '.cs'))
    if ($LASTEXITCODE -ne 0) { throw "Test build failed: $name" }
    $arguments = if ($name -eq 'IconAssetsTests') { '"' + $PSScriptRoot + '" "' + $directory + '\icon-overrides"' } else { '"' + $directory + '"' }
    $report = Join-Path $directory ($name + '.txt')
    $process = Start-Process -FilePath $executable -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput $report
    if ($process.ExitCode -ne 0) { throw "Test failed: $name; see $report" }
    Write-Output "$name passed"
}
