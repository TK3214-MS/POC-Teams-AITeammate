@description('AI Search service name')
param name string

@description('Location')
param location string

resource aiSearch 'Microsoft.Search/searchServices@2024-06-01-preview' = {
  name: name
  location: location
  sku: {
    name: 'basic'
  }
  properties: {
    replicaCount: 1
    partitionCount: 1
    hostingMode: 'default'
    semanticSearch: 'standard'
  }
}

output endpoint string = 'https://${aiSearch.name}.search.windows.net'
output name string = aiSearch.name
