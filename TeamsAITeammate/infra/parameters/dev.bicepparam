using '../main.bicep'

param environmentName = 'dev'
param botAppId = ''
param botAppPassword = ''
param openAiDeploymentName = 'gpt-55'
param openAiFallbackDeploymentName = 'gpt-41'
param imageTag = 'latest'
