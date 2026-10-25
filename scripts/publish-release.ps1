<#
.SYNOPSIS
    NexIsland Automated Release Publishing Script
.DESCRIPTION
    Compiles and publishes NexIsland in Release configuration for win-x64.
#>

[CmdletBinding()]
param(
    [switch]$SingleFile = $false,
    [switch]$SelfContained = $false,
    [string]$OutputDir = ""
)

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $repoRoot = Split-Path $PSScriptRoot -Parent
    $OutputDir = Join-Path $repoRoot "dist\NexIsland-win-x64"
}

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "   NEXISLAND - PRODUCTION PUBLISH SCRIPT     " -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

# Locate the active dotnet CLI with SDK
$dotnetCmd = "dotnet"
$userDotnet = "$env:USERPROFILE\.dotnet\dotnet.exe"
if (Test-Path $userDotnet) {
    $dotnetCmd = $userDotnet
    $env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet"
    $env:PATH = "$env:USERPROFILE\.dotnet;$env:PATH"
}

$projectPath = "$PSScriptRoot\..\src\NexIsland\NexIsland.csproj"

$argsList = @(
    "publish",
    $projectPath,
    "-c", "Release",
    "-r", "win-x64",
    "-o", $OutputDir
)

if ($SingleFile) {
    Write-Host "Mode: Single-File Executable" -ForegroundColor Yellow
    $argsList += "-p:PublishSingleFile=true"
    $argsList += "-p:IncludeNativeLibrariesForSelfExtract=true"
}

if ($SelfContained) {
    Write-Host "Mode: Self-Contained (Bundling .NET Runtime)" -ForegroundColor Yellow
    $argsList += "--self-contained", "true"
} else {
    Write-Host "Mode: Framework-Dependent (.NET 8 Runtime)" -ForegroundColor Yellow
    $argsList += "--self-contained", "false"
}

Write-Host "Using .NET compiler: $dotnetCmd" -ForegroundColor DarkCyan
Write-Host "Publishing to: $OutputDir ..." -ForegroundColor Green
& $dotnetCmd @argsList

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n[SUCCESS] NexIsland published successfully to $OutputDir" -ForegroundColor Green
} else {
    Write-Host "`n[ERROR] Publishing failed with exit code $LASTEXITCODE" -ForegroundColor Red
}
