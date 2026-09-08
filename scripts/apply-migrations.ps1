# Applies the pending database migrations in database/migrations to the configured SQL Server
# (SqlServer:ConnectionString in src/ProcurementCopilot.Admin/appsettings.json, or the SqlServer__ConnectionString
# environment variable). Scripts are idempotent and record themselves in Procurement.SchemaMigration.
#   -Status   only print which scripts are applied / pending
param([switch]$Status)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$flag = if ($Status) { '--status' } else { '--migrate' }
dotnet run --project (Join-Path $root 'src' 'ProcurementCopilot.Admin') -- $flag
exit $LASTEXITCODE
