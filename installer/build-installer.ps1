<#
.SYNOPSIS
    Builds the Inno Setup installer for NexIsland
#>

$possiblePaths = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
)

$innoCompiler = $possiblePaths | Where-Object { Test-Path $_ } | Select-Object -First 1
$issScript = "$PSScriptRoot\NexIsland.iss"

if (-not $innoCompiler) {
    Write-Host "[INFO] Inno Setup compiler (ISCC.exe) was not found." -ForegroundColor Yellow
    Write-Host "To generate the standalone setup installer:" -ForegroundColor Yellow
    Write-Host "1. Install Inno Setup: winget install JRSoftware.InnoSetup" -ForegroundColor Yellow
    Write-Host "2. Re-run this script: .\build-installer.ps1" -ForegroundColor Yellow
    exit 0
}

Write-Host "Found Inno Setup at: $innoCompiler" -ForegroundColor Cyan
Write-Host "Compiling NexIsland setup installer..." -ForegroundColor Green

& $innoCompiler $issScript

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n[SUCCESS] Setup installer generated successfully in dist\installer\" -ForegroundColor Green
} else {
    Write-Host "`n[ERROR] Inno Setup compilation failed with exit code $LASTEXITCODE" -ForegroundColor Red
}
