#Requires -Version 7.0

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host " AI Teammate - Development Environment Setup" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

function Test-Command {
    param([string]$Name)
    if (Get-Command $Name -ErrorAction SilentlyContinue) {
        $version = & $Name --version 2>$null | Select-Object -First 1
        Write-Host "  ✓ $Name found: $version" -ForegroundColor Green
        return $true
    }
    else {
        Write-Host "  ✗ $Name not found" -ForegroundColor Red
        return $false
    }
}

Write-Host ""
Write-Host "1. Checking prerequisites..." -ForegroundColor Yellow
Write-Host "-------------------------------------------"

# .NET SDK
if (Test-Command "dotnet") {
    $dotnetVersion = dotnet --version
    $major = [int]($dotnetVersion.Split('.')[0])
    if ($major -lt 9) {
        Write-Host "  ⚠ .NET SDK $dotnetVersion found, but 9.0+ is required" -ForegroundColor Yellow
        Write-Host "  Install from: https://dot.net/download"
    }
}
else {
    Write-Host "  Install from: https://dot.net/download"
}

# Node.js
if (Test-Command "node") {
    $nodeVersion = (node --version).TrimStart('v')
    $major = [int]($nodeVersion.Split('.')[0])
    if ($major -lt 22) {
        Write-Host "  ⚠ Node.js $nodeVersion found, but 22+ is recommended" -ForegroundColor Yellow
    }
}
else {
    Write-Host "  Install from: https://nodejs.org/"
}

# Azure CLI
if (-not (Test-Command "az")) {
    Write-Host "  Install from: https://aka.ms/install-azure-cli"
}

# Azure Developer CLI
if (-not (Test-Command "azd")) {
    Write-Host "  Install: winget install Microsoft.Azd"
}

# Docker
if (-not (Test-Command "docker")) {
    Write-Host "  Install from: https://docs.docker.com/get-docker/"
}

Write-Host ""
Write-Host "2. Restoring .NET dependencies..." -ForegroundColor Yellow
Write-Host "-------------------------------------------"
Push-Location (Join-Path $PSScriptRoot "..")
dotnet restore TeamsAITeammate.sln

Write-Host ""
Write-Host "3. Building solution..." -ForegroundColor Yellow
Write-Host "-------------------------------------------"
dotnet build TeamsAITeammate.sln -c Debug

Write-Host ""
Write-Host "4. Checking appsettings.Development.json..." -ForegroundColor Yellow
Write-Host "-------------------------------------------"
$devSettings = "src/TeamsAITeammate.Agent/appsettings.Development.json"
if (Test-Path $devSettings) {
    Write-Host "  ⚠ $devSettings already exists, skipping" -ForegroundColor Yellow
}
else {
    Write-Host "  Created $devSettings — fill in your values"
}

Write-Host ""
Write-Host "5. Dev Tunnel setup..." -ForegroundColor Yellow
Write-Host "-------------------------------------------"
if (Get-Command "devtunnel" -ErrorAction SilentlyContinue) {
    Write-Host "  ✓ devtunnel CLI found" -ForegroundColor Green
    Write-Host "  To create a tunnel: devtunnel create --allow-anonymous"
    Write-Host "  To start: devtunnel host --port 5000"
}
else {
    Write-Host "  ⚠ devtunnel CLI not found" -ForegroundColor Yellow
    Write-Host "  Install: winget install Microsoft.DevTunnel"
}

Pop-Location

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host " Setup complete!" -ForegroundColor Green
Write-Host ""
Write-Host " Next steps:"
Write-Host "  1. Fill in appsettings.Development.json with your Bot ID and secrets"
Write-Host "  2. Run: devtunnel host --port 5000"
Write-Host "  3. Run: dotnet run --project src/TeamsAITeammate.Agent"
Write-Host "  4. Sideload the Teams app from appPackage/"
Write-Host "=========================================" -ForegroundColor Cyan
