<#
.SYNOPSIS
    Deploys all Azure infrastructure for the Expense Management application.

.DESCRIPTION
    This script deploys all Azure resources including App Service, Azure SQL,
    Managed Identity, Monitoring, and optionally GenAI resources.
    It also configures the database schema, stored procedures, and App Service settings.

.PARAMETER ResourceGroup
    Required. The Azure resource group name. Use a unique name (e.g., include date suffix).

.PARAMETER Location
    Required. The Azure region (e.g., 'uksouth').

.PARAMETER BaseName
    Optional. Base name for resources. Defaults to 'expensemgmt'.

.PARAMETER DeployGenAI
    Switch. When specified, deploys Azure OpenAI and AI Search resources.

.PARAMETER SkipDatabaseSetup
    Switch. When specified, skips database schema import and stored procedure deployment.

.EXAMPLE
    .\deploy-infra\deploy.ps1 -ResourceGroup "rg-expensemgmt-20240101" -Location "uksouth"

.EXAMPLE
    .\deploy-infra\deploy.ps1 -ResourceGroup "rg-expensemgmt-20240101" -Location "uksouth" -DeployGenAI

.EXAMPLE
    .\deploy-infra\deploy.ps1 -ResourceGroup "rg-expensemgmt-20240101" -Location "uksouth" -SkipDatabaseSetup
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroup,

    [Parameter(Mandatory = $true)]
    [string]$Location,

    [Parameter(Mandatory = $false)]
    [string]$BaseName = "expensemgmt",

    [switch]$DeployGenAI,

    [switch]$SkipDatabaseSetup
)

# ============================================================
# Warn on PowerShell 5.1
# ============================================================
if ($PSVersionTable.PSVersion.Major -lt 7) {
    Write-Warning "You are running PowerShell $($PSVersionTable.PSVersion). PowerShell 7+ is recommended."
    Write-Warning "Download PowerShell 7+ from: https://github.com/PowerShell/PowerShell/releases"
    Write-Host ""
}

# ============================================================
# CI/CD Detection
# ============================================================
$IsCI = $env:GITHUB_ACTIONS -eq "true" -or $env:TF_BUILD -eq "true" -or $env:CI -eq "true"

if ($IsCI) {
    Write-Host "CI/CD environment detected." -ForegroundColor Cyan
} else {
    Write-Host "Interactive (local) environment detected." -ForegroundColor Cyan
}

# ============================================================
# Check Azure CLI
# ============================================================
Write-Host "`n[1/12] Checking Azure CLI..." -ForegroundColor Green

try {
    $null = az version 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Azure CLI not found" }
} catch {
    Write-Error "Azure CLI is not installed or not in PATH. Install from: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    exit 1
}

# Check logged in
$accountJson = az account show 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Error "Not logged in to Azure. Run 'az login' first."
    exit 1
}

$account = $accountJson | ConvertFrom-Json
Write-Host "  Logged in as: $($account.user.name)" -ForegroundColor Cyan
Write-Host "  Subscription: $($account.name) ($($account.id))" -ForegroundColor Cyan

# ============================================================
# Get admin credentials
# ============================================================
Write-Host "`n[2/12] Retrieving admin credentials..." -ForegroundColor Green

if ($IsCI) {
    # CI/CD mode: use service principal
    $spClientId = $env:AZURE_CLIENT_ID
    if ([string]::IsNullOrEmpty($spClientId)) {
        Write-Error "AZURE_CLIENT_ID environment variable is not set. Required for CI/CD deployment."
        exit 1
    }

    Write-Host "  Retrieving Service Principal details for Client ID: $spClientId" -ForegroundColor Cyan
    $spJson = az ad sp show --id $spClientId 2>&1 | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to retrieve Service Principal details. Ensure the SP exists and you have permission to read it."
        exit 1
    }

    $adminObjectId = $spJson.id
    $adminUpn = $spJson.displayName
    $adminPrincipalType = "Application"
    Write-Host "  Service Principal: $adminUpn ($adminObjectId)" -ForegroundColor Cyan
} else {
    # Interactive mode: use signed-in user
    $userJson = az ad signed-in-user show 2>&1 | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to retrieve signed-in user details. Ensure you are logged in with 'az login'."
        exit 1
    }

    $adminObjectId = $userJson.id
    $adminUpn = $userJson.userPrincipalName
    $adminPrincipalType = "User"
    Write-Host "  Signed-in user: $adminUpn ($adminObjectId)" -ForegroundColor Cyan
}

