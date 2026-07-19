using System.Globalization;
using System.Text;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Infrastructure.Services;

public class MessageFormatter : IMessageFormatter
{
    private static readonly Dictionary<string, Dictionary<string, string>> Templates = new()
    {
        ["ja"] = new()
        {
            ["question"] = "💡 **追加で確認したい点があります**: {question}\n\n📝 *理由: {rationale}*",
            ["question_with_target"] = "💡 **{target}さんに確認したい点があります**: {question}\n\n📝 *理由: {rationale}*",
            ["summary_header"] = "📊 **会話サマリー**\n\n",
            ["topics_header"] = "🏷️ **トピック**\n",
            ["topic_item_active"] = "- 🟢 **{title}**: {summary}\n",
            ["topic_item_concluded"] = "- ✅ **{title}**: {summary}\n",
            ["topic_item_tabled"] = "- ⏸️ **{title}**: {summary}\n",
            ["decisions_header"] = "\n💡 **意思決定事項**\n",
            ["decision_item"] = "- {summary}\n",
            ["actions_header"] = "\n📋 **アクションアイテム**\n",
            ["action_item"] = "- [ ] {description} (担当: {assignee})\n",
            ["knowledge_header"] = "\n📚 **蓄積ナレッジ**: {count}件",
            ["questions_header"] = "\n❓ **未解決の質問**: {count}件",
            ["silence_prompt"] = "💬 会話が静かになりましたので、確認したい点を共有します。",
            ["topic_change"] = "📋 **{previous}** から **{new}** に話題が移りました。前のトピックについて:\n\n",
            ["periodic_check"] = "📊 定期チェック: ここまでの会話で確認したい点があります。",
        },
        ["en"] = new()
        {
            ["question"] = "💡 **I'd like to ask a follow-up**: {question}\n\n📝 *Reason: {rationale}*",
            ["question_with_target"] = "💡 **Question for {target}**: {question}\n\n📝 *Reason: {rationale}*",
            ["summary_header"] = "📊 **Conversation Summary**\n\n",
            ["topics_header"] = "🏷️ **Topics**\n",
            ["topic_item_active"] = "- 🟢 **{title}**: {summary}\n",
            ["topic_item_concluded"] = "- ✅ **{title}**: {summary}\n",
            ["topic_item_tabled"] = "- ⏸️ **{title}**: {summary}\n",
            ["decisions_header"] = "\n💡 **Decisions**\n",
            ["decision_item"] = "- {summary}\n",
            ["actions_header"] = "\n📋 **Action Items**\n",
            ["action_item"] = "- [ ] {description} (Assignee: {assignee})\n",
            ["knowledge_header"] = "\n📚 **Knowledge Captured**: {count} entries",
            ["questions_header"] = "\n❓ **Open Questions**: {count} entries",
            ["silence_prompt"] = "💬 The conversation has been quiet. Here's something I'd like to clarify.",
            ["topic_change"] = "📋 The discussion moved from **{previous}** to **{new}**. Regarding the previous topic:\n\n",
            ["periodic_check"] = "📊 Periodic check: I have some follow-up questions on the conversation so far.",
        },
    };

    public string FormatQuestion(GeneratedQuestion question, string language)
    {
        var lang = NormalizeLanguage(language);
        var templateKey = string.IsNullOrEmpty(question.TargetSpeaker) ? "question" : "question_with_target";
        var template = GetLocalizedTemplate(templateKey, lang);

        return template
            .Replace("{question}", question.Question, StringComparison.Ordinal)
            .Replace("{rationale}", question.Rationale, StringComparison.Ordinal)
            .Replace("{target}", question.TargetSpeaker, StringComparison.Ordinal);
    }

    public string FormatSummary(ConversationAnalysis analysis, string language)
    {
        var lang = NormalizeLanguage(language);
        var sb = new StringBuilder();

        sb.Append(GetLocalizedTemplate("summary_header", lang));

        // Topics
        if (analysis.Topics.Count > 0)
        {
            sb.Append(GetLocalizedTemplate("topics_header", lang));
            foreach (var topic in analysis.Topics)
            {
                var key = topic.Status switch
                {
                    TopicStatus.Active => "topic_item_active",
                    TopicStatus.Concluded => "topic_item_concluded",
                    TopicStatus.Tabled => "topic_item_tabled",
                    _ => "topic_item_active"
                };
                sb.Append(GetLocalizedTemplate(key, lang)
                    .Replace("{title}", topic.Title, StringComparison.Ordinal)
                    .Replace("{summary}", topic.Summary, StringComparison.Ordinal));
            }
        }

        // Decisions
        if (analysis.Decisions.Count > 0)
        {
            sb.Append(GetLocalizedTemplate("decisions_header", lang));
            foreach (var decision in analysis.Decisions)
            {
                sb.Append(GetLocalizedTemplate("decision_item", lang)
                    .Replace("{summary}", decision.Summary, StringComparison.Ordinal));
            }
        }

        // Action Items
        if (analysis.ActionItems.Count > 0)
        {
            sb.Append(GetLocalizedTemplate("actions_header", lang));
            foreach (var item in analysis.ActionItems)
            {
                sb.Append(GetLocalizedTemplate("action_item", lang)
                    .Replace("{description}", item.Description, StringComparison.Ordinal)
                    .Replace("{assignee}", string.IsNullOrEmpty(item.Assignee) ? "TBD" : item.Assignee, StringComparison.Ordinal));
            }
        }

        // Knowledge count
        if (analysis.TacitKnowledgeCandidates.Count > 0)
        {
            sb.Append(GetLocalizedTemplate("knowledge_header", lang)
                .Replace("{count}", analysis.TacitKnowledgeCandidates.Count.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal));
        }

        // Open questions count
        if (analysis.Questions.Count > 0)
        {
            sb.Append(GetLocalizedTemplate("questions_header", lang)
                .Replace("{count}", analysis.Questions.Count.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal));
        }

        return sb.ToString();
    }

    public string GetLocalizedTemplate(string templateKey, string language)
    {
        var lang = NormalizeLanguage(language);

        if (Templates.TryGetValue(lang, out var langTemplates) &&
            langTemplates.TryGetValue(templateKey, out var template))
        {
            return template;
        }

        // Fallback to English
        if (Templates.TryGetValue("en", out var enTemplates) &&
            enTemplates.TryGetValue(templateKey, out var enTemplate))
        {
            return enTemplate;
        }

        return $"[{templateKey}]";
    }

    private static string NormalizeLanguage(string language)
    {
        if (string.IsNullOrEmpty(language)) return "ja";

        var lower = language.ToLowerInvariant();
        if (lower.StartsWith("ja", StringComparison.Ordinal)) return "ja";
        if (lower.StartsWith("en", StringComparison.Ordinal)) return "en";

        return "en"; // Default fallback
    }
}
