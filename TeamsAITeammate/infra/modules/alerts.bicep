@description('Alert rule name prefix')
param namePrefix string

@description('Application Insights resource ID')
param appInsightsId string

@description('Action group resource ID for notifications')
param actionGroupId string = ''

@description('Location')
param location string

// Alert: エラー率 > 5%
resource errorRateAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${namePrefix}-high-error-rate'
  location: 'global'
  properties: {
    severity: 2
    enabled: true
    scopes: [appInsightsId]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'ErrorRate'
          metricName: 'requests/failed'
          metricNamespace: 'microsoft.insights/components'
          operator: 'GreaterThan'
          threshold: 5
          timeAggregation: 'Average'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
    actions: actionGroupId != '' ? [{ actionGroupId: actionGroupId }] : []
    description: 'エラー率が5%を超えました'
  }
}

// Alert: AI分析レイテンシ > 30秒
resource latencyAlert 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' = {
  name: '${namePrefix}-high-ai-latency'
  location: location
  properties: {
    severity: 2
    enabled: true
    scopes: [appInsightsId]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      allOf: [
        {
          query: 'customMetrics | where name == "AnalysisLatencyMs" | summarize avg(value) by bin(timestamp, 5m) | where avg_value > 30000'
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 0
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: actionGroupId != '' ? [actionGroupId] : []
    }
    description: 'AI分析のレイテンシが30秒を超えました'
  }
}

// Alert: トランスクリプト取得エラー連続3回
resource transcriptErrorAlert 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' = {
  name: '${namePrefix}-transcript-errors'
  location: location
  properties: {
    severity: 1
    enabled: true
    scopes: [appInsightsId]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      allOf: [
        {
          query: 'exceptions | where customDimensions["ErrorType"] == "TranscriptError" | summarize count() by bin(timestamp, 5m) | where count_ >= 3'
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 0
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: actionGroupId != '' ? [actionGroupId] : []
    }
    description: 'トランスクリプト取得エラーが連続3回以上発生しました'
  }
}

// Alert: Azure OpenAI 429エラー多発
resource throttlingAlert 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' = {
  name: '${namePrefix}-openai-throttling'
  location: location
  properties: {
    severity: 2
    enabled: true
    scopes: [appInsightsId]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      allOf: [
        {
          query: 'dependencies | where resultCode == "429" | summarize count() by bin(timestamp, 5m) | where count_ >= 5'
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 0
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: actionGroupId != '' ? [actionGroupId] : []
    }
    description: 'Azure OpenAI APIのスロットリング(429)が多発しています'
  }
}

// Alert: ヘルスチェック失敗
resource healthCheckAlert 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' = {
  name: '${namePrefix}-health-check-failure'
  location: location
  properties: {
    severity: 1
    enabled: true
    scopes: [appInsightsId]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    criteria: {
      allOf: [
        {
          query: 'requests | where name contains "healthz" and success == false | summarize count() by bin(timestamp, 5m) | where count_ >= 1'
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 0
          failingPeriods: {
            numberOfEvaluationPeriods: 2
            minFailingPeriodsToAlert: 2
          }
        }
      ]
    }
    actions: {
      actionGroups: actionGroupId != '' ? [actionGroupId] : []
    }
    description: 'ヘルスチェックが連続して失敗しています'
  }
}