# ============================================================
# Create resource group
# ============================================================
Write-Host "`n[3/12] Creating resource group '$ResourceGroup'..." -ForegroundColor Green

az group create --name $ResourceGroup --location $Location --output none
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to create resource group '$ResourceGroup'."
    exit 1
}
Write-Host "  Resource group ready." -ForegroundColor Cyan

# ============================================================
# Deploy Bicep templates
# ============================================================
Write-Host "`n[4/12] Deploying Bicep infrastructure templates..." -ForegroundColor Green
Write-Host "  This may take 5-10 minutes..." -ForegroundColor Yellow

$deployGenAIValue = if ($DeployGenAI) { "true" } else { "false" }

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
$bicepFile = Join-Path $scriptDir "main.bicep"
$bicepParamFile = Join-Path $scriptDir "main.bicepparam"

az deployment group create `
    --resource-group $ResourceGroup `
    --name "main-$(Get-Date -Format 'yyyyMMddHHmmss')" `
    --template-file $bicepFile `
    --parameters $bicepParamFile `
    --parameters adminObjectId=$adminObjectId `
                 adminUserPrincipalName=$adminUpn `
                 adminPrincipalType=$adminPrincipalType `
                 deployGenAI=$deployGenAIValue `
                 location=$Location `
                 baseName=$BaseName `
    --output none

if ($LASTEXITCODE -ne 0) {
    Write-Error "Bicep deployment failed. Check the Azure portal for details."
    exit 1
}
Write-Host "  Infrastructure deployed successfully." -ForegroundColor Cyan

# ============================================================
# Retrieve deployment outputs
# ============================================================
Write-Host "`n[5/12] Retrieving deployment outputs..." -ForegroundColor Green

$outputs = az deployment group show `
    --resource-group $ResourceGroup `
    --name (az deployment group list --resource-group $ResourceGroup --query "[0].name" -o tsv) `
    --query "properties.outputs" | ConvertFrom-Json

$webAppName = $outputs.webAppName.value
$sqlServerFqdn = $outputs.sqlServerFqdn.value
$sqlServerName = $outputs.sqlServerName.value
$managedIdentityClientId = $outputs.managedIdentityClientId.value
$managedIdentityName = $outputs.managedIdentityName.value
$appInsightsConnectionString = $outputs.appInsightsConnectionString.value
$openAIEndpoint = $outputs.openAIEndpoint.value
$openAIModelName = $outputs.openAIModelName.value
$searchEndpoint = $outputs.searchEndpoint.value

Write-Host "  Web App: $webAppName" -ForegroundColor Cyan
Write-Host "  SQL Server: $sqlServerFqdn" -ForegroundColor Cyan
Write-Host "  Managed Identity Client ID: $managedIdentityClientId" -ForegroundColor Cyan

# ============================================================
# Wait for SQL Server
# ============================================================
Write-Host "`n[6/12] Waiting for SQL Server to be ready..." -ForegroundColor Green

$maxRetries = 12
$retryCount = 0
$sqlReady = $false

while (-not $sqlReady -and $retryCount -lt $maxRetries) {
    Start-Sleep -Seconds 15
    $retryCount++
    Write-Host "  Attempt $retryCount/$maxRetries..." -ForegroundColor Yellow

    $testResult = az sql server show --resource-group $ResourceGroup --name $sqlServerName --query "state" -o tsv 2>&1
    if ($testResult -eq "Ready") {
        $sqlReady = $true
        Write-Host "  SQL Server is ready." -ForegroundColor Cyan
    }
}

