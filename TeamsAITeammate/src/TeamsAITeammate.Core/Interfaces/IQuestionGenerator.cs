using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Core.Interfaces;

public interface IQuestionGenerator
{
    Task<IReadOnlyList<GeneratedQuestion>> GenerateQuestionsAsync(
        ConversationWindow conversation,
        AnalysisContext context,
        QuestionGenerationOptions options,
        CancellationToken ct = default);
}
