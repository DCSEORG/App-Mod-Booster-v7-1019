<#
.SYNOPSIS
    Deploys the Expense Management application to Azure App Service.

.DESCRIPTION
    Builds the .NET application, packages it, and deploys to Azure App Service.
    Reads deployment context from .deployment-context.json if available.

.PARAMETER ResourceGroup
    Optional. Azure resource group name. Overrides value from context file.

.PARAMETER WebAppName
    Optional. Azure Web App name. Overrides value from context file.

.PARAMETER SkipBuild
    Switch. When specified, skips the dotnet publish step.

.PARAMETER ConfigureSettings
    Switch. When specified, also configures App Service settings from context.

.EXAMPLE
    # After running deploy-infra/deploy.ps1, simply run:
    .\deploy-app\deploy.ps1

.EXAMPLE
    .\deploy-app\deploy.ps1 -ResourceGroup "rg-expensemgmt-20240101" -WebAppName "app-expensemgmt-abc123"
#>

param(
    [Parameter(Mandatory = $false)]
    [string]$ResourceGroup,

    [Parameter(Mandatory = $false)]
    [string]$WebAppName,

    [switch]$SkipBuild,

    [switch]$ConfigureSettings
)

$ErrorActionPreference = "Stop"

# ============================================================
# Resolve paths
# ============================================================
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
$contextFile = Join-Path $repoRoot ".deployment-context.json"
$projectFile = Join-Path $repoRoot "src" "ExpenseManagement" "ExpenseManagement.csproj"
$publishDir = Join-Path $scriptDir "publish"
$zipFile = Join-Path $scriptDir "app.zip"

# ============================================================
# Load context file
# ============================================================
$context = $null
if (Test-Path $contextFile) {
    Write-Host "Loading deployment context from $contextFile..." -ForegroundColor Cyan
    $context = Get-Content $contextFile | ConvertFrom-Json
}

# Resolve resource group and web app name
if ([string]::IsNullOrEmpty($ResourceGroup)) {
    if ($null -ne $context -and -not [string]::IsNullOrEmpty($context.resourceGroup)) {
        $ResourceGroup = $context.resourceGroup
    } else {
        Write-Error "ResourceGroup not specified and not found in .deployment-context.json. Please specify -ResourceGroup."
        exit 1
    }
}

if ([string]::IsNullOrEmpty($WebAppName)) {
    if ($null -ne $context -and -not [string]::IsNullOrEmpty($context.webAppName)) {
        $WebAppName = $context.webAppName
    } else {
        Write-Error "WebAppName not specified and not found in .deployment-context.json. Please specify -WebAppName."
        exit 1
    }
}

Write-Host ""
Write-Host "============================================================" -ForegroundColor Green
Write-Host "  EXPENSE MANAGEMENT - APPLICATION DEPLOYMENT" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green
Write-Host "  Resource Group: $ResourceGroup" -ForegroundColor White
Write-Host "  Web App:        $WebAppName" -ForegroundColor White
Write-Host "============================================================" -ForegroundColor Green
Write-Host ""

# ============================================================
# Check Azure CLI
# ============================================================
Write-Host "[1/5] Checking Azure CLI..." -ForegroundColor Green

try {
    $null = az version 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Azure CLI not found" }
} catch {
    Write-Error "Azure CLI is not installed. Install from: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    exit 1
}

$accountJson = az account show 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Error "Not logged in to Azure. Run 'az login' first."
    exit 1
}

$account = $accountJson | ConvertFrom-Json
Write-Host "  Logged in as: $($account.user.name)" -ForegroundColor Cyan

