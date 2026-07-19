# AI Teammate トラブルシューティング

## よくある問題

### 1. Bot がメッセージに応答しない

**症状**: Teams チャットでコマンドを送信しても応答がない

**確認手順**:
1. ヘルスチェック: `curl https://<app-url>/healthz`
2. Application Insights でエラーログを確認
3. Container Apps のログを確認:
   ```bash
   az containerapp logs show --name <app-name> --resource-group <rg> --follow
   ```

**対処**:
- Bot App ID と Password が正しく設定されているか確認
- Container App が起動しているか確認
- Agents SDK の設定（MultiTenant/SingleTenant）を確認

### 2. トランスクリプトが取得できない

**症状**: 会議に参加しているが分析が行われない

**確認手順**:
1. Application Insights で `TranscriptError` イベントを検索
2. Graph API のアクセス許可を確認

**対処**:
- `OnlineMeetingTranscript.Read.All` 権限が付与されているか確認
- トランスクリプトプロバイダーの設定を確認（WorkIQ/GraphAPI）
- 会議でトランスクリプトが有効になっているか確認

### 3. AI 分析のレイテンシが高い

**症状**: 分析結果の表示が遅い（30秒以上）

**確認手順**:
1. Application Insights で `AILatencyMs` メトリクスを確認
2. Azure OpenAI のスロットリング（429）を確認

**対処**:
- Azure OpenAI のレートリミットを引き上げ
- フォールバックモデルの設定を確認
- 分析スケジューラーのデバウンス間隔を調整

### 4. ナレッジが保存されない

**症状**: 暗黙知が抽出されるが Cosmos DB に保存されない

**確認手順**:
1. Cosmos DB のエンドポイントとアクセス権を確認
2. Application Insights で Cosmos DB 関連エラーを検索

**対処**:
- Managed Identity に Cosmos DB のロールが付与されているか確認
- データベースとコンテナが存在するか確認
- パーティションキー（TenantId）が正しいか確認

### 5. サイドパネルが表示されない

**症状**: 会議でサイドパネルタブが表示されない

**対処**:
- Teams Developer Portal でアプリのマニフェストを確認
- `configurableTabs` の `meetingSurfaces` に `sidePanel` が含まれているか確認
- アプリが正しくインストールされているか確認

### 6. Adaptive Card のアクションが動作しない

**症状**: カードのボタンを押しても反応がない

**確認手順**:
1. Application Insights で `UserInteraction` イベントを検索
2. Bot のエンドポイントが到達可能か確認

**対処**:
- Bot のメッセージングエンドポイントが正しく設定されているか確認
- Invoke Activity のハンドリングが実装されているか確認

## 監視・アラート

### アラート一覧

| アラート | 条件 | 重大度 |
|---------|------|--------|
| High Error Rate | エラー率 > 5% | 警告 |
| High AI Latency | 分析レイテンシ > 30秒 | 警告 |
| Transcript Errors | 連続3回エラー | 重大 |
| OpenAI Throttling | 429エラー多発 | 警告 |
| Health Check Failure | ヘルスチェック連続失敗 | 重大 |

### ログ検索クエリ

```kql
// エラーの概要
exceptions
| summarize count() by type
| order by count_ desc

// AI分析のレイテンシ
customMetrics
| where name == "AnalysisLatencyMs"
| summarize avg(value), percentile(value, 95) by bin(timestamp, 1h)

// 会議セッション数
customEvents
| where name == "MeetingJoined"
| summarize count() by bin(timestamp, 1d)
```

## ロールバック

### Container Apps のロールバック

```bash
# 前のリビジョンに切り戻し
az containerapp revision list --name <app> --resource-group <rg> --query "[].name" -o tsv
az containerapp ingress traffic set --name <app> --resource-group <rg> --revision-weight "<prev-rev>=100"
```
