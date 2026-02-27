@description('Azure region for the managed identity')
param location string = resourceGroup().location

@description('Base name for resources')
param baseName string = 'expensemgmt'

var managedIdentityName = 'mid-appmodassist-${uniqueString(resourceGroup().id, baseName)}'

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: managedIdentityName
  location: location
}

output managedIdentityId string = managedIdentity.id
output managedIdentityClientId string = managedIdentity.properties.clientId
output managedIdentityPrincipalId string = managedIdentity.properties.principalId
output managedIdentityName string = managedIdentity.name
