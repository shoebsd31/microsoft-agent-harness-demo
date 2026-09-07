# Full verification: restore, build (warnings as errors), test with coverage, vulnerability scan, file-length check, self-check.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

function Step($name, [scriptblock]$action) {
    Write-Host ""
    Write-Host "==> $name" -ForegroundColor Cyan
    & $action
    if ($LASTEXITCODE -ne 0) { Write-Host "FAILED: $name" -ForegroundColor Red; exit $LASTEXITCODE }
}

Step 'dotnet restore'                     { dotnet restore ProcurementCopilot.slnx }
Step 'dotnet build -warnaserror'          { dotnet build ProcurementCopilot.slnx --no-restore -warnaserror }
Step 'dotnet test (with coverage)'        { dotnet test --solution ProcurementCopilot.slnx --no-build --coverage --coverage-settings (Join-Path $root 'coverage.runsettings') --coverage-output-format cobertura --results-directory (Join-Path $root 'TestResults') }
Step 'dotnet list package --vulnerable'   {
    $out = dotnet list ProcurementCopilot.slnx package --vulnerable --include-transitive 2>&1 | Out-String
    Write-Host $out
    if ($out -match 'has the following vulnerable packages') { Write-Host 'Vulnerable packages found.' -ForegroundColor Red; exit 1 }
    $global:LASTEXITCODE = 0
}
Step '150-line limit'                     { & (Join-Path $PSScriptRoot 'check-file-length.ps1') }
Step 'self-check (no model call)'         { dotnet run --project src/ProcurementCopilot.Console --no-build -- --self-check }

Write-Host ""
Write-Host "All verification steps passed." -ForegroundColor Green
