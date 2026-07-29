@description('Container App name')
param name string

@description('Location')
param location string

@description('Container Registry name')
param containerRegistryName string

@description('Bot App ID')
param botAppId string

@description('Key Vault name')
param keyVaultName string

@description('Cosmos DB endpoint')
param cosmosDbEndpoint string

@description('OpenAI endpoint')
param openAiEndpoint string

@description('AI Search endpoint')
param aiSearchEndpoint string

@description('Azure Blob Storage service endpoint')
param blobStorageEndpoint string

@description('Azure AI Speech endpoint')
param speechEndpoint string

@description('Azure AI Speech region')
param speechRegion string

@description('Key Vault URI for the Azure AI Speech key')
param speechKeySecretUri string

@description('Application Insights connection string')
param appInsightsConnectionString string

// Container Registry
resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: containerRegistryName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
  }
}

// Log Analytics Workspace
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${name}-logs'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// Container Apps Environment
resource containerAppEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${name}-env'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

// Managed Identity
resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${name}-identity'
  location: location
}

// Role assignment: AcrPull for managed identity
resource acrPullRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, managedIdentity.id, 'acrpull')
  scope: acr
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
    principalId: managedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Container App
resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: name
  location: location
  dependsOn: [
    kvAccessPolicy
  ]
  tags: {
    'azd-service-name': 'agent'
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerAppEnv.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'http'
        allowInsecure: false
      }
      registries: [
        {
          server: acr.properties.loginServer
          identity: managedIdentity.id
        }
      ]
      secrets: [
        {
          name: 'bot-app-password'
          keyVaultUrl: 'https://${keyVaultName}${environment().suffixes.keyvaultDns}/secrets/BotAppPassword'
          identity: managedIdentity.id
        }
        {
          name: 'speech-service-key'
          keyVaultUrl: speechKeySecretUri
          identity: managedIdentity.id
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'agent'
          image: 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            { name: 'Agents__Type', value: 'SingleTenant' }
            { name: 'Agents__MicrosoftAppId', value: botAppId }
            { name: 'Agents__MicrosoftAppPassword', secretRef: 'bot-app-password' }
            { name: 'Agents__MicrosoftAppTenantId', value: subscription().tenantId }
            { name: 'Connections__ServiceConnection__Settings__AuthType', value: 'ClientSecret' }
            { name: 'Connections__ServiceConnection__Settings__AuthorityEndpoint', value: '${environment().authentication.loginEndpoint}${subscription().tenantId}' }
            { name: 'Connections__ServiceConnection__Settings__ClientId', value: botAppId }
            { name: 'Connections__ServiceConnection__Settings__ClientSecret', secretRef: 'bot-app-password' }
            { name: 'Connections__ServiceConnection__Settings__Scopes__0', value: 'https://api.botframework.com/.default' }
            { name: 'ConnectionsMap__0__ServiceUrl', value: '*' }
            { name: 'ConnectionsMap__0__Connection', value: 'ServiceConnection' }
            { name: 'CosmosDb__Endpoint', value: cosmosDbEndpoint }
            { name: 'AzureOpenAI__Endpoint', value: openAiEndpoint }
            { name: 'AzureAISearch__Endpoint', value: aiSearchEndpoint }
            { name: 'BlobStorage__Endpoint', value: blobStorageEndpoint }
            { name: 'Speech__Endpoint', value: speechEndpoint }
            { name: 'Speech__Key', secretRef: 'speech-service-key' }
            { name: 'Speech__Region', value: speechRegion }
            { name: 'TeamsTabAuth__Audience', value: 'api://${botAppId}' }
            { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsightsConnectionString }
            { name: 'AZURE_CLIENT_ID', value: managedIdentity.properties.clientId }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 10
        rules: [
          {
            name: 'http-scaling'
            http: {
              metadata: {
                concurrentRequests: '50'
              }
            }
          }
        ]
      }
    }
  }
}

// Key Vault access for managed identity
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource kvAccessPolicy 'Microsoft.KeyVault/vaults/accessPolicies@2023-07-01' = {
  name: 'add'
  parent: keyVault
  properties: {
    accessPolicies: [
      {
        objectId: managedIdentity.properties.principalId
        tenantId: subscription().tenantId
        permissions: {
          secrets: ['get', 'list']
        }
      }
    ]
  }
}

output fqdn string = containerApp.properties.configuration.ingress.fqdn
output containerRegistryEndpoint string = acr.properties.loginServer
output identityPrincipalId string = managedIdentity.properties.principalId
output identityClientId string = managedIdentity.properties.clientId
