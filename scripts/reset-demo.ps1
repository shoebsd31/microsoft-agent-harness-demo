# Resets the harness demo to a clean slate.
# Deletes the runtime folders next to the console executable for every build configuration:
#   <repo>/src/ProcurementCopilot.Console/bin/<Debug|Release>/net10.0/logs
#   <repo>/src/ProcurementCopilot.Console/bin/<Debug|Release>/net10.0/workspace   (output, agent-file-memory and the rfps copies)
# The read-only workspace/rfps, data and skills copies are recreated by the next `dotnet build`.
#
#   -Sessions   also delete the persisted sessions (%LOCALAPPDATA%\ProcurementCopilot\sessions or Sessions:Directory)
#   -All        -Sessions plus the TestResults folders
#   -Database   also remove the purchase orders and award recommendations the copilot wrote to SQL Server
#               (runs database/maintenance/reset-demo-awards.sql with sqlcmd; server and database from -Server/-Database)
#   -WhatIf     show what would be deleted without deleting
param(
    [switch]$Sessions,
    [switch]$All,
    [switch]$Database,
    [string]$Server = 'localhost\SQLEXPRESS',
    [string]$DatabaseName = 'AdventureWorks2019',
    [switch]$WhatIf
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$consoleBin = Join-Path $root 'src' 'ProcurementCopilot.Console' 'bin'

$targets = @()
if (Test-Path $consoleBin) {
    $targets += Get-ChildItem -Path $consoleBin -Directory -Recurse -Depth 2 |
        Where-Object { $_.Name -in @('logs', 'workspace') } |
        Select-Object -ExpandProperty FullName
}
if ($Sessions -or $All) {
    $targets += Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'ProcurementCopilot' 'sessions'
}
if ($All) {
    $targets += Join-Path $root 'TestResults'
    $targets += Get-ChildItem -Path (Join-Path $root 'tests') -Directory -Recurse -Depth 1 |
        Where-Object { $_.Name -eq 'TestResults' } | Select-Object -ExpandProperty FullName
}

$targets = $targets | Where-Object { $_ -and (Test-Path $_) } | Sort-Object -Unique
if (-not $targets) {
    Write-Host 'Nothing to reset: no runtime folders found.' -ForegroundColor Green
    exit 0
}

foreach ($path in $targets) {
    if ($WhatIf) {
        Write-Host "Would delete $path" -ForegroundColor Yellow
    } else {
        Remove-Item -LiteralPath $path -Recurse -Force -Confirm:$false
        Write-Host "Deleted $path" -ForegroundColor Cyan
    }
}
if ($Database) {
    $script = Join-Path $root 'database' 'maintenance' 'reset-demo-awards.sql'
    if ($WhatIf) { Write-Host "Would run $script against $Server/$DatabaseName" -ForegroundColor Yellow }
    else { sqlcmd -S $Server -E -d $DatabaseName -C -b -i $script; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE } }
}
if (-not $WhatIf) {
    Write-Host "Reset complete. Run 'dotnet build' (or 'dotnet run') to recreate the read-only workspace inputs." -ForegroundColor Green
}
