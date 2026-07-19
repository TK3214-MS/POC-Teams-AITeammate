@description('Key Vault name')
param name string

@description('Location')
param location string

@secure()
@description('Bot app password to store')
param botAppPassword string

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: name
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enabledForDeployment: false
    enabledForDiskEncryption: false
    enabledForTemplateDeployment: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enableRbacAuthorization: false
    accessPolicies: []
  }
}

resource botPasswordSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'BotAppPassword'
  properties: {
    value: botAppPassword
  }
}

output name string = keyVault.name
output uri string = keyVault.properties.vaultUri
