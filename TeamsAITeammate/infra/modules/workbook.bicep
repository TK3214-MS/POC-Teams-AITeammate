@description('Workbook name')
param name string

@description('Location')
param location string

@description('Application Insights resource ID')
param appInsightsId string

resource workbook 'Microsoft.Insights/workbooks@2023-06-01' = {
  name: guid(name, resourceGroup().id)
  location: location
  kind: 'shared'
  properties: {
    displayName: '${name} - AI Teammate Dashboard'
    category: 'workbook'
    sourceId: appInsightsId
    serializedData: string(loadJsonContent('../workbook-template.json'))
  }
}

output workbookId string = workbook.id
