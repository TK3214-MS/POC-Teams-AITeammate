using System.Text.Json;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Infrastructure.Services;

/// <summary>
/// Generates Adaptive Card JSON payloads (schema v1.6) for Teams meeting interactions.
/// </summary>
public static class AdaptiveCardTemplates
{
    private const string Schema = "http://adaptivecards.io/schemas/adaptive-card.json";
    private const string Version = "1.6";

    public static string BuildQuestionCard(GeneratedQuestion question, string language)
    {
        var isJapanese = language.StartsWith("ja", StringComparison.OrdinalIgnoreCase);
        var answerLabel = isJapanese ? "回答を入力" : "Enter your answer";
        var answerBtn = isJapanese ? "回答する" : "Answer";
        var skipBtn = isJapanese ? "スキップ" : "Skip";
        var deferBtn = isJapanese ? "後で回答" : "Answer Later";
        var reasonLabel = isJapanese ? "質問の理由" : "Reason";
        var categoryLabel = GetQuestionTypeLabelLocalized(question.Type, isJapanese);

        var card = new
        {
            type = "AdaptiveCard",
            version = Version,
            body = new object[]
            {
                new { type = "ColumnSet", columns = new object[] {
                    new { type = "Column", width = "auto", items = new object[] {
                        new { type = "TextBlock", text = "❓", size = "Large" }
                    }},
                    new { type = "Column", width = "stretch", items = new object[] {
                        new { type = "TextBlock", text = question.Question, wrap = true, weight = "Bolder", size = "Medium" },
                        new { type = "TextBlock", text = categoryLabel, color = "Accent", size = "Small", spacing = "None" }
                    }}
                }},
                new { type = "ActionSet", actions = new object[] {
                    new { type = "Action.ToggleVisibility", title = reasonLabel, targetElements = new[] { "reasonBlock" } }
                }},
                new { type = "TextBlock", id = "reasonBlock", text = question.Rationale, wrap = true, isVisible = false, color = "Dark", isSubtle = true },
                new { type = "Input.Text", id = "answerText", placeholder = answerLabel, isMultiline = true }
            },
            actions = new object[]
            {
                new { type = "Action.Execute", title = answerBtn, verb = "questionAnswer",
                    data = new { questionId = question.Id, action = "answer" },
                    style = "positive" },
                new { type = "Action.Execute", title = skipBtn, verb = "questionSkip",
                    data = new { questionId = question.Id, action = "skip" } },
                new { type = "Action.Execute", title = deferBtn, verb = "questionDefer",
                    data = new { questionId = question.Id, action = "defer" } }
            },
            schema = Schema
        };

        return JsonSerializer.Serialize(card);
    }

    public static string BuildAgendaSuggestionCard(IReadOnlyList<SuggestedAgendaItem> items, string language)
    {
        var isJapanese = language.StartsWith("ja", StringComparison.OrdinalIgnoreCase);
        var title = isJapanese ? "📋 追加議題の提案" : "📋 Suggested Agenda Items";
        var discussBtn = isJapanese ? "この議題を議論する" : "Discuss This Topic";
        var skipAllBtn = isJapanese ? "すべてスキップ" : "Skip All";

        var bodyItems = new List<object>
        {
            new { type = "TextBlock", text = title, weight = "Bolder", size = "Medium" }
        };

        foreach (var item in items)
        {
            var priorityIcon = item.Priority switch
            {
                QuestionPriority.Critical => "🔴",
                QuestionPriority.High => "🟠",
                QuestionPriority.Medium => "🟡",
                _ => "🟢"
            };

            bodyItems.Add(new
            {
                type = "ColumnSet",
                columns = new object[]
                {
                    new { type = "Column", width = "auto", items = new object[] {
                        new { type = "TextBlock", text = priorityIcon }
                    }},
                    new { type = "Column", width = "stretch", items = new object[] {
                        new { type = "TextBlock", text = item.Title, wrap = true, weight = "Bolder" },
                        new { type = "TextBlock", text = item.Rationale, wrap = true, isSubtle = true, size = "Small" }
                    }},
                    new { type = "Column", width = "auto", items = new object[] {
                        new { type = "ActionSet", actions = new object[] {
                            new { type = "Action.Execute", title = discussBtn, verb = "agendaAccept",
                                data = new { agendaId = item.Id } }
                        }}
                    }}
                }
            });
        }

        var card = new
        {
            type = "AdaptiveCard",
            version = Version,
            body = bodyItems,
            actions = new object[]
            {
                new { type = "Action.Execute", title = skipAllBtn, verb = "agendaSkipAll" }
            },
            schema = Schema
        };

        return JsonSerializer.Serialize(card);
    }