# ============================================================
# Build application
# ============================================================
if (-not $SkipBuild) {
    Write-Host "`n[2/5] Building application..." -ForegroundColor Green

    if (-not (Test-Path $projectFile)) {
        Write-Error "Project file not found at: $projectFile"
        exit 1
    }

    # Clean previous publish output
    if (Test-Path $publishDir) {
        Remove-Item -Path $publishDir -Recurse -Force
    }

    Write-Host "  Running: dotnet publish $projectFile -c Release -o $publishDir" -ForegroundColor Yellow

    dotnet publish $projectFile -c Release -o $publishDir

    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet publish failed."
        exit 1
    }

    Write-Host "  Build successful." -ForegroundColor Cyan
} else {
    Write-Host "`n[2/5] Skipping build (SkipBuild flag set)." -ForegroundColor Yellow

    if (-not (Test-Path $publishDir)) {
        Write-Error "Publish directory not found at: $publishDir. Cannot skip build if no previous build exists."
        exit 1
    }
}

# ============================================================
# Create deployment zip
# ============================================================
Write-Host "`n[3/5] Creating deployment package..." -ForegroundColor Green

# Remove old zip if exists
if (Test-Path $zipFile) {
    Remove-Item -Path $zipFile -Force
}

# Create zip with files at root level (not in subdirectory)
# Azure App Service expects DLL files at the root of the zip
Compress-Archive -Path "$publishDir\*" -DestinationPath $zipFile

$zipSize = (Get-Item $zipFile).Length / 1MB
Write-Host "  Package created: app.zip ($([math]::Round($zipSize, 2)) MB)" -ForegroundColor Cyan

# ============================================================
# Deploy to App Service
# ============================================================
Write-Host "`n[4/5] Deploying to Azure App Service..." -ForegroundColor Green
Write-Host "  This may take 2-5 minutes..." -ForegroundColor Yellow

az webapp deploy `
    --resource-group $ResourceGroup `
    --name $WebAppName `
    --src-path $zipFile `
    --type zip `
    --clean true `
    --restart true `
    --output none

if ($LASTEXITCODE -ne 0) {
    Write-Error "Deployment to App Service failed."
    exit 1
}

Write-Host "  Deployment successful." -ForegroundColor Cyan

# ============================================================
# Configure settings (optional)
# ============================================================
if ($ConfigureSettings -and $null -ne $context) {
    Write-Host "`n[4b] Configuring App Service settings from context..." -ForegroundColor Green

    $connectionString = "Server=tcp:$($context.sqlServerFqdn),1433;Initial Catalog=Northwind;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity;User Id=$($context.managedIdentityClientId);"

    az webapp config appsettings set `
        --resource-group $ResourceGroup `
        --name $WebAppName `
        --settings `
            "AZURE_CLIENT_ID=$($context.managedIdentityClientId)" `
            "ManagedIdentityClientId=$($context.managedIdentityClientId)" `
        --output none

    az webapp config connection-string set `
        --resource-group $ResourceGroup `
        --name $WebAppName `
        --settings "DefaultConnection=$connectionString" `
        --connection-string-type SQLAzure `
        --output none

    Write-Host "  Settings configured." -ForegroundColor Cyan
}

# ============================================================
# Cleanup
# ============================================================
Write-Host "`n[5/5] Cleaning up..." -ForegroundColor Green

if (Test-Path $zipFile) {
    Remove-Item -Path $zipFile -Force
    Write-Host "  Removed temporary zip file." -ForegroundColor Cyan
}

# ============================================================
# Summary
# ============================================================
$appUrl = "https://$WebAppName.azurewebsites.net"

Write-Host ""
Write-Host "============================================================" -ForegroundColor Green
Write-Host "  DEPLOYMENT COMPLETE" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green
Write-Host "  Application URL:  $appUrl/Index" -ForegroundColor White
Write-Host "  Swagger UI:       $appUrl/swagger" -ForegroundColor White
Write-Host "  Chat Interface:   $appUrl/Chat" -ForegroundColor White
Write-Host "============================================================" -ForegroundColor Green
Write-Host ""
Write-Host "Note: The application URL is /Index (not the root URL)." -ForegroundColor Yellow
Write-Host "Allow 1-2 minutes for the application to start after deployment." -ForegroundColor Yellow
Write-Host ""
