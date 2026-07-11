#!/usr/bin/env pwsh
$ErrorActionPreference = 'Stop'

$ScriptDir = $PSScriptRoot
$ProjectDir = Resolve-Path "$ScriptDir/../.."

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Physiquinator Screenshot Generation" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Step 1: Build the .NET MAUI Windows app in Debug configuration
Write-Host "Building Windows app in Debug configuration..." -ForegroundColor Yellow
Push-Location $ProjectDir
try {
    dotnet publish Physiquinator.csproj `
        -f net10.0-windows10.0.19041.0 `
        -c Debug `
        -p:WindowsPackageType=None `
        -p:WindowsAppSDKSelfContained=true `
        -p:SelfContained=false `
        -p:PublishTrimmed=false `
        -o ./artifacts/windows-debug

    if ($LASTEXITCODE -ne 0) {
        Write-Host "[ERROR] dotnet publish failed!" -ForegroundColor Red
        exit 1
    }
}
finally {
    Pop-Location
}

Write-Host "[SUCCESS] App built successfully in artifacts/windows-debug/`n" -ForegroundColor Green

# Step 2: Install dependencies in tools/screenshot-generator
Write-Host "Installing Node.js dependencies for screenshot generator..." -ForegroundColor Yellow
Push-Location "$ScriptDir"
try {
    npm install
    Write-Host "Ensuring Playwright chromium is installed..." -ForegroundColor Yellow
    npx playwright install chromium
}
catch {
    Write-Host "[ERROR] Failed to install npm dependencies or Playwright: $_" -ForegroundColor Red
    Pop-Location
    exit 1
}

# Step 3: Run the screenshot script
Write-Host "Running Playwright automation script..." -ForegroundColor Yellow
try {
    node screenshot.js
    if ($LASTEXITCODE -ne 0) {
        throw "Screenshot script exited with code $LASTEXITCODE"
    }
    Write-Host "[SUCCESS] Screenshot generation completed!" -ForegroundColor Green
}
catch {
    Write-Host "[ERROR] Screenshot script execution failed: $_" -ForegroundColor Red
    Pop-Location
    exit 1
}
finally {
    Pop-Location
}

exit 0
