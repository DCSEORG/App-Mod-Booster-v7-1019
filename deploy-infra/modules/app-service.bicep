@description('Azure region for App Service resources')
param location string = resourceGroup().location

@description('Base name for resources')
param baseName string = 'expensemgmt'

@description('Resource ID of the user-assigned managed identity')
param managedIdentityId string

@description('Client ID of the user-assigned managed identity')
param managedIdentityClientId string

@description('Principal ID of the user-assigned managed identity')
param managedIdentityPrincipalId string

var appServicePlanName = 'asp-${baseName}-${uniqueString(resourceGroup().id)}'
var webAppName = 'app-${baseName}-${uniqueString(resourceGroup().id)}'

resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: 'S1'
    tier: 'Standard'
  }
  kind: 'app'
  properties: {
    reserved: false
  }
}

resource webApp 'Microsoft.Web/sites@2023-01-01' = {
  name: webAppName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentityId}': {}
    }
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      windowsFxVersion: 'DOTNET|8.0'
      alwaysOn: true
      http20Enabled: true
      minTlsVersion: '1.2'
      appSettings: [
        {
          name: 'AZURE_CLIENT_ID'
          value: managedIdentityClientId
        }
        {
          name: 'ManagedIdentityClientId'
          value: managedIdentityClientId
        }
        {
          name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE'
          value: 'false'
        }
      ]
    }
  }
}

output webAppName string = webApp.name
output webAppId string = webApp.id
output webAppHostName string = webApp.properties.defaultHostName
output managedIdentityPrincipalId string = managedIdentityPrincipalId
