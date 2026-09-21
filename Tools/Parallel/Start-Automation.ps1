$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$Venv = Join-Path $Root '.parallel\automation-venv'
$Python = Join-Path $Venv 'Scripts\python.exe'

foreach ($Name in @('DISCORD_BOT_TOKEN', 'DISCORD_GUILD_ID', 'DISCORD_OWNER_ID', 'TYPESAFE_API_KEY')) {
    if (-not [Environment]::GetEnvironmentVariable($Name, 'Process')) {
        throw "$Name 환경 변수가 없습니다. 새 터미널에서 다시 실행해 주세요."
    }
}

if (-not (Test-Path -LiteralPath $Python)) {
    python -m venv $Venv
    & $Python -m pip install --disable-pip-version-check -r (Join-Path $PSScriptRoot 'automation-requirements.txt')
}

Set-Location -LiteralPath $Root
& $Python (Join-Path $PSScriptRoot 'discord_control.py')

