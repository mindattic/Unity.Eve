#Requires -Version 5.1
<#
.SYNOPSIS
    Shuts down any running Unity.Eve, clears the build output, rebuilds the Windows
    player from scratch, and publishes a standalone copy to C:\Apps\Unity.Eve\ — same
    process Automata/KdpPublish/IdiotProof use to always have a fresh, independent
    deployed copy instead of a possibly-stale one.

.DESCRIPTION
    1. Stops any running Unity.Eve.exe process (dev copy or deployed copy).
    2. Removes Build\Windows — the build OUTPUT only, never Library\ (Unity's asset
       import / script-compile cache, which survives exactly as it does for a normal
       Editor rebuild) — so the player build below is never a stale incremental copy.
    3. Runs tools\unity.ps1 -Method Unity.Eve.Editor.UnityEveBuild.BuildWindows in Unity
       batch mode (requires the Editor closed) -> Build\Windows\Unity.Eve.exe.
    4. Copies Build\Windows\* -> C:\Apps\Unity.Eve\.
    5. Writes C:\Apps\Unity.Eve\launch.bat, which calls this same deploy.ps1 (by its
       baked-in source path) EVERY time before launching, so a double-click always runs
       the current source rather than risking a stale deployed copy. If the source repo
       is missing (moved/deleted), it warns and launches whatever is already deployed.

.PARAMETER Launch
    After publishing, immediately run the deployed Unity.Eve.exe.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\deploy.ps1
    powershell -ExecutionPolicy Bypass -File tools\deploy.ps1 -Launch
#>
param([switch]$Launch)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoDir  = Split-Path $PSScriptRoot   # tools\ -> Unity.Eve\
$out      = 'C:\Apps\Unity.Eve'
$exeName  = 'Unity.Eve.exe'
$buildDir = Join-Path $repoDir 'Build\WindowsRelease'
$deployBat = Join-Path $repoDir 'deploy.bat'
$marker    = Join-Path $out '.deploy-commit'

# ── Pull latest, skip the rebuild entirely if nothing changed ──────────────
# Untracked files (e.g. .prose/ session state) don't block a pull; only real changes to tracked
# files (staged or not) do.
$dirty = git -C $repoDir status --porcelain | Where-Object { $_ -notmatch '^\?\? ' }
if ($dirty) { throw "$repoDir has uncommitted changes - not pulling. Commit/stash first." }
git -C $repoDir pull --ff-only
if ($LASTEXITCODE -ne 0) { throw "git pull --ff-only failed in $repoDir (not a clean fast-forward)." }
$head = (git -C $repoDir rev-parse HEAD).Trim()
$upToDate = (Test-Path (Join-Path $out $exeName)) -and (Test-Path $marker) -and
            ((Get-Content -LiteralPath $marker -Raw).Trim() -eq $head)
if ($upToDate) {
    Write-Host "  Already deployed at $head - skipping rebuild." -ForegroundColor Green
    if ($Launch) { Start-Process -FilePath (Join-Path $out $exeName) -WorkingDirectory $out }
    exit 0
}

# ── Stop running instance ──────────────────────────────────────────────────
Write-Host ''
Write-Host '  Stopping running instance...' -ForegroundColor Yellow
$procs = Get-Process 'Unity.Eve' -ErrorAction SilentlyContinue
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
& (Join-Path $PSScriptRoot 'unity.ps1') -Method Unity.Eve.Editor.UnityEveBuild.BuildWindowsRelease -Log deploy-build -Graphics
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
    'title Unity.Eve',
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

Set-Content -LiteralPath $marker -Value $head -NoNewline

# ── Desktop shortcut ─────────────────────────────────────────────────────────
# Points at deploy.bat (not the exe directly), so double-clicking it always pulls latest and
# only rebuilds if the source actually changed, before launching.
$shortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Unity.Eve.lnk'
$link = (New-Object -ComObject WScript.Shell).CreateShortcut($shortcut)
$link.TargetPath       = $deployBat
$link.WorkingDirectory = $repoDir
$link.IconLocation     = "$exe,0"
$link.Description      = 'Pull latest, build Release if changed, and launch Unity.Eve'
$link.Save()
Write-Host "    Shortcut -> $shortcut" -ForegroundColor Gray

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
