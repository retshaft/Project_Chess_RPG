# Tests use a disposable repository and never mutate the actual project's Git data.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$team = Join-Path $PSScriptRoot 'Team.ps1'
$sandbox = Join-Path ([IO.Path]::GetTempPath()) ('checkmate-team-test-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $sandbox | Out-Null
$checks = 0
function Assert-True($Condition, [string]$Message) {
    if (!$Condition) { throw "FAIL: $Message" }
    $script:checks++
}
function Assert-Rejected([scriptblock]$Operation, [string]$Message) {
    $rejected = $false
    try { & $Operation | Out-Null } catch { $rejected = $true }
    Assert-True $rejected $Message
}
function Invoke-FixtureGit([string[]]$Arguments) {
    & git @Arguments | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Test fixture Git operation failed.' }
}
Push-Location $sandbox
try {
    Invoke-FixtureGit @('init', '-b', 'main')
    Invoke-FixtureGit @('config', 'user.name', 'Team Script Test')
    Invoke-FixtureGit @('config', 'user.email', 'test@example.invalid')
    foreach ($folder in @('Packages', 'ProjectSettings', 'Tools/Parallel')) {
        New-Item -ItemType Directory -Path $folder -Force | Out-Null
    }
    Set-Content AGENTS.md 'fixture'
    Set-Content Packages/manifest.json '{}'
    Set-Content Packages/packages-lock.json '{}'
    Set-Content ProjectSettings/ProjectVersion.txt 'fixture'
    Set-Content .gitignore '/.worktrees/'
    Copy-Item -LiteralPath $team -Destination Tools/Parallel/Team.ps1
    Invoke-FixtureGit @('add', '.')
    Invoke-FixtureGit @('commit', '-m', 'fixture')
    & $team -Action New -Agent zcode -Task smoke | Out-Null
    $worker = Join-Path $sandbox '.worktrees/zcode-smoke'
    Assert-True (Test-Path "$worker/Packages/manifest.json") 'Worktree contains the package manifest.'
    Assert-True ((& git -C $worker branch --show-current) -eq 'codex/zcode/smoke') 'Worker uses an independent branch.'
    Assert-Rejected { & $team -Action New -Agent zcode -Task smoke } 'Duplicate creation is rejected.'
    Assert-Rejected { & $team -Action New -Agent zcode -Task '../escape' } 'Path traversal is rejected.'
    Set-Content AGENTS.md 'uncommitted source edit'
    Assert-True ((Get-Content "$worker/AGENTS.md") -eq 'fixture') 'Uncommitted edits stay isolated.'
    $lease = (& $team -Action AcquireUnity) | ConvertFrom-Json
    Assert-Rejected { & $team -Action AcquireUnity } 'Second lease acquisition is rejected.'
    Assert-Rejected { & $team -Action ReleaseUnity -Token 'wrong' } 'Incorrect token is rejected.'
    Push-Location $worker
    try {
        Assert-Rejected { & $team -Action AcquireUnity } 'Lease is shared across worktrees.'
        Assert-Rejected { & $team -Action ReleaseUnity -Token $lease.token } 'Another worktree cannot release the lease.'
        Assert-Rejected { & $team -Action New -Agent antigravity -Task nested } 'Linked worktrees cannot create nested worktrees.'
    } finally { Pop-Location }
    & $team -Action ReleaseUnity -Token $lease.token | Out-Null
    $nextLease = (& $team -Action AcquireUnity) | ConvertFrom-Json
    Assert-True ($nextLease.token -ne $lease.token) 'A released lease can be acquired again.'
    & $team -Action ReleaseUnity -Token $nextLease.token | Out-Null
    & $team -Action Status | Out-Null
    Write-Output "PASS: $checks checks. Fixture retained at $sandbox"
} finally { Pop-Location }
