@description('Azure region for all resources')
param location string = 'uksouth'

@description('Base name for all resources')
param baseName string = 'expensemgmt'

@description('Object ID of the Entra ID administrator')
param adminObjectId string

@description('User Principal Name of the Entra ID administrator')
param adminUserPrincipalName string

@description('Principal type for SQL administrator')
@allowed(['User', 'Application'])
param adminPrincipalType string = 'User'

@description('Whether to deploy GenAI resources (Azure OpenAI + AI Search)')
param deployGenAI bool = false

// ============================================================
// Managed Identity (must be first - other modules depend on it)
// ============================================================
module managedIdentity './modules/managed-identity.bicep' = {
  name: 'managedIdentity'
  params: {
    location: location
    baseName: baseName
  }
}

// ============================================================
// App Service
// ============================================================
module appService './modules/app-service.bicep' = {
  name: 'appService'
  params: {
    location: location
    baseName: baseName
    managedIdentityId: managedIdentity.outputs.managedIdentityId
    managedIdentityClientId: managedIdentity.outputs.managedIdentityClientId
    managedIdentityPrincipalId: managedIdentity.outputs.managedIdentityPrincipalId
  }
}

// ============================================================
// Azure SQL
// ============================================================
module azureSql './modules/azure-sql.bicep' = {
  name: 'azureSql'
  params: {
    location: location
    baseName: baseName
    adminObjectId: adminObjectId
    adminUserPrincipalName: adminUserPrincipalName
    adminPrincipalType: adminPrincipalType
    managedIdentityId: managedIdentity.outputs.managedIdentityId
    managedIdentityPrincipalId: managedIdentity.outputs.managedIdentityPrincipalId
    managedIdentityName: managedIdentity.outputs.managedIdentityName
  }
}

// ============================================================
// Monitoring
// ============================================================
module monitoring './modules/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    location: location
    baseName: baseName
    webAppName: appService.outputs.webAppName
    sqlServerName: azureSql.outputs.sqlServerName
    sqlDatabaseName: azureSql.outputs.databaseName
  }
}

// ============================================================
// GenAI (optional)
// ============================================================
module genAI './modules/genai.bicep' = if (deployGenAI) {
  name: 'genAI'
  params: {
    location: location
    openAILocation: 'swedencentral'
    managedIdentityPrincipalId: managedIdentity.outputs.managedIdentityPrincipalId
  }
}

// ============================================================
// App Service settings (updated after all modules deploy)
// ============================================================
resource existingWebApp 'Microsoft.Web/sites@2023-01-01' existing = {
  name: appService.outputs.webAppName
}

resource appSettings 'Microsoft.Web/sites/config@2023-01-01' = {
  parent: existingWebApp
  name: 'appsettings'
  properties: {
    AZURE_CLIENT_ID: managedIdentity.outputs.managedIdentityClientId
    ManagedIdentityClientId: managedIdentity.outputs.managedIdentityClientId
    APPLICATIONINSIGHTS_CONNECTION_STRING: monitoring.outputs.appInsightsConnectionString
    ApplicationInsightsAgent_EXTENSION_VERSION: '~3'
    WEBSITES_ENABLE_APP_SERVICE_STORAGE: 'false'
  }
}

// ============================================================
// Outputs
// ============================================================
output webAppName string = appService.outputs.webAppName
output sqlServerFqdn string = azureSql.outputs.sqlServerFqdn
output sqlServerName string = azureSql.outputs.sqlServerName
output databaseName string = azureSql.outputs.databaseName
output managedIdentityClientId string = managedIdentity.outputs.managedIdentityClientId
output managedIdentityPrincipalId string = managedIdentity.outputs.managedIdentityPrincipalId
output managedIdentityName string = managedIdentity.outputs.managedIdentityName
output appInsightsConnectionString string = monitoring.outputs.appInsightsConnectionString
output openAIEndpoint string = genAI.?outputs.openAIEndpoint ?? ''
output openAIModelName string = genAI.?outputs.openAIModelName ?? ''
output searchEndpoint string = genAI.?outputs.searchEndpoint ?? ''