    public static string BuildTacitKnowledgeConfirmCard(TacitKnowledgeCandidate candidate, string language)
    {
        var isJapanese = language.StartsWith("ja", StringComparison.OrdinalIgnoreCase);
        var title = isJapanese ? "📚 暗黙知の確認" : "📚 Tacit Knowledge Confirmation";
        var categoryLabel = isJapanese ? "カテゴリ" : "Category";
        var sourceLabel = isJapanese ? "ソース発言" : "Source";
        var confirmBtn = isJapanese ? "正しい" : "Confirm";
        var editBtn = isJapanese ? "修正が必要" : "Needs Edit";
        var rejectBtn = isJapanese ? "削除" : "Reject";
        var editPlaceholder = isJapanese ? "修正内容を入力" : "Enter correction";

        var card = new
        {
            type = "AdaptiveCard",
            version = Version,
            body = new object[]
            {
                new { type = "TextBlock", text = title, weight = "Bolder", size = "Medium" },
                new { type = "TextBlock", text = candidate.Content, wrap = true },
                new { type = "FactSet", facts = new object[] {
                    new { title = categoryLabel, value = candidate.Category.ToString() },
                    new { title = sourceLabel, value = candidate.SourceSpeaker }
                }},
                new { type = "TextBlock", text = $"\"{candidate.Context}\"", wrap = true, isSubtle = true, size = "Small" },
                new { type = "Input.Text", id = "correctionText", placeholder = editPlaceholder, isMultiline = true, isVisible = false }
            },
            actions = new object[]
            {
                new { type = "Action.Execute", title = confirmBtn, verb = "knowledgeConfirm",
                    data = new { candidateId = candidate.Id, action = "confirm" }, style = "positive" },
                new { type = "Action.Execute", title = editBtn, verb = "knowledgeEdit",
                    data = new { candidateId = candidate.Id, action = "edit" } },
                new { type = "Action.Execute", title = rejectBtn, verb = "knowledgeReject",
                    data = new { candidateId = candidate.Id, action = "reject" }, style = "destructive" }
            },
            schema = Schema
        };

        return JsonSerializer.Serialize(card);
    }

    public static string BuildConversationSummaryCard(ConversationAnalysis analysis, string language)
    {
        var isJapanese = language.StartsWith("ja", StringComparison.OrdinalIgnoreCase);
        var title = isJapanese ? "📊 会話サマリー" : "📊 Conversation Summary";
        var topicsHeader = isJapanese ? "トピック" : "Topics";
        var decisionsHeader = isJapanese ? "意思決定事項" : "Decisions";
        var actionsHeader = isJapanese ? "アクションアイテム" : "Action Items";
        var knowledgeLabel = isJapanese ? "蓄積ナレッジ" : "Knowledge Entries";
        var questionsLabel = isJapanese ? "未解決の質問" : "Open Questions";
        var detailBtn = isJapanese ? "詳細をサイドパネルで見る" : "View Details in Side Panel";

        var bodyItems = new List<object>
        {
            new { type = "TextBlock", text = title, weight = "Bolder", size = "Large" },
            new { type = "FactSet", facts = new object[] {
                new { title = knowledgeLabel, value = analysis.TacitKnowledgeCandidates.Count.ToString() },
                new { title = questionsLabel, value = analysis.Questions.Count.ToString() }
            }}
        };

        // Topics
        if (analysis.Topics.Count > 0)
        {
            bodyItems.Add(new { type = "TextBlock", text = topicsHeader, weight = "Bolder", spacing = "Medium" });
            foreach (var topic in analysis.Topics)
            {
                var statusIcon = topic.Status switch
                {
                    TopicStatus.Active => "🟢",
                    TopicStatus.Concluded => "✅",
                    TopicStatus.Tabled => "⏸️",
                    _ => "⚪"
                };
                bodyItems.Add(new { type = "TextBlock", text = $"{statusIcon} **{topic.Title}**: {topic.Summary}", wrap = true, size = "Small" });
            }
        }

        // Decisions
        if (analysis.Decisions.Count > 0)
        {
            bodyItems.Add(new { type = "TextBlock", text = decisionsHeader, weight = "Bolder", spacing = "Medium" });
            foreach (var d in analysis.Decisions)
            {
                bodyItems.Add(new { type = "TextBlock", text = $"• {d.Summary}", wrap = true, size = "Small" });
            }
        }

        // Action Items
        if (analysis.ActionItems.Count > 0)
        {
            bodyItems.Add(new { type = "TextBlock", text = actionsHeader, weight = "Bolder", spacing = "Medium" });
            foreach (var a in analysis.ActionItems)
            {
                var assignee = string.IsNullOrEmpty(a.Assignee) ? "TBD" : a.Assignee;
                bodyItems.Add(new { type = "TextBlock", text = $"☐ {a.Description} ({assignee})", wrap = true, size = "Small" });
            }
        }

        var card = new
        {
            type = "AdaptiveCard",
            version = Version,
            body = bodyItems,
            actions = new object[]
            {
                new { type = "Action.Execute", title = detailBtn, verb = "openSidePanel" }
            },
            schema = Schema
        };

        return JsonSerializer.Serialize(card);
    }

