<#
.SYNOPSIS
  The gate: pure-Core rules under dotnet test, optionally a Unity compile check and the EditMode suite.
.EXAMPLE
  pwsh tools/test.ps1                          # Core rules (seconds, no Unity)
  pwsh tools/test.ps1 -Compile                 # + batch-mode Unity compile of Runtime/Editor
  pwsh tools/test.ps1 -EditMode                # + the Unity EditMode suite (Assets/UnityEve/Tests/EditMode)
  pwsh tools/test.ps1 -EditMode -Filter SaveSystemTests   # only tests whose name matches, in both suites
.NOTES
  -EditMode runs Unity in batch mode, so the Editor must not have the project open (unity.ps1 refuses).
  Results land in Logs/editmode-results.xml (NUnit 3 format).
#>
param(
    [switch]$Compile,
    [switch]$EditMode,
    [string]$Filter = ""
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

$dotnetArgs = @("test", "$root/Tests/Core.Tests/Core.Tests.csproj", "--nologo")
if ($Filter) { $dotnetArgs += @("--filter", "FullyQualifiedName~$Filter") }
dotnet @dotnetArgs
if ($LASTEXITCODE -ne 0) { Write-Host "FAIL core tests"; exit $LASTEXITCODE }
Write-Host "PASS core tests"

if ($Compile -and -not $EditMode) {
    & "$PSScriptRoot/unity.ps1" -Method Unity.Eve.Editor.UnityEveBuild.CompileCheck -Log compile
    if ($LASTEXITCODE -ne 0) { Write-Host "FAIL unity compile"; exit $LASTEXITCODE }
    Write-Host "PASS unity compile"
}

if ($EditMode) {
    # Running the suite compiles every assembly first, so it is also the compile check.
    $results = Join-Path $root "Logs/editmode-results.xml"
    if (Test-Path $results) { Remove-Item $results }
    $extra = "-runTests -testPlatform EditMode -testResults $results"
    if ($Filter) { $extra += " -testFilter $Filter" }
    & "$PSScriptRoot/unity.ps1" -Log editmode -Extra $extra
    $code = $LASTEXITCODE
    if (-not (Test-Path $results)) { Write-Host "FAIL editmode tests: no results (compile error? see Logs/editmode.log)"; exit ($(if ($code) { $code } else { 1 })) }
    $run = ([xml](Get-Content $results -Raw)).'test-run'
    Write-Host "editmode: $($run.passed)/$($run.total) passed, $($run.failed) failed, $($run.skipped) skipped"
    if ($code -ne 0 -or [int]$run.failed -gt 0) {
        ([xml](Get-Content $results -Raw)).SelectNodes("//test-case[@result='Failed']") | ForEach-Object { Write-Host "  FAIL $($_.fullname)" }
        Write-Host "FAIL editmode tests"
        exit ($(if ($code) { $code } else { 1 }))
    }
    Write-Host "PASS editmode tests"
}
