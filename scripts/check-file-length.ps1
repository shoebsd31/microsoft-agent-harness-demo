# Fails when any .cs file under src/ or tests/ exceeds 150 lines (excluding using directives and blank lines).
param([int]$Max = 150)
$root = Split-Path -Parent $PSScriptRoot
$failed = $false
Get-ChildItem -Path (Join-Path $root 'src'), (Join-Path $root 'tests') -Recurse -Filter *.cs |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
    ForEach-Object {
        $count = (Get-Content $_.FullName | Where-Object { $_.Trim() -ne '' -and $_ -notmatch '^\s*using\s' -and $_ -notmatch '^\s*global\s+using\s' }).Count
        if ($count -gt $Max) {
            Write-Host "TOO LONG ($count lines): $($_.FullName)"
            $failed = $true
        }
    }
if ($failed) { exit 1 }
Write-Host "All .cs files are within $Max lines."