if (-not $sqlReady) {
    Write-Warning "SQL Server may not be fully ready. Proceeding anyway..."
}

# ============================================================
# Add current IP to SQL firewall
# ============================================================
Write-Host "`n[7/12] Adding current IP to SQL Server firewall..." -ForegroundColor Green

try {
    $currentIp = (Invoke-RestMethod -Uri "https://api.ipify.org?format=text" -TimeoutSec 10).Trim()
    Write-Host "  Current IP: $currentIp" -ForegroundColor Cyan

    az sql server firewall-rule create `
        --resource-group $ResourceGroup `
        --server $sqlServerName `
        --name "DeploymentClient-$(Get-Date -Format 'yyyyMMddHHmmss')" `
        --start-ip-address $currentIp `
        --end-ip-address $currentIp `
        --output none

    if ($LASTEXITCODE -eq 0) {
        Write-Host "  Firewall rule added." -ForegroundColor Cyan
    }
} catch {
    Write-Warning "Could not determine current IP or add firewall rule: $_"
}

if (-not $SkipDatabaseSetup) {
    # sqlcmd authentication method
    $authMethod = if ($IsCI) { "ActiveDirectoryAzCli" } else { "ActiveDirectoryDefault" }

    # ============================================================
    # Import database schema
    # ============================================================
    Write-Host "`n[8/12] Importing database schema..." -ForegroundColor Green

    $schemaFile = Join-Path $repoRoot "Database-Schema" "database_schema.sql"

    if (-not (Test-Path $schemaFile)) {
        Write-Warning "Schema file not found at: $schemaFile. Skipping schema import."
    } else {
        Write-Host "  Running: sqlcmd -S $sqlServerFqdn -d Northwind --authentication-method=$authMethod -i $schemaFile" -ForegroundColor Yellow
        sqlcmd -S $sqlServerFqdn -d "Northwind" "--authentication-method=$authMethod" -i $schemaFile

        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Schema import returned non-zero exit code. The schema may already exist."
        } else {
            Write-Host "  Schema imported successfully." -ForegroundColor Cyan
        }
    }

    # ============================================================
    # Create managed identity database user (SID-based)
    # ============================================================
    Write-Host "`n[9/12] Creating managed identity database user..." -ForegroundColor Green

    # Convert Client ID GUID to SID hex format (no Directory Reader required)
    $guidBytes = [System.Guid]::Parse($managedIdentityClientId).ToByteArray()
    $sidHex = "0x" + [System.BitConverter]::ToString($guidBytes).Replace("-", "")

    $createUserSql = @"
IF EXISTS (SELECT * FROM sys.database_principals WHERE name = '$managedIdentityName')
    DROP USER [$managedIdentityName];
CREATE USER [$managedIdentityName] WITH SID = $sidHex, TYPE = E;
ALTER ROLE db_datareader ADD MEMBER [$managedIdentityName];
ALTER ROLE db_datawriter ADD MEMBER [$managedIdentityName];
GRANT EXECUTE TO [$managedIdentityName];
"@

    sqlcmd -S $sqlServerFqdn -d "Northwind" "--authentication-method=$authMethod" -Q $createUserSql

    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Managed identity user creation returned non-zero exit code."
    } else {
        Write-Host "  Managed identity database user created." -ForegroundColor Cyan
    }

    # ============================================================
    # Deploy stored procedures
    # ============================================================
    Write-Host "`n[10/12] Deploying stored procedures..." -ForegroundColor Green

    $spFile = Join-Path $repoRoot "stored-procedures.sql"

    if (-not (Test-Path $spFile)) {
        Write-Warning "Stored procedures file not found at: $spFile. Skipping."
    } else {
        sqlcmd -S $sqlServerFqdn -d "Northwind" "--authentication-method=$authMethod" -i $spFile

        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Stored procedure deployment returned non-zero exit code."
        } else {
            Write-Host "  Stored procedures deployed." -ForegroundColor Cyan
        }
    }
} else {
    Write-Host "`n[8-10/12] Skipping database setup (SkipDatabaseSetup flag set)." -ForegroundColor Yellow
}

