# Application Deployment

This folder contains the PowerShell script to deploy the Expense Management application to Azure App Service.

## Prerequisites

- Azure CLI installed and logged in (`az login`)
- .NET 8 SDK installed
- Infrastructure already deployed (run `deploy-infra/deploy.ps1` first)

## Automated Deployment (Recommended)

After running the infrastructure deployment script, simply run:

```powershell
.\deploy-app\deploy.ps1
```

The script automatically reads the `.deployment-context.json` file created by the infrastructure script, so you don't need to specify any parameters.

## Manual Deployment

If you need to specify parameters explicitly:

```powershell
.\deploy-app\deploy.ps1 `
    -ResourceGroup "rg-expensemgmt-20240101" `
    -WebAppName "app-expensemgmt-abc123"
```

## Parameters

| Parameter | Required | Description |
|-----------|----------|-------------|
| `-ResourceGroup` | Optional* | Azure resource group name |
| `-WebAppName` | Optional* | Azure Web App name |
| `-SkipBuild` | No | Skip the dotnet publish step (use cached build) |
| `-ConfigureSettings` | No | Also configure App Service settings from context file |

*Optional if `.deployment-context.json` exists at the repository root.

## What the Script Does

1. Reads deployment context from `.deployment-context.json` (if available)
2. Validates Azure CLI is installed and logged in
3. Builds the .NET application using `dotnet publish`
4. Creates a deployment zip package with DLL files at the root level
5. Deploys to Azure App Service using `az webapp deploy`
6. Cleans up the temporary zip file
7. Displays the application URLs

## Application URLs

After deployment, the application is available at:

- **Main App**: `https://<webappname>.azurewebsites.net/Index`
- **Swagger UI**: `https://<webappname>.azurewebsites.net/swagger`
- **Chat Interface**: `https://<webappname>.azurewebsites.net/Chat`

> **Note**: The main entry point is `/Index`, not the root URL.

## Two-Phase Deployment Flow

```
Phase 1: Infrastructure
  .\deploy-infra\deploy.ps1 -ResourceGroup "rg-myapp-20240101" -Location "uksouth"
  └── Creates all Azure resources
  └── Imports database schema
  └── Deploys stored procedures
  └── Configures App Service settings
  └── Saves .deployment-context.json

Phase 2: Application
  .\deploy-app\deploy.ps1
  └── Reads .deployment-context.json
  └── Builds .NET application
  └── Deploys to App Service
```

## Troubleshooting

### "Project file not found"
Ensure you're running the script from the repository root, and `src/ExpenseManagement/ExpenseManagement.csproj` exists.

### "Not logged in to Azure"
Run `az login` before running the deployment script.

### Application returns 500 errors
Check Application Insights for logs. Common causes:
- Managed identity not granted database permissions (run `deploy-infra/deploy.ps1` again)
- `AZURE_CLIENT_ID` environment variable not set in App Service settings
- Connection string format incorrect
