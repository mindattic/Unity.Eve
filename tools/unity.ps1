<#
.SYNOPSIS
  Runs the Unity.Eve project in Unity batch mode and surfaces compile errors.
.EXAMPLE
  pwsh tools/unity.ps1 -Method Unity.Eve.Editor.DemoContentBuilder.BuildAll -Log build-content
  pwsh tools/unity.ps1 -Method Unity.Eve.Editor.UnityEveBuild.BuildWindows -Log build-player -Graphics
.NOTES
  Unity batch mode needs the Editor closed on this project. This script refuses to run if it is open.
#>
param(
    [string]$Method,
    [string]$Log = "unity",
    [switch]$Graphics,
    [string]$Extra = ""
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$version = (Get-Content "$root/ProjectSettings/ProjectVersion.txt" | Select-String 'm_EditorVersion:').ToString().Split(':')[1].Trim()
$unity = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
if (-not (Test-Path $unity)) { throw "Unity $version not found at $unity" }

if (Test-Path "$root/Temp/UnityLockfile") {
    $locked = $false
    try { [IO.File]::Open("$root/Temp/UnityLockfile", 'Open', 'ReadWrite', 'None').Close() } catch { $locked = $true }
    if ($locked) { Write-Error "The project is open in the Unity Editor. Close it and re-run."; exit 3 }
}

New-Item -ItemType Directory -Force "$root/Logs" | Out-Null
$logFile = "$root/Logs/$Log.log"
$argList = @("-batchmode", "-projectPath", "`"$root`"", "-logFile", "`"$logFile`"")
if (-not $Graphics) { $argList += "-nographics" }
if ($Method) { $argList += @("-executeMethod", $Method) }
if ($Extra) { $argList += $Extra.Split(' ') }
if ($Extra -notmatch 'runTests') { $argList += "-quit" }

$p = Start-Process -FilePath $unity -ArgumentList $argList -Wait -PassThru -NoNewWindow
Select-String -Path $logFile -Pattern 'error CS|\[Unity\.Eve\]|\[Build\]|Shader error|Compilation failed' |
    Select-Object -First 80 | ForEach-Object { $_.Line }
Write-Host "exit=$($p.ExitCode)  log=$logFile"
exit $p.ExitCode