# ============================================================
# Configure App Service settings
# ============================================================
Write-Host "`n[11/12] Configuring App Service settings..." -ForegroundColor Green

$connectionString = "Server=tcp:$sqlServerFqdn,1433;Initial Catalog=Northwind;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity;User Id=$managedIdentityClientId;"

az webapp config appsettings set `
    --resource-group $ResourceGroup `
    --name $webAppName `
    --settings `
        "AZURE_CLIENT_ID=$managedIdentityClientId" `
        "ManagedIdentityClientId=$managedIdentityClientId" `
        "APPLICATIONINSIGHTS_CONNECTION_STRING=$appInsightsConnectionString" `
        "WEBSITES_ENABLE_APP_SERVICE_STORAGE=false" `
    --output none

az webapp config connection-string set `
    --resource-group $ResourceGroup `
    --name $webAppName `
    --settings "DefaultConnection=$connectionString" `
    --connection-string-type SQLAzure `
    --output none

if ($LASTEXITCODE -eq 0) {
    Write-Host "  App Service settings configured." -ForegroundColor Cyan
}

# Configure GenAI settings if deployed
if ($DeployGenAI -and -not [string]::IsNullOrEmpty($openAIEndpoint)) {
    Write-Host "  Configuring Azure OpenAI settings..." -ForegroundColor Yellow

    az webapp config appsettings set `
        --resource-group $ResourceGroup `
        --name $webAppName `
        --settings `
            "OpenAI__Endpoint=$openAIEndpoint" `
            "OpenAI__DeploymentName=$openAIModelName" `
            "AzureSearch__Endpoint=$searchEndpoint" `
        --output none

    Write-Host "  GenAI settings configured." -ForegroundColor Cyan
}

# ============================================================
# Save deployment context
# ============================================================
Write-Host "`n[12/12] Saving deployment context..." -ForegroundColor Green

$context = @{
    resourceGroup           = $ResourceGroup
    webAppName              = $webAppName
    sqlServerFqdn           = $sqlServerFqdn
    managedIdentityClientId = $managedIdentityClientId
    location                = $Location
}

$contextFile = Join-Path $repoRoot ".deployment-context.json"
$context | ConvertTo-Json | Set-Content -Path $contextFile
Write-Host "  Context saved to: $contextFile" -ForegroundColor Cyan

# ============================================================
# Summary
# ============================================================
Write-Host "`n============================================================" -ForegroundColor Green
Write-Host "  DEPLOYMENT COMPLETE" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green
Write-Host "  Resource Group:    $ResourceGroup" -ForegroundColor White
Write-Host "  Web App:           https://$webAppName.azurewebsites.net/Index" -ForegroundColor White
Write-Host "  SQL Server:        $sqlServerFqdn" -ForegroundColor White
Write-Host "  Managed Identity:  $managedIdentityClientId" -ForegroundColor White
if ($DeployGenAI -and -not [string]::IsNullOrEmpty($openAIEndpoint)) {
    Write-Host "  Azure OpenAI:      $openAIEndpoint" -ForegroundColor White
    Write-Host "  AI Search:         $searchEndpoint" -ForegroundColor White
}
Write-Host "============================================================" -ForegroundColor Green
Write-Host ""
Write-Host "Next step: Deploy the application with:" -ForegroundColor Yellow
Write-Host "  .\deploy-app\deploy.ps1" -ForegroundColor Yellow
Write-Host ""
