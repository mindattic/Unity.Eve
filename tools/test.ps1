<#
.SYNOPSIS
  The gate: pure-Core rules under dotnet test, optionally a Unity compile check.
.EXAMPLE
  pwsh tools/test.ps1            # Core rules (seconds, no Unity)
  pwsh tools/test.ps1 -Compile   # + batch-mode Unity compile of Runtime/Editor
#>
param([switch]$Compile)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

dotnet test "$root/Tests/Core.Tests/Core.Tests.csproj" --nologo
if ($LASTEXITCODE -ne 0) { Write-Host "FAIL core tests"; exit $LASTEXITCODE }
Write-Host "PASS core tests"

if ($Compile) {
    & "$PSScriptRoot/unity.ps1" -Method Unity.Eve.Editor.UnityEveBuild.CompileCheck -Log compile
    if ($LASTEXITCODE -ne 0) { Write-Host "FAIL unity compile"; exit $LASTEXITCODE }
    Write-Host "PASS unity compile"
}
