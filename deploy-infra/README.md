# Infrastructure Deployment

This folder contains Bicep templates to deploy all Azure infrastructure for the Expense Management application.

## Architecture

The following Azure resources are deployed:

- **Managed Identity** – User-assigned identity used by the App Service to authenticate to Azure SQL and other services
- **App Service Plan (S1)** – Hosts the web application
- **Web App (.NET 8)** – The Expense Management application
- **Azure SQL Server + Database** – Stores expense data (Entra ID only authentication)
- **Log Analytics Workspace** – Centralised log collection
- **Application Insights** – Application telemetry and monitoring
- *(Optional)* **Azure OpenAI (GPT-4o)** – AI chat functionality
- *(Optional)* **Azure AI Search** – Search capabilities for the chat interface

## Prerequisites

- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli) installed and logged in
- [go-sqlcmd](https://github.com/microsoft/go-sqlcmd) installed (`winget install sqlcmd` on Windows)
- PowerShell 7+ recommended

## Automated Deployment (Recommended)

The deployment script handles everything automatically:

```powershell
# Set up variables
$resourceGroup = "rg-expensemgmt-$(Get-Date -Format 'yyyyMMdd')"
$location = "uksouth"

# Deploy infrastructure (without GenAI)
.\deploy-infra\deploy.ps1 -ResourceGroup $resourceGroup -Location $location

# Deploy infrastructure (with GenAI / Azure OpenAI)
.\deploy-infra\deploy.ps1 -ResourceGroup $resourceGroup -Location $location -DeployGenAI
```

### What the Script Does

1. Validates Azure CLI is installed and logged in
2. Detects CI/CD environment automatically
3. Creates the resource group if it doesn't exist
4. Deploys all Bicep templates
5. Waits for SQL Server to be ready
6. Adds your current IP to the SQL firewall
7. Imports the database schema
8. Creates the managed identity database user
9. Deploys stored procedures
10. Configures all App Service settings
11. Saves `.deployment-context.json` for use by the app deployment script

## Manual Deployment

### Step 1: Set Variables

```powershell
$resourceGroup = "rg-expensemgmt-20240101"
$location = "uksouth"
$adminObjectId = (az ad signed-in-user show --query id -o tsv)
$adminUpn = (az ad signed-in-user show --query userPrincipalName -o tsv)
```

### Step 2: Create Resource Group

```powershell
az group create --name $resourceGroup --location $location
```

### Step 3: Deploy Bicep Templates

Without GenAI:
```powershell
az deployment group create `
  --resource-group $resourceGroup `
  --template-file ./deploy-infra/main.bicep `
  --parameters ./deploy-infra/main.bicepparam `
  --parameters adminObjectId=$adminObjectId `
               adminUserPrincipalName=$adminUpn `
               deployGenAI=false
```

With GenAI (Azure OpenAI + AI Search):
```powershell
az deployment group create `
  --resource-group $resourceGroup `
  --template-file ./deploy-infra/main.bicep `
  --parameters ./deploy-infra/main.bicepparam `
  --parameters adminObjectId=$adminObjectId `
               adminUserPrincipalName=$adminUpn `
               deployGenAI=true
```

### Step 4: Import Database Schema

```powershell
$serverFqdn = (az deployment group show `
  --resource-group $resourceGroup `
  --name main `
  --query "properties.outputs.sqlServerFqdn.value" -o tsv)

sqlcmd -S $serverFqdn -d "Northwind" "--authentication-method=ActiveDirectoryDefault" -i ./Database-Schema/database_schema.sql
```

### Step 5: Deploy Stored Procedures

```powershell
sqlcmd -S $serverFqdn -d "Northwind" "--authentication-method=ActiveDirectoryDefault" -i ./stored-procedures.sql
```

## Parameters

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `location` | No | `uksouth` | Azure region |
| `baseName` | No | `expensemgmt` | Base name for resources |
| `adminObjectId` | Yes | – | Object ID of the Entra ID admin |
| `adminUserPrincipalName` | Yes | – | UPN of the Entra ID admin |
| `adminPrincipalType` | No | `User` | `User` or `Application` (for CI/CD) |
| `deployGenAI` | No | `false` | Whether to deploy Azure OpenAI and AI Search |

## Notes

- Always use a **unique resource group name** (include date/time suffix) to avoid ARM caching issues
- All resource names are lowercase and deterministic based on the resource group ID
- SQL Server uses **Entra ID only authentication** (no SQL passwords)
- The managed identity uses SID-based database user creation (no Directory Reader permissions required)
