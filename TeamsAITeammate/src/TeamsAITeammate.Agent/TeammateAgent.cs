using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Teams;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

/// <summary>
/// AI Teammate bot — handles Teams messages, meeting events, and commands.
/// </summary>
public class TeammateAgent : AgentApplication
{
    private readonly IAnalysisEngine _analysisEngine;
    private readonly IMeetingSessionRepository _sessions;
    private readonly ITranscriptRepository _transcripts;
    private readonly IKnowledgeRepository _knowledge;

    public TeammateAgent(
        AgentApplicationOptions options,
        IAnalysisEngine analysisEngine,
        IMeetingSessionRepository sessions,
        ITranscriptRepository transcripts,
        IKnowledgeRepository knowledge)
        : base(options)
    {
        _analysisEngine = analysisEngine;
        _sessions = sessions;
        _transcripts = transcripts;
        _knowledge = knowledge;

        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, OnMembersAddedAsync);
        OnActivity(ActivityTypes.Message, OnMessageAsync);
    }

    private async Task OnMembersAddedAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken ct)
    {
        foreach (var member in turnContext.Activity.MembersAdded ?? [])
        {
            if (member.Id != turnContext.Activity.Recipient?.Id)
            {
                await turnContext.SendActivityAsync(
                    "👋 AI Teammateです。会議中の暗黙知を自動蓄積します。\n\n" +
                    "利用可能なコマンド:\n" +
                    "- **join** — 会議に参加してトランスクリプト分析を開始\n" +
                    "- **status** — 現在の分析状態を表示\n" +
                    "- **summarize** — これまでの会話サマリーを表示\n" +
                    "- **knowledge** — 蓄積されたナレッジを表示\n" +
                    "- **settings** — エージェント設定を変更",
                    cancellationToken: ct);
            }
        }
    }

    private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken ct)
    {
        var text = turnContext.Activity.Text?.Trim().ToLowerInvariant() ?? string.Empty;

        var response = text switch
        {
            "join" => await HandleJoinCommandAsync(turnContext, ct),
            "status" => await HandleStatusCommandAsync(turnContext, ct),
            "summarize" => await HandleSummarizeCommandAsync(turnContext, ct),
            "knowledge" => await HandleKnowledgeCommandAsync(turnContext, ct),
            "settings" => "⚙️ 設定画面はサイドパネルから開けます。",
            _ => $"「{turnContext.Activity.Text}」を受信しました。コマンド一覧は **help** と入力してください。"
        };

        await turnContext.SendActivityAsync(response, cancellationToken: ct);
    }

    private async Task<string> HandleJoinCommandAsync(ITurnContext turnContext, CancellationToken ct)
    {
        var meetingInfo = turnContext.Activity.ChannelData?.ToString();
        var session = new MeetingSession
        {
            TenantId = turnContext.Activity.Conversation?.TenantId ?? string.Empty,
            MeetingId = turnContext.Activity.Conversation?.Id ?? string.Empty,
            Subject = "Meeting Session",
            Status = MeetingStatus.InProgress,
            StartedAt = DateTimeOffset.UtcNow
        };

        await _sessions.UpsertAsync(session, ct);
        return "✅ 会議に参加しました。トランスクリプト分析を開始します。";
    }

    private async Task<string> HandleStatusCommandAsync(ITurnContext turnContext, CancellationToken ct)
    {
        var meetingId = turnContext.Activity.Conversation?.Id ?? string.Empty;
        var session = await _sessions.GetByMeetingIdAsync(meetingId, ct);

        if (session is null)
            return "📋 現在アクティブなセッションはありません。";

        var entries = await _transcripts.GetBySessionAsync(session.Id, ct);
        return $"📊 分析状況:\n- ステータス: {session.Status}\n- トランスクリプト数: {entries.Count}\n- 開始時刻: {session.StartedAt:HH:mm}";
    }

    private async Task<string> HandleSummarizeCommandAsync(ITurnContext turnContext, CancellationToken ct)
    {
        var meetingId = turnContext.Activity.Conversation?.Id ?? string.Empty;
        var session = await _sessions.GetByMeetingIdAsync(meetingId, ct);

        if (session is null)
            return "サマリーを生成するアクティブなセッションがありません。";

        var entries = await _transcripts.GetBySessionAsync(session.Id, ct);
        if (entries.Count == 0)
            return "トランスクリプトがまだありません。";

        var summary = await _analysisEngine.GenerateSummaryAsync(entries, ct);
        return $"📝 会話サマリー:\n\n{summary}";
    }

    private async Task<string> HandleKnowledgeCommandAsync(ITurnContext turnContext, CancellationToken ct)
    {
        var tenantId = turnContext.Activity.Conversation?.TenantId ?? string.Empty;
        var knowledgeEntries = await _knowledge.SearchAsync(tenantId, "*", limit: 5, ct: ct);

        if (knowledgeEntries.Count == 0)
            return "📚 蓄積されたナレッジはまだありません。";

        var lines = knowledgeEntries.Select(k => $"- **{k.Title}** ({k.Type}): {k.Content[..Math.Min(100, k.Content.Length)]}...");
        return $"📚 最近のナレッジ:\n\n{string.Join("\n", lines)}";
    }
}
