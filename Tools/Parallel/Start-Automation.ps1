$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$Venv = Join-Path $Root '.parallel\automation-venv'
$Python = Join-Path $Venv 'Scripts\python.exe'
$env:PYTHONUTF8 = '1'
$env:PYTHONIOENCODING = 'utf-8'

foreach ($Name in @('DISCORD_BOT_TOKEN', 'DISCORD_GUILD_ID', 'DISCORD_OWNER_ID', 'TYPESAFE_API_KEY')) {
    if (-not [Environment]::GetEnvironmentVariable($Name, 'Process')) {
        $UserValue = [Environment]::GetEnvironmentVariable($Name, 'User')
        if (-not $UserValue) {
            throw "$Name 환경 변수가 없습니다."
        }
        Set-Item -Path "Env:$Name" -Value $UserValue
    }
}

if (-not (Test-Path -LiteralPath $Python)) {
    python -m venv $Venv
    & $Python -m pip install --disable-pip-version-check -r (Join-Path $PSScriptRoot 'automation-requirements.txt')
}

Set-Location -LiteralPath $Root
& $Python (Join-Path $PSScriptRoot 'discord_control.py')
