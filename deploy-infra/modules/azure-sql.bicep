@description('Azure region for SQL resources')
param location string = resourceGroup().location

@description('Base name for resources')
param baseName string = 'expensemgmt'

@description('Object ID of the Entra ID admin (user or service principal)')
param adminObjectId string

@description('User Principal Name of the Entra ID admin')
param adminUserPrincipalName string

@description('Principal type for the SQL administrator')
@allowed(['User', 'Application'])
param adminPrincipalType string = 'User'

@description('Resource ID of the managed identity for DatabaseManager role')
param managedIdentityId string

@description('Principal ID of the managed identity')
param managedIdentityPrincipalId string

@description('Name of the managed identity (used as DB username)')
param managedIdentityName string

var sqlServerName = 'sql-${baseName}-${uniqueString(resourceGroup().id)}'
var databaseName = 'Northwind'

resource sqlServer 'Microsoft.Sql/servers@2023-05-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administrators: {
      administratorType: 'ActiveDirectory'
      principalType: adminPrincipalType
      login: adminUserPrincipalName
      sid: adminObjectId
      tenantId: subscription().tenantId
      azureADOnlyAuthentication: true
    }
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-05-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
    capacity: 5
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648
  }
}

// Allow Azure services to access SQL Server
resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = {
  parent: sqlServer
  name: 'AllowAllAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Assign ##MS_DatabaseManager## server role to the managed identity
resource managedIdentityServerRole 'Microsoft.Sql/servers/administrators@2023-05-01-preview' = {
  parent: sqlServer
  name: 'ActiveDirectory'
  properties: {
    administratorType: 'ActiveDirectory'
    principalType: adminPrincipalType
    login: adminUserPrincipalName
    sid: adminObjectId
    tenantId: subscription().tenantId
  }
  dependsOn: [sqlServer]
}

output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlServerName string = sqlServer.name
output databaseName string = sqlDatabase.name
