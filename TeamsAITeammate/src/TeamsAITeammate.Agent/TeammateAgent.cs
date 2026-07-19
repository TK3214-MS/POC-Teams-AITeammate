using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Teams;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;
using TeamsAITeammate.Infrastructure.Services;

/// <summary>
/// AI Teammate bot — handles Teams messages, meeting events, commands, and Adaptive Card actions.
/// </summary>
public class TeammateAgent : AgentApplication
{
    private readonly IAnalysisEngine _analysisEngine;
    private readonly IMeetingSessionManager _sessionManager;
    private readonly IMeetingSessionRepository _sessions;
    private readonly ITranscriptRepository _transcripts;
    private readonly IKnowledgeRepository _knowledge;
    private readonly ICommandParser _commandParser;
    private readonly IInterventionTimer _interventionTimer;
    private readonly IInterventionOrchestrator _interventionOrchestrator;
    private readonly ICardActionHandler _cardActionHandler;
    private readonly ILogger<TeammateAgent> _logger;

    public TeammateAgent(
        AgentApplicationOptions options,
        IAnalysisEngine analysisEngine,
        IMeetingSessionManager sessionManager,
        IMeetingSessionRepository sessions,
        ITranscriptRepository transcripts,
        IKnowledgeRepository knowledge,
        ICommandParser commandParser,
        IInterventionTimer interventionTimer,
        IInterventionOrchestrator interventionOrchestrator,
        ICardActionHandler cardActionHandler,
        ILogger<TeammateAgent> logger)
        : base(options)
    {
        _analysisEngine = analysisEngine;
        _sessionManager = sessionManager;
        _sessions = sessions;
        _transcripts = transcripts;
        _knowledge = knowledge;
        _commandParser = commandParser;
        _interventionTimer = interventionTimer;
        _interventionOrchestrator = interventionOrchestrator;
        _cardActionHandler = cardActionHandler;
        _logger = logger;

        // Conversation lifecycle
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, OnMembersAddedAsync);
        OnConversationUpdate(ConversationUpdateEvents.MembersRemoved, OnMembersRemovedAsync);

        // Message handling
        OnActivity(ActivityTypes.Message, OnMessageAsync);

        // Meeting lifecycle events
        OnActivity(ActivityTypes.Event, OnEventActivityAsync);

