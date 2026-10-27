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
    5. Writes C:\Apps\Unity.Eve\run.bat on every run (and deletes a stale launch.bat or
       deploy.bat left there by older versions) - not only when publishing - so an
       existing install picks up launcher changes without a rebuild. The launcher opens a
       menu (MAT-T67): 1) Deploy & Run calls this run.ps1 (by its baked-in source path)
       with -Force, then launches; 2) Run launches the exe beside it without deploying
       (deploying first if it is missing). If the source repo is missing (moved/deleted), it
       warns and launches whatever is already deployed.

    The menus live in the .bat files (that run.bat and the repo-root run.bat); this script
    never prompts, so agents and /deploy run it unattended.

.PARAMETER Launch
    After publishing, immediately run the deployed Unity.Eve.exe.

.PARAMETER Force
    Rebuild and republish even when the deployed commit is up to date. It skips only that
    check. The launchers' "Deploy & Run" passes it.

.PARAMETER LauncherOnly
    Only (re)write C:\Apps\Unity.Eve\run.bat (removing a stale launch.bat/deploy.bat) and exit: no pull, no stop, no build.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\run.ps1
    powershell -ExecutionPolicy Bypass -File tools\run.ps1 -Launch
    powershell -ExecutionPolicy Bypass -File tools\run.ps1 -Force -Launch
    powershell -ExecutionPolicy Bypass -File tools\run.ps1 -LauncherOnly
#>
param(
    [switch]$Launch,
    [switch]$Force,
    [switch]$LauncherOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoDir  = Split-Path $PSScriptRoot   # tools\ -> Unity.Eve\
$out      = 'C:\Apps\Unity.Eve'
$exeName  = 'Unity.Eve.exe'
$buildDir = Join-Path $repoDir 'Build\WindowsRelease'
$runBat = Join-Path $repoDir 'run.bat'
$marker    = Join-Path $out '.deploy-commit'

# ── Generated launcher ──────────────────────────────────────────────────────
# A menu first (MAT-T67): 1) Deploy & Run redeploys from source (-Force) before launching;
# 2) Run launches the exe beside it as is. $PSScriptRoot here is THIS script's own folder,
# baked into the batch file as an absolute path so it still resolves no matter where
# run.bat itself is invoked from. Falls back to launching the existing exe (with a
# warning) if that path is missing or the redeploy fails, rather than refusing to start.
# Written with CRLF, so its goto labels are safe.
$runPs1Path = Join-Path $PSScriptRoot 'run.ps1'
$launchBatPath = Join-Path $out 'run.bat'
# Older versions wrote these names; the shortcut-era launcher is run.bat (MAT-T68).
$staleLaunchers = @((Join-Path $out 'launch.bat'), (Join-Path $out 'deploy.bat'))
function Write-Launcher {
    New-Item -ItemType Directory -Force $out | Out-Null
    $lines = @(
        '@echo off',
        'title Unity.Eve',
        "set `"RUN_PS1=$runPs1Path`"",
        'echo.',
        'echo   Unity.Eve',
        'echo     1^) Deploy ^& Run',
        'echo     2^) Run',
        'echo.',
        'choice /c 12 /n /m "  Choose 1 or 2: "',
        'if errorlevel 2 goto run',
        ':deploy',
        'if exist "%RUN_PS1%" (',
        '    echo Redeploying latest build from source...',
        '    powershell -NoProfile -ExecutionPolicy Bypass -File "%RUN_PS1%" -Force',
        '    if errorlevel 1 echo Redeploy failed - launching whatever is already in this folder instead.',
        ') else (',
        '    echo Source repo not found at "%RUN_PS1%" - launching existing build without redeploying.',
        ')',
        'goto launch',
        ':run',
        "if not exist `"%~dp0$exeName`" (",
        '    echo Nothing is deployed here yet - deploying first.',
        '    goto deploy',
        ')',
        ':launch',
        "start `"`" `"%~dp0$exeName`""
    )
    # Rewrite only when the content changed: this may be the very file cmd is executing (the
    # launcher's Deploy & Run calls this script), and cmd re-reads a running batch file by offset.
    $text = ($lines -join "`r`n") + "`r`n"
    $current = if (Test-Path -LiteralPath $launchBatPath) { [IO.File]::ReadAllText($launchBatPath) } else { $null }
    if ($current -cne $text) { [IO.File]::WriteAllText($launchBatPath, $text, [Text.Encoding]::ASCII) }
    foreach ($stale in $staleLaunchers) {
        if (Test-Path -LiteralPath $stale) { Remove-Item -LiteralPath $stale -Force }
    }
    Write-Host "    Launcher -> $launchBatPath" -ForegroundColor Gray
}

if ($LauncherOnly) {
    Write-Launcher
    exit 0
}
# Every run refreshes the launcher of an existing install; a publish below rewrites it after
# replacing the folder.
if (Test-Path $out) { Write-Launcher }

# ── Pull latest, skip the rebuild entirely if nothing changed ──────────────
# Untracked files (e.g. .prose/ session state) don't block a pull; changes to tracked files (staged
# or not) skip it, as does a pull that fails: the desktop launcher runs this, and the ticket tools
# keep docs/USER_STORIES.md modified, so throwing here fell back to the installed copy and never
# redeployed (U.EVE-T43). It then deploys the commit already checked out.
$dirty = git -C $repoDir status --porcelain | Where-Object { $_ -notmatch '^\?\? ' }
if ($dirty) {
    Write-Host "  $repoDir has uncommitted changes - not pulling; deploying the current commit." -ForegroundColor DarkYellow
} else {
    git -C $repoDir pull --ff-only
    if ($LASTEXITCODE -ne 0) { Write-Host "  git pull --ff-only failed in $repoDir - deploying the current commit." -ForegroundColor DarkYellow }
}
$head = (git -C $repoDir rev-parse HEAD).Trim()
$upToDate = (-not $Force) -and (Test-Path (Join-Path $out $exeName)) -and (Test-Path $marker) -and
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

# ── Launcher (publishing replaced the folder) ─────────────────────────────────
Write-Launcher

Set-Content -LiteralPath $marker -Value $head -NoNewline

# ── Desktop shortcut ─────────────────────────────────────────────────────────
# Points at run.bat (not the exe directly), which asks first: Deploy & Run, or Run (MAT-T67).
$shortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Unity.Eve.lnk'
$link = (New-Object -ComObject WScript.Shell).CreateShortcut($shortcut)
$link.TargetPath       = $runBat
$link.WorkingDirectory = $repoDir
$link.IconLocation     = "$exe,0"
$link.Description      = 'Unity.Eve: 1) Deploy & Run (pull, rebuild, launch) or 2) Run the published build'
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
