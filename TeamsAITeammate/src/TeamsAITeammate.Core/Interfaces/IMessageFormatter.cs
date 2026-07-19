using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Core.Interfaces;

public interface IMessageFormatter
{
    string FormatQuestion(GeneratedQuestion question, string language);
    string FormatSummary(ConversationAnalysis analysis, string language);
    string GetLocalizedTemplate(string templateKey, string language);
}
