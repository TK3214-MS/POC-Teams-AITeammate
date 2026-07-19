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
param openAiFallbackDeploymentName string = 'gpt-41'

@description('Container image tag')
param imageTag string = 'latest'

// ---------- Variables ----------
var prefix = 'aiteammate'
var resourceName = '${prefix}-${environmentName}-${resourceSuffix}'
var containerRegistryName = replace('${prefix}${environmentName}${resourceSuffix}', '-', '')

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
    name: '${prefix}kv${environmentName}${take(resourceSuffix, 8)}'
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

module containerApp 'modules/container-app.bicep' = {
  name: 'container-app'
  params: {
    name: '${resourceName}-app'
    location: location
    containerRegistryName: containerRegistryName
    imageTag: imageTag
    botAppId: botAppId
    keyVaultName: keyVault.outputs.name
    cosmosDbEndpoint: cosmosDb.outputs.endpoint
    openAiEndpoint: openAi.outputs.endpoint
    aiSearchEndpoint: aiSearch.outputs.endpoint
    appInsightsConnectionString: appInsights.outputs.connectionString
  }
}

// ---------- Outputs ----------
output containerAppFqdn string = containerApp.outputs.fqdn
output cosmosDbEndpoint string = cosmosDb.outputs.endpoint
output openAiEndpoint string = openAi.outputs.endpoint
output aiSearchEndpoint string = aiSearch.outputs.endpoint
output keyVaultUri string = keyVault.outputs.uri
output appInsightsConnectionString string = appInsights.outputs.connectionString

// ---------- Phase 8: Monitoring ----------

module workbook 'modules/workbook.bicep' = {
  name: 'workbook'
  params: {
    name: resourceName
    location: location
    appInsightsId: appInsights.outputs.name
  }
}

module alerts 'modules/alerts.bicep' = {
  name: 'alerts'
  params: {
    namePrefix: resourceName
    location: location
    appInsightsId: appInsights.outputs.name
  }
}