    public static string BuildSettingsCard(InterventionSettings settings, string language)
    {
        var isJapanese = language.StartsWith("ja", StringComparison.OrdinalIgnoreCase);
        var title = isJapanese ? "⚙️ エージェント設定" : "⚙️ Agent Settings";
        var frequencyLabel = isJapanese ? "介入頻度" : "Intervention Frequency";
        var proactiveLabel = isJapanese ? "プロアクティブ介入" : "Proactive Intervention";
        var maxLabel = isJapanese ? "最大介入回数" : "Max Interventions";
        var saveBtn = isJapanese ? "保存" : "Save";
        var cancelBtn = isJapanese ? "キャンセル" : "Cancel";
        var enabledLabel = isJapanese ? "有効" : "Enabled";
        var disabledLabel = isJapanese ? "無効" : "Disabled";
        var lowLabel = isJapanese ? "低（60秒）" : "Low (60s)";
        var medLabel = isJapanese ? "中（30秒）" : "Medium (30s)";
        var highLabel = isJapanese ? "高（15秒）" : "High (15s)";

        var currentFrequency = settings.SilenceThreshold.TotalSeconds <= 15 ? "high" :
                               settings.SilenceThreshold.TotalSeconds <= 30 ? "medium" : "low";

        var card = new
        {
            type = "AdaptiveCard",
            version = Version,
            body = new object[]
            {
                new { type = "TextBlock", text = title, weight = "Bolder", size = "Large" },
                new { type = "TextBlock", text = frequencyLabel, weight = "Bolder" },
                new { type = "Input.ChoiceSet", id = "frequency", value = currentFrequency, choices = new object[] {
                    new { title = lowLabel, value = "low" },
                    new { title = medLabel, value = "medium" },
                    new { title = highLabel, value = "high" }
                }},
                new { type = "TextBlock", text = proactiveLabel, weight = "Bolder" },
                new { type = "Input.Toggle", id = "proactive", title = settings.EnableProactiveIntervention ? enabledLabel : disabledLabel,
                    value = settings.EnableProactiveIntervention.ToString().ToLowerInvariant() },
                new { type = "TextBlock", text = maxLabel, weight = "Bolder" },
                new { type = "Input.Number", id = "maxInterventions", value = settings.MaxInterventionsPerMeeting, min = 1, max = 50 }
            },
            actions = new object[]
            {
                new { type = "Action.Execute", title = saveBtn, verb = "settingsUpdate", style = "positive" },
                new { type = "Action.Execute", title = cancelBtn, verb = "settingsCancel" }
            },
            schema = Schema
        };

        return JsonSerializer.Serialize(card);
    }

    private static string GetQuestionTypeLabelLocalized(QuestionType type, bool isJapanese)
    {
        return (type, isJapanese) switch
        {
            (QuestionType.WhyQuestion, true) => "🔍 Why質問",
            (QuestionType.ImpactQuestion, true) => "💥 影響質問",
            (QuestionType.ClarificationQuestion, true) => "🔎 明確化質問",
            (QuestionType.AlternativeQuestion, true) => "🔄 代替案質問",
            (QuestionType.TimelineQuestion, true) => "⏰ タイムライン質問",
            (QuestionType.StakeholderQuestion, true) => "👥 ステークホルダー質問",
            (QuestionType.RiskQuestion, true) => "⚠️ リスク質問",
            (QuestionType.ProcessQuestion, true) => "📋 プロセス質問",
            (QuestionType.PrecedentQuestion, true) => "📜 前例質問",
            (QuestionType.AssumptionQuestion, true) => "🤔 前提確認質問",
            (QuestionType.WhyQuestion, false) => "🔍 Why Question",
            (QuestionType.ImpactQuestion, false) => "💥 Impact Question",
            (QuestionType.ClarificationQuestion, false) => "🔎 Clarification",
            (QuestionType.AlternativeQuestion, false) => "🔄 Alternative",
            (QuestionType.TimelineQuestion, false) => "⏰ Timeline",
            (QuestionType.StakeholderQuestion, false) => "👥 Stakeholder",
            (QuestionType.RiskQuestion, false) => "⚠️ Risk",
            (QuestionType.ProcessQuestion, false) => "📋 Process",
            (QuestionType.PrecedentQuestion, false) => "📜 Precedent",
            (QuestionType.AssumptionQuestion, false) => "🤔 Assumption",
            _ => type.ToString()
        };
    }
}
