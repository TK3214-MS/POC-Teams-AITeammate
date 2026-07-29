targetScope = 'resourceGroup'

@description('Primary location for all resources')
param location string = resourceGroup().location

@description('Environment name (dev, staging, prod)')
@allowed(['dev', 'staging', 'prod'])
param environmentName string = 'dev'

@description('Unique suffix for resource names')
param resourceSuffix string = uniqueString(resourceGroup().id)

@description('Bot Microsoft App ID (from Entra ID app registration)')
param botAppId string

@secure()
@description('Bot Microsoft App Password')
param botAppPassword string

@description('Azure OpenAI model deployment name')
param openAiDeploymentName string = 'gpt-55'

@description('Fallback Azure OpenAI model deployment name')
param openAiFallbackDeploymentName string = 'gpt-54-mini'

// ---------- Variables ----------
var prefix = 'aiteammate'
var resourceName = '${prefix}-${environmentName}-${resourceSuffix}'
var containerRegistryName = replace('${prefix}${environmentName}${resourceSuffix}', '-', '')
var keyVaultName = '${prefix}kv${environmentName}${take(resourceSuffix, 8)}'
var speechAccountName = '${resourceName}-speech'
var storageAccountName = 'aitmst${environmentName}${take(resourceSuffix, 8)}'

// ---------- Modules ----------

module appInsights 'modules/app-insights.bicep' = {
  name: 'app-insights'
  params: {
    name: '${resourceName}-ai'
    location: location
  }
}

module keyVault 'modules/key-vault.bicep' = {
  name: 'key-vault'
  params: {
    name: keyVaultName
    location: location
    botAppPassword: botAppPassword
  }
}

module cosmosDb 'modules/cosmos-db.bicep' = {
  name: 'cosmos-db'
  params: {
    name: '${resourceName}-cosmos'
    location: location
  }
}

module openAi 'modules/openai.bicep' = {
  name: 'openai'
  params: {
    name: '${resourceName}-openai'
    location: location
    deploymentName: openAiDeploymentName
    fallbackDeploymentName: openAiFallbackDeploymentName
  }
}

module aiSearch 'modules/ai-search.bicep' = {
  name: 'ai-search'
  params: {
    name: '${resourceName}-search'
    location: location
  }
}

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    name: storageAccountName
    location: location
  }
}

resource speech 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: speechAccountName
  location: location
  kind: 'SpeechServices'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: speechAccountName
    publicNetworkAccess: 'Enabled'
  }
}

resource deployedKeyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource speechKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: deployedKeyVault
  name: 'SpeechServiceKey'
  properties: {
    value: speech.listKeys().key1
  }
}

module containerApp 'modules/container-app.bicep' = {
  name: 'container-app'
  params: {
    name: '${resourceName}-app'
    location: location
    containerRegistryName: containerRegistryName
    botAppId: botAppId
    keyVaultName: keyVault.outputs.name
    cosmosDbEndpoint: cosmosDb.outputs.endpoint
    openAiEndpoint: openAi.outputs.endpoint
    aiSearchEndpoint: aiSearch.outputs.endpoint
    blobStorageEndpoint: storage.outputs.blobEndpoint
    speechEndpoint: speech.properties.endpoint
    speechRegion: location
    speechKeySecretUri: speechKeySecret.properties.secretUriWithVersion
    appInsightsConnectionString: appInsights.outputs.connectionString
  }
}

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-05-15' existing = {
  name: '${resourceName}-cosmos'
}

resource cosmosDataContributor 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-05-15' = {
  parent: cosmosAccount
  name: guid(cosmosAccount.id, '${resourceName}-app-identity', 'cosmos-data-contributor')
  properties: {
    principalId: containerApp.outputs.identityPrincipalId
    roleDefinitionId: '${cosmosAccount.id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002'
    scope: cosmosAccount.id
  }
}

resource deployedStorageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

resource deployedAppIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: '${resourceName}-app-identity'
}

resource storageBlobDataContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(deployedStorageAccount.id, deployedAppIdentity.id, 'storage-blob-data-contributor')
  scope: deployedStorageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
    principalId: containerApp.outputs.identityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource deployedOpenAi 'Microsoft.CognitiveServices/accounts@2024-10-01' existing = {
  name: '${resourceName}-openai'
}

resource openAiUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(deployedOpenAi.id, deployedAppIdentity.id, 'cognitive-services-openai-user')
  scope: deployedOpenAi
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd')
    principalId: containerApp.outputs.identityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource deployedAiSearch 'Microsoft.Search/searchServices@2024-06-01-preview' existing = {
  name: '${resourceName}-search'
}

resource searchIndexDataContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(deployedAiSearch.id, deployedAppIdentity.id, 'search-index-data-contributor')
  scope: deployedAiSearch
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '8ebe5a00-799e-43f5-93ac-243d3dce84a7')
    principalId: containerApp.outputs.identityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// ---------- Outputs ----------
output containerAppFqdn string = containerApp.outputs.fqdn
output cosmosDbEndpoint string = cosmosDb.outputs.endpoint
output openAiEndpoint string = openAi.outputs.endpoint
output aiSearchEndpoint string = aiSearch.outputs.endpoint
output blobStorageEndpoint string = storage.outputs.blobEndpoint
output speechEndpoint string = speech.properties.endpoint
output keyVaultUri string = keyVault.outputs.uri
output appInsightsConnectionString string = appInsights.outputs.connectionString
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = containerApp.outputs.containerRegistryEndpoint

// ---------- Phase 8: Monitoring ----------

module workbook 'modules/workbook.bicep' = {
  name: 'workbook'
  params: {
    name: resourceName
    location: location
    appInsightsId: appInsights.outputs.resourceId
  }
}

module alerts 'modules/alerts.bicep' = {
  name: 'alerts'
  params: {
    namePrefix: resourceName
    location: location
    appInsightsId: appInsights.outputs.resourceId
  }
}
