# Microsoft Agent 365 登録 & 監視・可視化ガイド

## 概要

本ドキュメントでは、AI Teammate を **Microsoft Agent 365** プラットフォームにエージェントとして登録し、Agent 365 が提供する可観測性（Observability）機能を活用して運用監視・可視化を行う手順を説明します。

Microsoft Agent 365 は、組織内のAIエージェントを **観察（Observe）**・**統治（Govern）**・**保護（Secure）** するためのコントロールプレーンです（2026年5月GA）。

> **参考**: [Microsoft Agent 365 overview](https://learn.microsoft.com/en-us/microsoft-agent-365/overview)

---

## 前提条件

- Microsoft 365 E5 以上（推奨）または Microsoft Agent 365 ライセンスがテナントで有効
- Microsoft 365 管理センターへのアクセス権（AI Administrator または Global Administrator ロール）
- AI Teammate が M365 Agents SDK でビルド・デプロイ済み（[deployment-guide.md](deployment-guide.md) 参照）
- `appPackage/` に有効な Teams アプリマニフェスト（manifest.json、アイコン等）が含まれていること

---

## Part 1: Agent 365 へのエージェント登録

### 1.1 エージェントの分類

Agent 365 の Agent Registry では、エージェントは以下の種類で管理されます:

| エージェント種類 | 説明 |
| --------------- | ----- |
| **Microsoft agents** | Microsoft が構築・管理するエージェント |
| **External partner-built agents** | サードパーティ開発者が構築したエージェント |
| **Published by your org** | 組織が承認・公開したカスタムエージェント（LOB） |
| **Shared by creator** | 個人が作成・共有したエージェント |

AI Teammate は **Microsoft 365 Agents Toolkit** を開発とアプリパッケージ管理に使用するエージェントとして、Agent Registry に **"Agent Toolkit"** タイプで認識されます。さらに Agent 365 SDK で拡張することで **"Agent instance"**（Entra バックの独自 ID、拡張通知、オブザーバビリティ等）にアップグレードできます。

### 1.2 アプリパッケージ（ZIP）の準備

Agent Registry にカスタムエージェントをアップロードするには、ZIP パッケージが必要です。

```bash
cd TeamsAITeammate/appPackage

# ZIP パッケージ作成
zip -r ../ai-teammate-agent.zip manifest.json color.png outline.png
```

ZIP に含めるファイル:

- `manifest.json` — エージェントのメタデータ、能力宣言、Bot ID
- `color.png` — 192×192 カラーアイコン
- `outline.png` — 32×32 アウトラインアイコン

### 1.3 Agent Registry への登録（カスタムエージェントアップロード）

1. [Microsoft 365 管理センター](https://admin.microsoft.com/) にサインイン
2. 左ナビゲーションで **Agents** → **All Agents** → **Registry** を選択
3. **Add agent** をクリック
4. **Choose file** で作成した ZIP パッケージ（`ai-teammate-agent.zip`）を選択 → バリデーション通過後 **Next**
5. エージェント名・アイコン・ホストプロダクト（Teams）を確認
6. **Publish** セクションで、エージェントをインストールできるユーザー/グループを選択:
   - **Just me** — テスト用
   - **Entire organization** — 全社展開
   - **Specific users/groups** — 段階的展開
7. **Deploy**（任意）で、エージェントを事前インストールするユーザー/グループを指定 → **Next**
8. セキュリティポリシーを適用:
   - 既存のセキュリティテンプレート、カスタムポリシー、またはデフォルトポリシーを選択 → **Next**
9. エージェントの権限（Permissions）をレビュー → **Next**
10. 最終確認 → **Finish deployment**

### 1.4 Agent Instance としてオンボード（AI Teammate としての独自 ID 付与）

Agent 365 の Frontier プログラムを通じて、AI Teammate を **独自のEntra ID を持つ Agent Instance** としてオンボードできます。

> **参考**: [Discover, create, and onboard agents with their own identity](https://learn.microsoft.com/en-us/microsoft-agent-365/onboard)

#### Agent Instance の前提条件

- テナント管理者が M365 管理センターでリクエストを承認済み
- **Microsoft Agent 365 Frontier** サブスクリプション（25シート）が有効

#### 手順

1. Teams アプリストアまたは Microsoft 365 Copilot Store で **Agents for your team** カテゴリから AI Teammate テンプレートを発見
2. テンプレート詳細ページで **Request instance** をクリック
3. 管理者が M365 管理センターでリクエストを承認し、テンプレートをアクティベート、ライセンス・ポリシーを割り当て
4. 承認後、テンプレートページで **Create instance** をクリック
5. エージェントインスタンスの設定:
   - **Agent icon**: エージェントのアイコン
   - **Name**: エージェント名（スペース・特殊文字不可）
   - **Agent description**: 説明
   - **Alias**: エージェントに割り当てるエイリアス
   - **Domain**: 許可されたドメインから選択
6. **Save** で完了

#### オンボード後の動作

- エージェントが Teams チャット経由でクリエイターに連絡
- 組織図にクリエイターの管理下として表示
- Teams チャット、チャネル、会議で利用可能に

### 1.5 エージェントの可用性・インストール設定

登録後、M365 管理センターでユーザーへの公開範囲を制御します。

1. **Agents** → **All Agents** → Registry でエージェントを選択
2. **Users** タブを開く
3. 以下を設定:

| 設定 | オプション | 説明 |
| ----- | --------- | ----- |
| **Installed for** | Just me / Entire organization / Specific users/groups | 事前インストール対象 |
| **Available to** | No users / All users / Specific users/groups | インストール可能な対象 |

1. **Update** をクリック

### 1.6 エージェントのピン留め

管理者は重要なエージェントをユーザーの Copilot インターフェースにピン留めできます。

1. **All Agents** ページで右端の省略記号（...）→ **Manage pinned agents**
2. **Pin agent** → リストから AI Teammate を選択 → **Next**
3. ピン留めの範囲を指定（全ユーザー or 特定グループ）
4. **Save**

> ピン留め後、エンドユーザーに反映されるまで最大6時間かかります。

### 1.7 Microsoft Graph API による登録自動化（プレビュー）

Agent Registry への操作は Microsoft Graph API でもプログラムによる管理が可能です。

```http
# テナント内の全エージェント一覧取得
GET https://graph.microsoft.com/beta/copilot/packages
Authorization: Bearer {token}

# 特定エージェントの詳細取得
GET https://graph.microsoft.com/beta/copilot/packages/{package-id}
Authorization: Bearer {token}
```

必要なロール: **AI Administrator**

> **参考**: [Agent and app Package Management API overview (preview)](https://learn.microsoft.com/en-us/microsoft-365-copilot/extensibility/api/admin-settings/package/overview)

### 1.8 登録確認

M365 管理センターで以下を確認:

- [ ] **Agents** → **All Agents** → **Registry** に AI Teammate が表示される
- [ ] エージェントのステータスが **Available** になっている
- [ ] **Platform** カラムに **Microsoft 365 Agents Toolkit** と表示される
- [ ] **Channel** に **Teams** が含まれる
- [ ] Teams クライアントで AI Teammate を検索・インストールできる

---

## Part 2: Agent 365 による監視・可視化

### 2.1 Agent Overview ダッシュボード

M365 管理センターの Agent Overview は、テナント全体のエージェント活用状況を可視化します。

**アクセス方法:**

1. [Microsoft 365 管理センター](https://admin.microsoft.com/) にサインイン
2. **Agents** → **Overview** を選択

#### ヒーローメトリクス

| メトリクス | 説明 |
| ---------- | ----- |
| **Agent registry** | 組織で利用可能な全エージェント数 |
| **Active users** | 過去30日間にエージェントと対話したユニークユーザー数 |
| **Agent run-time** | 過去30日間のエージェント稼働時間（合計） |
| **Registry sync** | スキャンされた外部プラットフォーム接続数 |

#### ガバナンスアクション

Overview ダッシュボードには管理者が対処すべきアクション（Top actions for you）が表示されます:

- **Pending Requests** — 承認待ちのエージェントリクエスト
- **Agents at risk** — セキュリティリスクが検出されたエージェント
- **Agents without owners** — オーナー未割り当てのエージェント
- **Agents with exceptions** — エラーが発生しているエージェント

### 2.2 Agent Activity（個別エージェント監視）

各エージェントの詳細パネルにある **Activity** タブで、個別のパフォーマンスを確認できます。

**アクセス方法:**

1. **Agents** → **All Agents** → **Registry** でエージェントを選択
2. フライアウトパネルの **Activity** タブを選択

#### Activity メトリクス

| メトリクス | 説明 |
| ---------- | ----- |
| **Active users** | 選択期間内にエージェントと対話したユニークユーザー数 |
| **Sessions** | 会話セッション数（30分非アクティブで新セッション） |
| **Exceptions** | エラーが発生したセッション数 |
| **Agent run-time** | エージェントの合計稼働時間 |

#### タイムシリーズビュー

- **Active users over time** — 日別アクティブユーザー推移
- **Sessions over time** — 日別セッション数推移

#### ユーザーテーブル

ユーザーごとの利用状況（UPN、合計セッション数、最終アクティビティ日時）が一覧表示され、CSV エクスポートも可能。

### 2.3 Agent Map（ビジュアルマップ）

Agent Map はテナント内のエージェント全体像を視覚的に把握するためのビューです。

**アクセス方法:**

1. **Agents** → **All Agents** → **Map** タブを選択

#### クラスタ表示

エージェントはビルドプラットフォーム別にクラスタとしてグループ化されます:

- Microsoft 365 Copilot Agent Builder
- Copilot Studio
- **Microsoft 365 Agents Toolkit** ← AI Teammate はここに分類
- SharePoint
- Azure AI Foundry
- Amazon Bedrock / Google Vertex AI（Registry Sync 利用時）

#### フィルタリング

| フィルタグループ | フィルタ項目 |
| --------------- | ------------ |
| **Status** | Available, Blocked, Draft, Not activated |
| **Publisher type** | Your org, Your users, Microsoft, Third party |
| **Platform** | Copilot Studio, Agent Builder, Microsoft 365 Agents Toolkit, Foundry 等 |
| **Channel** | Copilot, Outlook, Teams, Microsoft 365 apps, SharePoint |
| **Usage** | Active users (Top 100), Sessions, Exception rate, Assisted hours, Security alerts |

#### Single Agent Map（プレビュー）

特定のエージェント1つについて、ユーザーとの関連・ツール呼び出し・例外パターンを可視化します。

1. **Map** タブで AI Teammate を選択
2. サマリーメトリクス（ユーザー、セッション、例外）を確認
3. **All connections** を選択して Single Agent Map を展開
   - ユーザーノード: ユーザー詳細を表示
   - ツールノード: ツール呼び出し数、例外数、最終アクティビティを確認
   - 接続線の太さ: インタラクション量を示す
   - 例外率 > 1% のツールへの線はハイライト表示

### 2.4 Agent Risks（セキュリティリスク可視化）

Agent Registry の **Risks** カラムで、各エージェントのセキュリティリスクを一元的に確認できます。

#### リスク種別

| リスク種別 | 重大度 | 検出元 | トリガー |
| ---------- | ------ | ------- | -------- |
| Shadow agent | Critical | Entra, M365 管理センター | Registry 未登録、オーナー無し、Agent ID 無し |
| No owner assigned | Critical | Entra, M365 管理センター | オーナー/スポンサー未設定 |
| Excessive permissions | Critical | Entra, Defender | 最小権限違反 |
| Security misconfiguration | High | Defender | 攻撃パスの検出 |
| Prompt injection | High | Defender, Entra SSE | AI Prompt Shield によるインジェクション検出 |
| Sensitive data access | High | Purview | DLP ポリシー例外なしでラベル付きデータへのアクセス |
| Conditional access violation | High | Entra | 条件付きアクセスポリシー違反 |
| Operational exceptions | Medium | M365 管理センター | エージェント会話/ツール実行のエラー |

#### リスクの調査

1. **Registry** リストの **Risks** カラムでリスク数をクリック
2. フライアウトの **Security** タブが表示
3. 各検出プラットフォーム別のリスク集計を確認
4. **Review** リンクで該当セキュリティポータル（Defender / Purview / Entra）に遷移

### 2.5 Agent Security タブ

個別エージェントの **Security** タブでは、Microsoft Purview との連携による監視を行えます。

| 機能 | 説明 | 連携先 |
| ----- | ----- | ------- |
| **Monitor agent activity** | Activity Explorer でエージェントのインタラクションを監視 | Microsoft Purview |
| **Protect sensitive data** | AI observability で機密データの漏洩・過共有を防止 | Microsoft Purview |
| **Evaluate compliance gaps** | AI ベースラインアセスメントでコンプライアンスギャップを評価 | Purview Compliance Manager |

### 2.6 Agent Analytics（テナント全体の分析）

Agent Overview の **Agent analytics** セクションでテナント全体の分析を確認:

| 分析項目 | 内容 |
| --------- | ----- |
| **Agents by creators** | パブリッシャー種別ごとのエージェント内訳（組織内 / サードパーティ / Microsoft） |
| **Top platforms used to build agents** | エージェント構築に使われたプラットフォーム別ランキング |
| **Active users over time** | 過去30日間の日別アクティブユーザートレンド |
| **Trending agents by active users** | アクティブユーザー数でのエージェントランキング |

### 2.7 エージェントのブロック・削除

問題のあるエージェントは管理センターまたは Teams のエージェント設定カードから制御できます。

- **Block**: エージェントを一時停止（アクティブなタスクを停止、いつでもブロック解除可能）
- **Delete**: エージェントを完全削除（元に戻せない、関連リソースは削除後30日間保持）

### 2.8 Registry Sync（外部プラットフォームのエージェント同期）

Agent 365 は外部プラットフォームのエージェントも一元管理できます（プレビュー）。

**対応プラットフォーム:**

- Amazon Bedrock
- Google Vertex AI
- Salesforce Agentforce
- Databricks Genie

**設定手順:**

1. **Agents** → **All Agents** → **Registry sync** の **Manage** を選択
2. **+ Connect a platform** をクリック
3. 接続名・説明・プラットフォーム・リージョンを入力
4. 認証情報を入力・検証
5. 保存後、**Sync agents** で同期実行

---

## Part 3: AI Teammate 固有の監視設定

Agent 365 のプラットフォームレベル監視に加え、AI Teammate 独自の Application Insights テレメトリも活用します。

### 3.1 Application Insights カスタムテレメトリ

AI Teammate は `IAITeammateTelemetry` 実装により以下を送信:

| イベント/メトリクス | 説明 |
| ------------------- | ----- |
| `MeetingJoined` / `MeetingLeft` | 会議参加・退出 |
| `AnalysisExecution` | AI分析実行（トピック数、質問生成数） |
| `KnowledgeIngestion` | ナレッジ蓄積（カテゴリ、ストアプロバイダー） |
| `AnalysisLatencyMs` | AI分析レイテンシ |
| `AIPromptTokens` / `AICompletionTokens` | トークン消費量 |

### 3.2 Azure Monitor Workbook

Bicep デプロイ（`infra/modules/workbook.bicep`）で自動作成される AI Teammate 専用ダッシュボード:

- 会議セッション数（日次推移）
- AI分析レイテンシ（平均・P95）
- ナレッジ蓄積数（カテゴリ別）
- AIモデルトークン消費量
- エラー率とエラー種別

### 3.3 アラートルール

`infra/modules/alerts.bicep` で定義:

| アラート | 条件 | 重大度 |
| -------- | ----- | ------- |
| high-error-rate | エラー率 > 5% | Sev 2 |
| high-ai-latency | AI分析 > 30秒 | Sev 2 |
| transcript-errors | トランスクリプト取得エラー連続3回 | Sev 1 |
| openai-throttling | 429エラー 5分間に5回以上 | Sev 2 |
| health-check-failure | `/healthz` 失敗 | Sev 1 |

---

## トラブルシューティング

| 症状 | 確認事項 |
| ----- | --------- |
| Agent Registry にエージェントが表示されない | ZIP パッケージの manifest.json が有効か確認、アップロード時のバリデーションエラーを確認 |
| Activity タブにデータが表示されない | Agent 365 ライセンス（E7 またはAgent 365）が付与されているか確認。アクティベーション後30日間はデータ蓄積期間 |
| Agent Map で Usage フィルタが使えない | テナントのエージェント数が4,000以下であることを確認 |
| Risks カラムが表示されない | Agent 365 または E7 ライセンスが必要 |
| エージェントのオンボードリクエストが承認されない | M365 管理センターで AI Administrator がリクエストを確認・承認する必要あり |
| Single Agent Map にデータがない | エージェントが Agent 365 にオブザーバビリティデータを送信しているか確認 |

---

## 関連ドキュメント

### プロジェクト内

- [デプロイ手順書](deployment-guide.md)
- [アーキテクチャ](architecture.md)
- [管理者ガイド](admin-guide.md)
- [セキュリティ](security.md)

### Microsoft 公式

- [Microsoft Agent 365 overview](https://learn.microsoft.com/en-us/microsoft-agent-365/overview)
- [Discover, create, and onboard agents](https://learn.microsoft.com/en-us/microsoft-agent-365/onboard)
- [Agent Registry in M365 admin center](https://learn.microsoft.com/en-us/microsoft-365/admin/manage/agent-registry)
- [Agent overview in M365 admin center](https://learn.microsoft.com/en-us/microsoft-365/admin/manage/agent-365-overview)
- [Agent Map](https://learn.microsoft.com/en-us/microsoft-365/admin/manage/agent-map)
- [Agent details](https://learn.microsoft.com/en-us/microsoft-365/admin/manage/agent-details)
- [Registry sync](https://learn.microsoft.com/en-us/microsoft-agent-365/admin/agent-registry)
