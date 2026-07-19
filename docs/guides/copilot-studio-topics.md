# Copilot Studio トピック設計ドキュメント

## 概要

AI Teammate のナレッジベースを M365 Copilot / Copilot Studio から検索・活用するためのトピック設計です。カスタムコネクタ経由で REST API を呼び出し、会議から蓄積されたナレッジを Copilot UI 上で提供します。

---

## カスタムコネクタ設定

### エンドポイント
- Base URL: `https://<app-domain>/api/copilot`
- 認証: Microsoft Entra ID (OAuth 2.0)

### API アクション

| アクション名 | メソッド | パス | 説明 |
|---|---|---|---|
| SearchKnowledge | POST | `/search` | ナレッジのハイブリッド検索 |
| GetKnowledge | GET | `/knowledge/{id}` | ナレッジ詳細取得 |
| GetStats | GET | `/stats` | ナレッジ統計情報 |

---

## トピック 1: ナレッジ検索

### トリガーフレーズ
- 「〜について知りたい」
- 「〜の背景は？」
- 「〜に関するナレッジを教えて」
- 「過去の会議で〜について話したことある？」
- 「〜の知見を検索して」
- "What do we know about ~?"
- "Search knowledge about ~"

### フロー

```
1. トリガー検出
   ↓
2. エンティティ抽出
   - searchQuery: ユーザーの検索意図（フレーズからキーワードを抽出）
   - category: (オプション) カテゴリフィルター
   ↓
3. カスタムコネクタ呼び出し
   POST /api/copilot/search
   {
     "query": "{searchQuery}",
     "maxResults": 5,
     "category": "{category}"  // null if not specified
   }
   ↓
4. 結果分岐
   - 結果あり → 応答カード表示
   - 結果なし → 「該当するナレッジが見つかりませんでした」
   ↓
5. 応答生成（Adaptive Card）
```

### 応答フォーマット

**結果あり:**
```
📚 「{query}」に関するナレッジが {count} 件見つかりました：

1. **{title}** (関連度: {score}%)
   {summary}
   📅 {meetingDate} | 🗣️ {sourceSpeaker}
   カテゴリ: {category}

2. **{title}** ...

詳細を見たい項目の番号を教えてください。
```

**結果なし:**
```
「{query}」に関するナレッジは見つかりませんでした。
別のキーワードで検索するか、関連するトピックをお試しください。
```

---

## トピック 2: 会議サマリー取得

### トリガーフレーズ
- 「先週の会議のまとめ」
- 「〜プロジェクトの会議サマリー」
- 「最近の会議で決まったこと」
- 「直近の会議の決定事項」
- "Summary of last week's meetings"
- "What was decided in the ~ meeting?"

### フロー

```
1. トリガー検出
   ↓
2. エンティティ抽出
   - timeRange: 「先週」→ fromDate/toDate計算
   - projectName: プロジェクト名（任意）
   ↓
3. カスタムコネクタ呼び出し
   POST /api/copilot/search
   {
     "query": "{projectName} 決定事項 サマリー",
     "maxResults": 10,
     "category": "DecisionBackground",
     "fromDate": "{fromDate}",
     "toDate": "{toDate}"
   }
   ↓
4. 結果をサマリーカードとして整形
   ↓
5. 応答表示
```

### 応答フォーマット

```
📋 会議サマリー ({fromDate} 〜 {toDate})

**決定事項:**
• {decision1} — {meetingSubject} ({meetingDate})
• {decision2} — {meetingSubject} ({meetingDate})

**重要な知見:**
• {insight1}
• {insight2}

**アクションアイテム:**
• {action1} (担当: {assignee})

合計 {count} 件のナレッジが該当しました。
```

---

## トピック 3: ナレッジ閲覧

### トリガーフレーズ
- 「最近蓄積されたナレッジ」
- 「〜カテゴリのナレッジ」
- 「エキスパート知識の一覧」
- 「今月のナレッジを見せて」
- "Show recent knowledge"
- "List knowledge in ~ category"

### フロー

```
1. トリガー検出
   ↓
2. エンティティ抽出
   - category: カテゴリ名 → TacitKnowledgeCategory にマッピング
     - 「意思決定」→ DecisionBackground
     - 「プロセス」→ UndocumentedProcess
     - 「専門知識」→ ExpertKnowledge
     - 「議論履歴」→ DiscussionHistory
     - 「技術的知見」→ TechnicalInsight
     - 「教訓」→ LessonsLearned
   - timeRange: (オプション) 「今月」「今週」
   ↓
3. カスタムコネクタ呼び出し
   POST /api/copilot/search
   {
     "query": "*",
     "maxResults": 10,
     "category": "{mappedCategory}",
     "fromDate": "{fromDate}"
   }
   ↓
4. 統計情報取得（オプション）
   GET /api/copilot/stats
   ↓
5. リスト形式で応答
```

### 応答フォーマット

```
📊 ナレッジベース ({category} カテゴリ)

全体統計: {totalEntries} 件 (確認済み: {confirmedCount} 件)

最近のナレッジ:
1. 📌 **{title}**
   {summary}
   📅 {meetingDate} | タグ: {tags}

2. 📌 **{title}**
   ...

「1の詳細を見せて」で詳細を確認できます。
```

---

## カテゴリマッピング表

| 日本語表現 | 英語表現 | API値 |
|---|---|---|
| 意思決定の背景 | Decision Background | DecisionBackground |
| 非文書化プロセス | Undocumented Process | UndocumentedProcess |
| 専門知識 | Expert Knowledge | ExpertKnowledge |
| 議論履歴 | Discussion History | DiscussionHistory |
| 組織的コンテキスト | Organizational Context | OrganizationalContext |
| 技術的知見 | Technical Insight | TechnicalInsight |
| 教訓・学び | Lessons Learned | LessonsLearned |

---

## エラーハンドリング

| エラー | 応答 |
|---|---|
| 認証エラー (401) | 「認証に問題があります。管理者にお問い合わせください。」 |
| アクセス拒否 (403) | 「このナレッジへのアクセス権がありません。」 |
| 検索エラー (500) | 「検索サービスに一時的な問題が発生しています。しばらく後にお試しください。」 |
| タイムアウト | 「応答に時間がかかっています。検索条件を絞ってお試しください。」 |

---

## セキュリティ考慮事項

1. **テナント分離**: すべての API 呼び出しで認証トークンからテナント ID を検証
2. **データアクセス制御**: ユーザーの所属テナントのナレッジのみ返却
3. **入力バリデーション**: 検索クエリの長さ制限・サニタイズ
4. **レート制限**: 過度な検索リクエストの制御
