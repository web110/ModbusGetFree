$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
Add-Type -AssemblyName System.IO.Compression.FileSystem
$release = Join-Path $PSScriptRoot 'release'
New-Item -ItemType Directory -Force $release | Out-Null
$destination = Join-Path $release 'ModbusGetFree-update.zip'
# Rebuild this generated archive; never collect preferences or user data.
$stream = [IO.File]::Open($destination, [IO.FileMode]::Create)
$archive = New-Object IO.Compression.ZipArchive($stream, [IO.Compression.ZipArchiveMode]::Create)
try {
    $files = @(Get-Item -LiteralPath "$PSScriptRoot\ModbusGetFree.exe", "$PSScriptRoot\LICENSE", "$PSScriptRoot\UPDATES.md") + @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter 'README*.md') + @(Get-ChildItem -LiteralPath "$PSScriptRoot\assets" -Recurse -File | Where-Object { $_.Extension -in '.png', '.ico', '.svg' })
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($PSScriptRoot.Length + 1).Replace('\', '/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $relative, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $archive.Dispose(); $stream.Dispose() }
Write-Output "Update package: $destination"

