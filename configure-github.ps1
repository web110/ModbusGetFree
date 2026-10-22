param([Parameter(Mandatory=$true)][string]$RepositoryUrl)
$ErrorActionPreference = 'Stop'
$url = [uri]$RepositoryUrl
if ($url.Scheme -ne 'https' -or $url.Host -ne 'github.com' -or $url.AbsolutePath.Trim('/').Split('/').Count -ne 2) {
    throw 'Use an HTTPS GitHub owner/repository URL.'
}
Set-Content -LiteralPath (Join-Path $PSScriptRoot 'github-repository.txt') -Value $url.AbsoluteUri.TrimEnd('/') -Encoding UTF8
Write-Output 'About will show this repository URL the next time it opens.'
