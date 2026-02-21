#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build Windows application for Physiquinator
.DESCRIPTION
    Builds the .NET MAUI Windows app and optionally creates a ZIP package
.PARAMETER OutputPath
    Output directory for the build (default: ./artifacts/windows)
.PARAMETER CreateZip
    Create a ZIP file of the output
.PARAMETER ZipPath
    Path for the ZIP file (default: ./Physiquinator-Windows.zip)
.EXAMPLE
    .\build-windows.ps1
    .\build-windows.ps1 -CreateZip
    .\build-windows.ps1 -OutputPath ./build -CreateZip -ZipPath ./release.zip
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$OutputPath = "./artifacts/windows",
    
    [Parameter()]
    [switch]$CreateZip,
    
    [Parameter()]
    [string]$ZipPath = "./Physiquinator-Windows.zip"
)

$ErrorActionPreference = 'Stop'

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host " Physiquinator Windows Build" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Build the application
Write-Host "Building Windows application..." -ForegroundColor Yellow
$buildStartTime = Get-Date

try {
    dotnet publish Physiquinator.csproj `
        -f net10.0-windows10.0.19041.0 `
        -c Release `
        -p:WindowsPackageType=None `
        -p:SelfContained=false `
        -p:PublishTrimmed=false `
        -o $OutputPath
    
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE"
    }
    
    $buildDuration = (Get-Date) - $buildStartTime
    $durationStr = "{0:mm}:{0:ss}" -f $buildDuration
    Write-Host "`n[SUCCESS] Build completed in $durationStr" -ForegroundColor Green
}
catch {
    Write-Host "`n[ERROR] Build failed: $_" -ForegroundColor Red
    exit 1
}

# Verify output
$exePath = Join-Path $OutputPath "Physiquinator.exe"
if (-not (Test-Path $exePath)) {
    Write-Host "[ERROR] Output executable not found at $exePath" -ForegroundColor Red
    exit 1
}

$exeInfo = Get-Item $exePath
$sizeMB = [math]::Round($exeInfo.Length / 1MB, 2)
Write-Host "`nOutput location: $($exeInfo.FullName)" -ForegroundColor Cyan
Write-Host "Executable size: ${sizeMB} MB" -ForegroundColor Cyan

# Create ZIP if requested
if ($CreateZip) {
    Write-Host "`nCreating ZIP package..." -ForegroundColor Yellow
    
    if (Test-Path $ZipPath) {
        Remove-Item $ZipPath -Force
    }
    
    try {
        Compress-Archive -Path "$OutputPath\*" -DestinationPath $ZipPath -Force
        
        $zipInfo = Get-Item $ZipPath
        $zipSizeMB = [math]::Round($zipInfo.Length / 1MB, 2)
        Write-Host "[SUCCESS] ZIP created: $($zipInfo.FullName)" -ForegroundColor Green
        Write-Host "ZIP size: ${zipSizeMB} MB" -ForegroundColor Cyan
    }
    catch {
        Write-Host "[ERROR] Failed to create ZIP: $_" -ForegroundColor Red
        exit 1
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host " Build Complete!" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "Run the application:" -ForegroundColor Yellow
Write-Host "  $exePath`n" -ForegroundColor White

exit 0
