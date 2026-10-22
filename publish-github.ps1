$ErrorActionPreference = 'Stop'
if (!(Get-Command gh -ErrorAction SilentlyContinue)) { throw 'Install GitHub CLI, sign in using gh auth login, or use VS Code Publish to GitHub.' }
Push-Location $PSScriptRoot
try {
    & gh auth status
    if ($LASTEXITCODE -ne 0) { throw 'Sign in to GitHub CLI first.' }
    $remote = & git remote get-url origin 2>$null
    if (!$remote) {
        & gh repo create ModbusGetFree --public --source . --remote origin --push
        if ($LASTEXITCODE -ne 0) { throw 'Repository creation failed.' }
    }
    $url = & gh repo view --json url --jq .url
    if ($LASTEXITCODE -ne 0 -or !$url.StartsWith('https://github.com/')) { throw 'Cannot verify repository URL.' }
    Set-Content -LiteralPath (Join-Path $PSScriptRoot 'github-repository.txt') -Value $url -Encoding UTF8
    & git add github-repository.txt
    & git commit -m 'Set GitHub project URL'
    & git push -u origin main
    if ($LASTEXITCODE -ne 0) { throw 'Push failed.' }
    Write-Output $url
} finally { Pop-Location }
