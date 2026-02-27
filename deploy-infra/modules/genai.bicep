@description('Azure region for GenAI resources (main location)')
param location string = 'uksouth'

@description('Location for Azure OpenAI (Sweden Central has better quota)')
param openAILocation string = 'swedencentral'

@description('Principal ID of the managed identity for role assignments')
param managedIdentityPrincipalId string

var openAIName = 'oai-${toLower(uniqueString(resourceGroup().id, location))}'
var searchName = 'srch-${toLower(uniqueString(resourceGroup().id, location))}'
var gptModelName = 'gpt-4o'

// Role definition IDs
var cognitiveServicesOpenAIUserRoleId = '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
var searchIndexDataReaderRoleId = '1407120a-92aa-4202-b7e9-c0e197c71c8f'

resource openAI 'Microsoft.CognitiveServices/accounts@2024-04-01-preview' = {
  name: openAIName
  location: openAILocation
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: openAIName
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
    }
  }
}

resource gptDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-04-01-preview' = {
  parent: openAI
  name: gptModelName
  sku: {
    name: 'Standard'
    capacity: 8
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4o'
      version: '2024-08-06'
    }
  }
}

resource aiSearch 'Microsoft.Search/searchServices@2024-03-01-preview' = {
  name: searchName
  location: location
  sku: {
    name: 'standard'
  }
  properties: {
    replicaCount: 1
    partitionCount: 1
    hostingMode: 'default'
    publicNetworkAccess: 'enabled'
  }
}

// Assign "Cognitive Services OpenAI User" role to managed identity
resource openAIRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(openAI.id, managedIdentityPrincipalId, cognitiveServicesOpenAIUserRoleId)
  scope: openAI
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', cognitiveServicesOpenAIUserRoleId)
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Assign "Search Index Data Reader" role to managed identity
resource searchRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(aiSearch.id, managedIdentityPrincipalId, searchIndexDataReaderRoleId)
  scope: aiSearch
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', searchIndexDataReaderRoleId)
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output openAIEndpoint string = openAI.properties.endpoint
output openAIModelName string = gptModelName
output openAIName string = openAI.name
output searchEndpoint string = 'https://${aiSearch.name}.search.windows.net'
output searchName string = aiSearch.name
