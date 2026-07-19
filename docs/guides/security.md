# AI Teammate セキュリティドキュメント

## 認証・認可

### Entra ID 統合

- **認証方式**: OAuth 2.0 / OpenID Connect (Entra ID)
- **Bot 認証**: Microsoft 365 Agents SDK による Multi-Tenant Bot 認証
- **API 認証**: Bearer Token (JWT) による認証
- **テナント分離**: 全データアクセスにおいて `TenantId` によるフィルタリング

### RBAC

| ロール | 権限 |
| -------- | ------ |
| Admin | 全設定変更、ユーザー管理、ナレッジ削除 |
| User | ナレッジ閲覧・編集 |
| Viewer | ナレッジ閲覧のみ |

## データ保護

### 保存時の暗号化

- Cosmos DB: Microsoft マネージドキーによる暗号化（既定で有効）
- Azure AI Search: サービス管理キーによる暗号化
- Key Vault: HSM バックアップキーによるシークレット保護

### 転送時の暗号化

- 全通信は TLS 1.2 以上
- Container Apps のイングレスは HTTPS のみ許可
- 内部通信も VNet 内で暗号化

### データ分離

- テナントごとにパーティションキー (`TenantId`) で論理分離
- API レベルでテナントIDの検証を強制
- クロステナントアクセスは不可

## ネットワークセキュリティ

### Container Apps

- イングレスは HTTPS のみ（443）
- IP 制限: 必要に応じて設定可能

### Azure サービス間通信

- Managed Identity による認証（キーレス）
- Key Vault でシークレット一元管理

## API セキュリティ

### レート制限

- 全APIエンドポイントにレート制限を適用
- AspNetCoreRateLimit による制御

### 入力検証

- モデルバインディングによる型安全な入力検証
- 検索クエリのサニタイズ
- CORS ポリシーによるオリジン制限

### OWASP Top 10 対策

| リスク | 対策 |
| -------- | ------ |
| A01 アクセス制御の不備 | テナントID検証、RBAC |
| A02 暗号化の失敗 | TLS 1.2+、保存時暗号化 |
| A03 インジェクション | パラメータ化クエリ、入力検証 |
| A04 安全でない設計 | テナント分離、最小権限 |
| A05 セキュリティ設定ミス | Bicep IaC、環境ごとのパラメータ |
| A06 脆弱なコンポーネント | Dependabot、定期更新 |
| A07 認証の失敗 | Entra ID、MFA |
| A08 データの整合性の不備 | CI/CDパイプライン検証 |
| A09 ログとモニタリングの不備 | Application Insights、監査ログ |
| A10 SSRF | 外部URLアクセスの制限 |

## Managed Identity

以下のサービスで Managed Identity を使用:

| サービス | ロール |
| --------- | -------- |
| Cosmos DB | Cosmos DB Built-in Data Contributor |
| Azure OpenAI | Cognitive Services OpenAI User |
| Azure AI Search | Search Index Data Contributor |
| Key Vault | Key Vault Secrets User |
| Application Insights | Monitoring Metrics Publisher |

## 監査

- 全管理操作を `audit-logs` コンテナに記録
- Application Insights で API アクセスログを記録
- Azure Monitor アラートで異常検知

## インシデント対応

1. **検知**: Application Insights アラート、ヘルスチェック
2. **対応**: Container Apps リビジョン切り戻し
3. **分析**: Application Insights/Log Analytics でログ分析
4. **改善**: 監査ログレビュー、設定修正