        // Adaptive Card invoke actions
        OnActivity(ActivityTypes.Invoke, OnInvokeActivityAsync);
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
                    "- **ask [質問]** — 蓄積ナレッジに対して質問\n" +
                    "- **pause** / **resume** — 分析の一時停止・再開\n" +
                    "- **settings** — 設定変更\n" +
                    "- **leave** — 会議から退出",
                    cancellationToken: ct);
            }
        }
    }

    private async Task OnMembersRemovedAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken ct)
    {
        foreach (var member in turnContext.Activity.MembersRemoved ?? [])
        {
            if (member.Id == turnContext.Activity.Recipient?.Id)
            {
                _logger.LogInformation("Bot removed from conversation {ConversationId}", turnContext.Activity.Conversation?.Id);

                var meetingId = turnContext.Activity.Conversation?.Id ?? string.Empty;
                var session = await _sessionManager.GetActiveSessionAsync(meetingId, ct);
                if (session is not null)
                {
                    await _interventionTimer.StopAsync(session.Id, ct);
                    await _sessionManager.LeaveMeetingAsync(session.Id, ct);
                }
            }
        }
    }

    private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken ct)
    {
        var rawText = turnContext.Activity.Text ?? string.Empty;
        var command = _commandParser.Parse(rawText);

        _logger.LogInformation("Parsed command: {Command} (recognized: {IsRecognized}) from: {Original}",
            command.Command, command.IsRecognized, command.OriginalText);

        var response = command.Command switch
        {
            "join" => await HandleJoinCommandAsync(turnContext, ct),
            "status" => await HandleStatusCommandAsync(turnContext, ct),
            "summarize" => await HandleSummarizeCommandAsync(turnContext, ct),
            "ask" => await HandleAskCommandAsync(turnContext, command.Argument, ct),
            "pause" => await HandlePauseCommandAsync(turnContext, ct),
            "resume" => await HandleResumeCommandAsync(turnContext, ct),
            "settings" => HandleSettingsCommand(),
            "leave" => await HandleLeaveCommandAsync(turnContext, ct),
            "help" => HandleHelpCommand(),
            _ => $"「{rawText}」を受信しました。コマンド一覧は **help** と入力してください。",
        };

        await turnContext.SendActivityAsync(response, cancellationToken: ct);
    }

    private async Task OnEventActivityAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken ct)
    {
        var eventName = turnContext.Activity.Name ?? string.Empty;
        _logger.LogInformation("Received event: {EventName}", eventName);

        switch (eventName)
        {
            case "application/vnd.microsoft.meetingStart":
                await OnTeamsMeetingStartAsync(turnContext, ct);
                break;
            case "application/vnd.microsoft.meetingEnd":
                await OnTeamsMeetingEndAsync(turnContext, ct);
                break;
            case "application/vnd.microsoft.meetingParticipantJoin":
                await OnTeamsMeetingParticipantsJoinAsync(turnContext, ct);
                break;
            case "application/vnd.microsoft.meetingParticipantLeave":
                await OnTeamsMeetingParticipantsLeaveAsync(turnContext, ct);
                break;
        }
    }

    private async Task OnTeamsMeetingStartAsync(ITurnContext turnContext, CancellationToken ct)
    {
        var meetingId = turnContext.Activity.Conversation?.Id ?? string.Empty;
        var tenantId = turnContext.Activity.Conversation?.TenantId ?? string.Empty;
        _logger.LogInformation("Meeting started: {MeetingId} in tenant {TenantId}", meetingId, tenantId);

        await turnContext.SendActivityAsync(
            "📢 会議が開始されました。`@AI Teammate join` で分析を開始できます。",
            cancellationToken: ct);
    }

    private async Task OnTeamsMeetingEndAsync(ITurnContext turnContext, CancellationToken ct)
    {
        var meetingId = turnContext.Activity.Conversation?.Id ?? string.Empty;
        _logger.LogInformation("Meeting ended: {MeetingId}", meetingId);

        var session = await _sessionManager.GetActiveSessionAsync(meetingId, ct);
        if (session is not null)
        {
            await _interventionTimer.StopAsync(session.Id, ct);
            await _sessionManager.LeaveMeetingAsync(session.Id, ct);

            var entries = await _transcripts.GetBySessionAsync(session.Id, ct);
            if (entries.Count > 0)
            {
                var summary = await _analysisEngine.GenerateSummaryAsync(entries, ct);
                await turnContext.SendActivityAsync(
                    $"📝 会議終了サマリー:\n\n{summary}",
                    cancellationToken: ct);
            }
        }

        await turnContext.SendActivityAsync("👋 会議が終了しました。お疲れ様でした。", cancellationToken: ct);
    }

    private Task OnTeamsMeetingParticipantsJoinAsync(ITurnContext turnContext, CancellationToken ct)
    {
        _logger.LogInformation("Participants joined meeting {MeetingId}", turnContext.Activity.Conversation?.Id);
        return Task.CompletedTask;
    }

    private Task OnTeamsMeetingParticipantsLeaveAsync(ITurnContext turnContext, CancellationToken ct)
    {
        _logger.LogInformation("Participants left meeting {MeetingId}", turnContext.Activity.Conversation?.Id);
        return Task.CompletedTask;
    }

    private async Task<string> HandleJoinCommandAsync(ITurnContext turnContext, CancellationToken ct)
    {
        var meetingId = turnContext.Activity.Conversation?.Id ?? string.Empty;
        var tenantId = turnContext.Activity.Conversation?.TenantId ?? string.Empty;
        var organizerId = turnContext.Activity.From?.Id ?? string.Empty;

        var existing = await _sessionManager.GetActiveSessionAsync(meetingId, ct);
        if (existing is not null)
            return "✅ 既にこの会議に参加しています。現在の状態: " + existing.State;

        var session = await _sessionManager.JoinMeetingAsync(meetingId, tenantId, organizerId, ct);

        // Start intervention timer
        await _interventionTimer.StartAsync(session.Id, new InterventionSettings(), ct);

        return "✅ 会議に参加しました。トランスクリプト分析を開始します。\n" +
               $"セッションID: `{session.Id}`";
    }

    private async Task<string> HandleStatusCommandAsync(ITurnContext turnContext, CancellationToken ct)
    {
        var meetingId = turnContext.Activity.Conversation?.Id ?? string.Empty;
        var session = await _sessionManager.GetActiveSessionAsync(meetingId, ct);

        if (session is null)
            return "📋 現在アクティブなセッションはありません。`join` で参加してください。";

        var entries = await _transcripts.GetBySessionAsync(session.Id, ct);
        var knowledgeEntries = await _knowledge.GetBySessionAsync(session.Id, ct);

        return $"📊 分析状況:\n" +
               $"- セッション状態: **{session.State}**\n" +
               $"- トランスクリプト数: {entries.Count}\n" +
               $"- 検出ナレッジ数: {knowledgeEntries.Count}\n" +
               $"- 開始時刻: {session.StartedAt:HH:mm}";
    }

    private async Task<string> HandleSummarizeCommandAsync(ITurnContext turnContext, CancellationToken ct)
    {
        var meetingId = turnContext.Activity.Conversation?.Id ?? string.Empty;
        var session = await _sessionManager.GetActiveSessionAsync(meetingId, ct);

        if (session is null)
            return "サマリーを生成するアクティブなセッションがありません。";

        var entries = await _transcripts.GetBySessionAsync(session.Id, ct);
        if (entries.Count == 0)
            return "トランスクリプトがまだありません。";

        await _sessionManager.UpdateSessionStateAsync(session.Id, SessionState.Analyzing, ct);
        var summary = await _analysisEngine.GenerateSummaryAsync(entries, ct);
        await _sessionManager.UpdateSessionStateAsync(session.Id, SessionState.Active, ct);

        return $"📝 会話サマリー:\n\n{summary}";
    }

    private async Task<string> HandleAskCommandAsync(ITurnContext turnContext, string? question, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(question))
            return "❓ 質問を入力してください。例: `ask プロジェクトの締め切りは？`";

        var tenantId = turnContext.Activity.Conversation?.TenantId ?? string.Empty;
        var knowledgeEntries = await _knowledge.SearchAsync(tenantId, question, limit: 5, ct: ct);

        if (knowledgeEntries.Count == 0)
            return "📚 関連するナレッジが見つかりませんでした。";

        var lines = knowledgeEntries.Select(k =>
            $"- **{k.Title}** ({k.Type}): {k.Content[..Math.Min(100, k.Content.Length)]}...");
        return $"📚 関連ナレッジ:\n\n{string.Join("\n", lines)}";
    }

    private async Task<string> HandlePauseCommandAsync(ITurnContext turnContext, CancellationToken ct)
    {
        var meetingId = turnContext.Activity.Conversation?.Id ?? string.Empty;
        var session = await _sessionManager.GetActiveSessionAsync(meetingId, ct);

        if (session is null)
            return "アクティブなセッションがありません。";

        if (session.State == SessionState.Paused)
            return "⏸️ 既に一時停止中です。`resume` で再開できます。";

        await _sessionManager.UpdateSessionStateAsync(session.Id, SessionState.Paused, ct);
        await _interventionTimer.StopAsync(session.Id, ct);
        return "⏸️ 分析を一時停止しました。`resume` で再開できます。";
    }

    private async Task<string> HandleResumeCommandAsync(ITurnContext turnContext, CancellationToken ct)
    {
        var meetingId = turnContext.Activity.Conversation?.Id ?? string.Empty;
        var session = await _sessionManager.GetActiveSessionAsync(meetingId, ct);

        if (session is null)
            return "アクティブなセッションがありません。";

        if (session.State != SessionState.Paused)
            return "▶️ セッションは既にアクティブです。";

        await _sessionManager.UpdateSessionStateAsync(session.Id, SessionState.Active, ct);
        await _interventionTimer.StartAsync(session.Id, new InterventionSettings(), ct);
        return "▶️ 分析を再開しました。";
    }

    private static string HandleSettingsCommand()
    {
        var settingsCard = AdaptiveCardTemplates.BuildSettingsCard(new InterventionSettings(), "ja");
        // Return text summary — the card would be sent via the graph client in a full implementation
        return "⚙️ 設定変更:\n" +
               "- 沈黙検知閾値: 30秒\n" +
               "- 定期分析間隔: 5分\n" +
               "- プロアクティブ介入: 有効\n" +
               "- 最大介入回数: 20回/会議\n\n" +
               "設定の変更はサイドパネルから行えます。";
    }

    private async Task OnInvokeActivityAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken ct)
    {
        var activity = turnContext.Activity;
        if (activity.Name != "adaptiveCard/action") return;

        var value = activity.Value as Newtonsoft.Json.Linq.JObject
                    ?? Newtonsoft.Json.Linq.JObject.FromObject(activity.Value ?? new object());

        var verb = value["action"]?["verb"]?.ToString() ?? string.Empty;
        var data = value["action"]?["data"]?.ToObject<Dictionary<string, object>>() ?? [];

        var meetingId = activity.Conversation?.Id ?? string.Empty;
        var session = await _sessionManager.GetActiveSessionAsync(meetingId, ct);
        var sessionId = session?.Id ?? string.Empty;

        var result = await _cardActionHandler.HandleActionAsync(verb, data, sessionId, ct);

        _logger.LogInformation("Card action {Verb}: success={Success}, message={Message}",
            verb, result.Success, result.Message);
    }

    private async Task<string> HandleLeaveCommandAsync(ITurnContext turnContext, CancellationToken ct)
    {
        var meetingId = turnContext.Activity.Conversation?.Id ?? string.Empty;
        var session = await _sessionManager.GetActiveSessionAsync(meetingId, ct);

        if (session is null)
            return "アクティブなセッションがありません。";

        await _interventionTimer.StopAsync(session.Id, ct);
        await _sessionManager.LeaveMeetingAsync(session.Id, ct);
        return "👋 会議から退出しました。蓄積されたナレッジは保存されています。";
    }

    private static string HandleHelpCommand()
    {
        return "📖 AI Teammate コマンド一覧:\n\n" +
               "| コマンド | 動作 |\n" +
               "|---------|------|\n" +
               "| **join** | 会議に参加してトランスクリプト分析を開始 |\n" +
               "| **status** | 現在の分析状態を表示 |\n" +
               "| **summarize** | これまでの会話サマリーを表示 |\n" +
               "| **ask [質問]** | 蓄積ナレッジに対して質問 |\n" +
               "| **pause** | 分析を一時停止 |\n" +
               "| **resume** | 分析を再開 |\n" +
               "| **settings** | 設定を表示 |\n" +
               "| **leave** | 会議から退出 |\n\n" +
               "日本語でもコマンドを受け付けます（例: 「まとめて」「参加して」）";
    }
}
