#Requires -Version 5.1
<#
.SYNOPSIS
    Shuts down any running UnityEve, clears the build output, rebuilds the Windows
    player from scratch, and publishes a standalone copy to C:\Apps\UnityEve\ — same
    process Automata/KdpPublish/IdiotProof use to always have a fresh, independent
    deployed copy instead of a possibly-stale one.

.DESCRIPTION
    1. Stops any running UnityEve.exe process (dev copy or deployed copy).
    2. Removes Build\Windows — the build OUTPUT only, never Library\ (Unity's asset
       import / script-compile cache, which survives exactly as it does for a normal
       Editor rebuild) — so the player build below is never a stale incremental copy.
    3. Runs tools\unity.ps1 -Method UnityEve.Editor.UnityEveBuild.BuildWindows in Unity
       batch mode (requires the Editor closed) -> Build\Windows\UnityEve.exe.
    4. Copies Build\Windows\* -> C:\Apps\UnityEve\.
    5. Writes C:\Apps\UnityEve\launch.bat, which calls this same deploy.ps1 (by its
       baked-in source path) EVERY time before launching, so a double-click always runs
       the current source rather than risking a stale deployed copy. If the source repo
       is missing (moved/deleted), it warns and launches whatever is already deployed.

.PARAMETER Launch
    After publishing, immediately run the deployed UnityEve.exe.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\deploy.ps1
    powershell -ExecutionPolicy Bypass -File tools\deploy.ps1 -Launch
#>
param([switch]$Launch)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoDir  = Split-Path $PSScriptRoot   # tools\ -> UnityEve\
$out      = 'C:\Apps\UnityEve'
$exeName  = 'UnityEve.exe'
$buildDir = Join-Path $repoDir 'Build\WindowsRelease'

# ── Stop running instance ──────────────────────────────────────────────────
Write-Host ''
Write-Host '  Stopping running instance...' -ForegroundColor Yellow
$procs = Get-Process 'UnityEve' -ErrorAction SilentlyContinue
if ($procs) {
    $procs | Stop-Process -Force
    Write-Host "    Stopped ($(@($procs).Count) process(es))" -ForegroundColor DarkYellow
    Start-Sleep -Milliseconds 800
} else {
    Write-Host '    Nothing running.' -ForegroundColor DarkGray
}

# ── Clear build output ───────────────────────────────────────────────────────
# Build\WindowsRelease only — never Library\, which is Unity's import/compile cache, not
# build output, and must survive a redeploy exactly as it survives a normal rebuild.
Write-Host ''
Write-Host '  Clearing build output (Build\WindowsRelease)...' -ForegroundColor Cyan
if (Test-Path $buildDir) {
    Remove-Item -Recurse -Force $buildDir
}

# ── Build ────────────────────────────────────────────────────────────────────
Write-Host ''
Write-Host '  Building Windows Release player (Unity batch mode)...' -ForegroundColor Cyan
& (Join-Path $PSScriptRoot 'unity.ps1') -Method UnityEve.Editor.UnityEveBuild.BuildWindowsRelease -Log deploy-build -Graphics
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed (exit $LASTEXITCODE)." -ForegroundColor Red
    exit $LASTEXITCODE
}

$builtExe = Join-Path $buildDir $exeName
if (-not (Test-Path $builtExe)) {
    Write-Host "Build completed but $exeName not found at $builtExe." -ForegroundColor Red
    exit 1
}

# ── Publish ─────────────────────────────────────────────────────────────────
Write-Host ''
Write-Host "  Publishing $builtExe" -ForegroundColor Cyan
Write-Host "    -> $out\$exeName"

if (Test-Path $out) { Remove-Item -Recurse -Force $out }
New-Item -ItemType Directory -Force $out | Out-Null
Copy-Item -Recurse -Force (Join-Path $buildDir '*') $out

$exe = Join-Path $out $exeName
if (-not (Test-Path $exe)) {
    Write-Host "Publish completed but $exeName not found at $exe." -ForegroundColor Red
    exit 1
}

# ── Write self-redeploying launcher ─────────────────────────────────────────
# Always redeploys from source before launching, so a double-click can never run a
# stale build. $PSScriptRoot here is THIS script's own folder — baked into the batch
# file as an absolute path so it still resolves no matter where launch.bat itself is
# invoked from. Falls back to launching the existing exe (with a warning) if that path
# is missing, rather than refusing to start.
$deployPs1Path = Join-Path $PSScriptRoot 'deploy.ps1'
$launchBatPath = Join-Path $out 'launch.bat'
@(
    '@echo off',
    'title UnityEve',
    "set `"DEPLOY_PS1=$deployPs1Path`"",
    'if exist "%DEPLOY_PS1%" (',
    '    echo Redeploying latest build from source...',
    '    powershell -NoProfile -ExecutionPolicy Bypass -File "%DEPLOY_PS1%"',
    '    if errorlevel 1 echo Redeploy failed - launching whatever is already in this folder instead.',
    ') else (',
    '    echo Source repo not found at "%DEPLOY_PS1%" - launching existing build without redeploying.',
    ')',
    "start `"`" `"%~dp0$exeName`""
) | Set-Content $launchBatPath -Encoding ascii

# ── Summary ────────────────────────────────────────────────────────────────
Write-Host ''
Write-Host '  Published successfully.' -ForegroundColor Green
Write-Host "    Exe    : $exe ($([math]::Round(((Get-Item $exe).Length / 1MB), 1)) MB)" -ForegroundColor Gray
Write-Host "    Launch : $launchBatPath" -ForegroundColor Gray
Write-Host ''

if ($Launch) {
    Write-Host '  Launching...' -ForegroundColor Cyan
    Start-Process $exe
}
