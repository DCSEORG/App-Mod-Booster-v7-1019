# CI/CD Setup Guide

This document describes the one-time manual setup required to enable GitHub Actions CI/CD for this repository.

## Overview

The GitHub Actions workflow uses **OIDC (OpenID Connect) federation** to authenticate with Azure — no secrets or passwords are stored in GitHub.

## Prerequisites

- Azure CLI installed locally
- Owner or Contributor + User Access Administrator access on the Azure subscription
- PowerShell 7+ recommended

## Step 1: Create a Service Principal with OIDC Federation

```powershell
# Set variables
$appName = "sp-expensemgmt-github"
$subscriptionId = (az account show --query id -o tsv)
$tenantId = (az account show --query tenantId -o tsv)
$githubOrg = "YOUR_GITHUB_ORG_OR_USERNAME"  # e.g., "mycompany"
$githubRepo = "YOUR_REPO_NAME"               # e.g., "expense-management"

# Create the App Registration (Service Principal)
$spAppId = (az ad app create --display-name $appName --query appId -o tsv)
$spObjectId = (az ad sp create --id $spAppId --query id -o tsv)

Write-Host "App (Client) ID: $spAppId"
Write-Host "SP Object ID: $spObjectId"
Write-Host "Tenant ID: $tenantId"
Write-Host "Subscription ID: $subscriptionId"
```

## Step 2: Assign Required Azure Roles

The Service Principal needs **two roles** at the subscription level:

| Role | Purpose |
|------|---------|
| **Contributor** | Create and manage Azure resources |
| **User Access Administrator** | Create role assignments in Bicep (required for GenAI managed identity access) |

```powershell
# Assign Contributor role
az role assignment create `
    --assignee $spObjectId `
    --role "Contributor" `
    --scope "/subscriptions/$subscriptionId"

# Assign User Access Administrator role
az role assignment create `
    --assignee $spObjectId `
    --role "User Access Administrator" `
    --scope "/subscriptions/$subscriptionId"
```

> **Why User Access Administrator?** When Bicep creates role assignments (e.g., giving the Managed Identity access to Azure OpenAI), the deploying principal must have `Microsoft.Authorization/roleAssignments/write` permission. Without this, deployments that include `Microsoft.Authorization/roleAssignments` resources will fail.

## Step 3: Create Federated Credentials

Federated credentials allow GitHub Actions to authenticate as the Service Principal using OIDC — no secrets required.

```powershell
# For the "production" environment
$federatedCredentialJson = @{
    name = "github-actions-production"
    issuer = "https://token.actions.githubusercontent.com"
    subject = "repo:$githubOrg/${githubRepo}:environment:production"
    description = "GitHub Actions production environment"
    audiences = @("api://AzureADTokenExchange")
} | ConvertTo-Json

az ad app federated-credential create `
    --id $spAppId `
    --parameters $federatedCredentialJson
```

## Step 4: Configure GitHub Repository Variables

In your GitHub repository, go to **Settings → Secrets and variables → Actions → Variables** and create:

| Variable Name | Value | Description |
|---------------|-------|-------------|
| `AZURE_CLIENT_ID` | `<App (Client) ID from Step 1>` | Service Principal Application ID |
| `AZURE_TENANT_ID` | `<Tenant ID from Step 1>` | Azure AD Tenant ID |
| `AZURE_SUBSCRIPTION_ID` | `<Subscription ID from Step 1>` | Azure Subscription ID |

> **Variables, not Secrets**: These values are not sensitive enough to require Secrets. Using Variables makes them easier to see and manage.

## Step 5: Create the "production" GitHub Environment

1. Go to your repository on GitHub
2. Navigate to **Settings → Environments**
3. Click **New environment**
4. Name it `production`
5. Optionally add required reviewers for approval gates

## Step 6: Run the Workflow

1. Go to **Actions** tab in your GitHub repository
2. Select **Deploy to Azure** workflow
3. Click **Run workflow**
4. Enter the required parameters:
   - **resourceGroup**: A unique name (e.g., `rg-expensemgmt-20240101`)
   - **location**: Azure region (default: `uksouth`)
   - **deployGenAI**: `true` or `false` (default: `false`)

## Troubleshooting

### "The client does not have permission to perform action 'Microsoft.Authorization/roleAssignments/write'"

The Service Principal is missing the **User Access Administrator** role. Run Step 2 again.

### "AADSTS70011: The provided request must include a 'scope' input parameter"

The federated credential subject doesn't match. Ensure the environment name in the federated credential (`environment:production`) matches the GitHub environment name exactly.

### "Identity not found" with sqlcmd

In CI/CD mode, the script uses `ActiveDirectoryAzCli` authentication for sqlcmd. Ensure the Azure CLI step (`azure/login@v2`) runs before any sqlcmd calls.

### Deployment fails with "ARM caching" errors on Log Analytics

Always use a **unique resource group name** with a date suffix. Reusing resource groups with failed deployments can cause ARM caching issues.
