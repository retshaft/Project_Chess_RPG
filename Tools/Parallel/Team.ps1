[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('New', 'Status', 'AcquireUnity', 'ReleaseUnity')]
    [string]$Action,
    [ValidateSet('zcode', 'antigravity')][string]$Agent,
    [string]$Task = 'bootstrap',
    [string]$Base = 'HEAD',
    [string]$Token
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Invoke-Git([string[]]$Arguments) {
    $result = & git @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Git failed: $($Arguments -join ' ')" }
    return $result
}

$root = [string](Invoke-Git @('rev-parse', '--show-toplevel'))
$common = [string](Invoke-Git @('rev-parse', '--path-format=absolute', '--git-common-dir'))
$coordination = Join-Path $common 'parallel-team'
$leasePath = Join-Path $coordination 'unity-lease.json'

switch ($Action) {
    'New' {
        if (!$Agent) { throw 'New requires -Agent.' }
        if ($Task -cnotmatch '^[a-z0-9][a-z0-9-]{0,47}$') { throw 'Task must be a short lowercase slug.' }
        # Only the primary checkout may create nested worktrees.
        if (!(Test-Path -LiteralPath (Join-Path $root '.git') -PathType Container)) {
            throw 'Run New from the primary checkout, not a linked worktree.'
        }
        $sha = [string](Invoke-Git @('rev-parse', '--verify', "$Base^{commit}"))
        foreach ($required in @('AGENTS.md', 'Packages/manifest.json', 'Packages/packages-lock.json', 'ProjectSettings/ProjectVersion.txt', 'Tools/Parallel/Team.ps1')) {
            Invoke-Git @('cat-file', '-e', "${sha}:$required") | Out-Null
        }
        $folder = Join-Path $root ".worktrees/$Agent-$Task"
        $branch = "codex/$Agent/$Task"
        if (Test-Path -LiteralPath $folder) { throw "Destination already exists: $folder" }
        if (Invoke-Git @('branch', '--list', $branch)) { throw "Branch already exists: $branch" }
        if (Invoke-Git @('status', '--porcelain')) {
            Write-Warning 'The source checkout has uncommitted changes. Only the selected commit is copied; reconcile relevant changes before assigning work.'
        }
        Invoke-Git @('worktree', 'add', '-b', $branch, $folder, $sha)
        Write-Output "Workspace: $folder"
        Write-Output "Base: $sha"
    }
    'Status' {
        Invoke-Git @('worktree', 'list')
        foreach ($entry in (Invoke-Git @('worktree', 'list', '--porcelain'))) {
            if ($entry.StartsWith('worktree ')) {
                $path = $entry.Substring(9)
                Write-Output "`n[$path]"
                Invoke-Git @('-C', $path, 'status', '--short')
            }
        }
        if (Test-Path -LiteralPath $leasePath) { Get-Content -LiteralPath $leasePath }
        else { Write-Output 'Unity lease: free (verify the actual editor state before starting).' }
    }
    'AcquireUnity' {
        New-Item -ItemType Directory -Path $coordination -Force | Out-Null
        $lease = [ordered]@{
            token = [Guid]::NewGuid().ToString('N')
            worktree = $root
            branch = [string](Invoke-Git @('branch', '--show-current'))
            createdUtc = [DateTime]::UtcNow.ToString('o')
        }
        # Atomic create: two agents cannot acquire the same cooperative lease.
        $stream = [IO.File]::Open($leasePath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
        try {
            $bytes = [Text.Encoding]::UTF8.GetBytes(($lease | ConvertTo-Json))
            $stream.Write($bytes, 0, $bytes.Length)
        } finally { $stream.Dispose() }
        $lease | ConvertTo-Json
    }
    'ReleaseUnity' {
        if (!$Token) { throw 'ReleaseUnity requires the token returned by AcquireUnity.' }
        $lease = Get-Content -LiteralPath $leasePath -Raw | ConvertFrom-Json
        if ($lease.token -cne $Token -or $lease.worktree -ne $root) {
            throw 'Token or worktree does not match the Unity lease owner.'
        }
        Remove-Item -LiteralPath $leasePath
        Write-Output 'Unity lease released.'
    }
}
