#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build Windows application for Physiquinator
.DESCRIPTION
    Builds the .NET MAUI Windows app and optionally creates a ZIP package.
    Supports both framework-dependent and portable (with WindowsAppSDK) builds.
.PARAMETER OutputPath
    Output directory for the build (default: ./artifacts/windows)
.PARAMETER Portable
    Create a portable build with WindowsAppSDK runtime included (larger but fewer dependencies)
.PARAMETER CreateZip
    Create a ZIP file of the output
.PARAMETER ZipPath
    Path for the ZIP file (default: ./Physiquinator-Windows.zip or ./Physiquinator-Windows-Portable.zip)
.EXAMPLE
    .\build-windows.ps1
    .\build-windows.ps1 -CreateZip
    .\build-windows.ps1 -Portable -CreateZip
    .\build-windows.ps1 -OutputPath ./build -CreateZip -ZipPath ./release.zip
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$OutputPath,

    [Parameter()]
    [switch]$Portable,

    [Parameter()]
    [switch]$CreateZip,

    [Parameter()]
    [string]$ZipPath
)

$ErrorActionPreference = 'Stop'

# Set defaults based on build type
if (-not $OutputPath) {
    $OutputPath = if ($Portable) { "./artifacts/windows-portable" } else { "./artifacts/windows" }
}

if (-not $ZipPath) {
    $ZipPath = if ($Portable) { "./Physiquinator-Windows-Portable.zip" } else { "./Physiquinator-Windows.zip" }
}

$buildType = if ($Portable) { "Portable (WindowsAppSDK)" } else { "Framework-Dependent" }

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host " Physiquinator Windows Build" -ForegroundColor Cyan
Write-Host " Type: $buildType" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Build the application
Write-Host "Building Windows application..." -ForegroundColor Yellow
if ($Portable) {
    Write-Host "Mode: Portable (includes WindowsAppSDK, still requires .NET 10 Runtime)" -ForegroundColor Yellow
} else {
    Write-Host "Mode: Framework-dependent (requires .NET 10 Runtime)" -ForegroundColor Yellow
}

$buildStartTime = Get-Date

try {
    $buildArgs = @(
        "publish"
        "Physiquinator.csproj"
        "-f", "net10.0-windows10.0.19041.0"
        "-c", "Release"
        "-p:WindowsPackageType=None"
        "-p:SelfContained=false"
        "-p:PublishTrimmed=false"
        "-o", $OutputPath
    )

    if ($Portable) {
        $buildArgs += "-p:WindowsAppSDKSelfContained=true"
    }

    & dotnet @buildArgs

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

# Calculate total size
$totalSize = (Get-ChildItem $OutputPath -Recurse | Measure-Object -Property Length -Sum).Sum
$totalSizeMB = [math]::Round($totalSize / 1MB, 2)

$exeInfo = Get-Item $exePath
$exeSizeMB = [math]::Round($exeInfo.Length / 1MB, 2)

Write-Host "`nBuild Information:" -ForegroundColor Cyan
Write-Host "  Output location: $($exeInfo.Directory.FullName)" -ForegroundColor White
Write-Host "  Executable: Physiquinator.exe (${exeSizeMB} MB)" -ForegroundColor White
Write-Host "  Total size: ${totalSizeMB} MB" -ForegroundColor White
Write-Host "  Build type: $buildType" -ForegroundColor White

if (-not $Portable) {
    Write-Host "`nRuntime Requirements:" -ForegroundColor Yellow
    Write-Host "  - .NET 10 Desktop Runtime (x64)" -ForegroundColor White
    Write-Host "  Download: https://dotnet.microsoft.com/download/dotnet/10.0" -ForegroundColor Gray
}

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

if ($CreateZip) {
    Write-Host "Distribution package:" -ForegroundColor Yellow
    Write-Host "  $ZipPath`n" -ForegroundColor White
}

exit 0
