using System.Text;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.AI.Services;

public class AnalysisEngine : IAnalysisEngine
{
    private readonly ChatClient _chatClient;
    private readonly string _deploymentName;

    public AnalysisEngine(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAI:Endpoint"]!;
        _deploymentName = configuration["AzureOpenAI:DeploymentName"] ?? "gpt-55";

        var azureClient = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());
        _chatClient = azureClient.GetChatClient(_deploymentName);
    }

    public async Task<IReadOnlyList<KnowledgeEntry>> AnalyzeTranscriptAsync(
        IReadOnlyList<TranscriptEntry> entries,
        MeetingSession session,
        CancellationToken ct = default)
    {
        if (entries.Count == 0)
            return [];

        var transcript = FormatTranscript(entries);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(
                """
                あなたは会議のトランスクリプトを分析し、暗黙知を抽出するAIアシスタントです。
                以下の観点で分析してください:
                1. 暗黙知（TacitKnowledge）: 明示的に文書化されていない知識やノウハウ
                2. 意思決定（Decision）: 会議で行われた決定事項
                3. アクションアイテム（ActionItem）: 誰が何をいつまでに行うか
                4. インサイト（Insight）: 重要な洞察や気づき
                5. 質問（Question）: 未解決の質問や確認事項
                6. リスク（Risk）: 識別されたリスクや懸念事項

                JSON配列形式で出力してください。各要素は以下の形式:
                {"title": "タイトル", "content": "詳細内容", "type": "KnowledgeType", "tags": ["タグ1"], "confidenceScore": 0.0-1.0}
                """),
            new UserChatMessage($"以下の会議トランスクリプトを分析してください:\n\n{transcript}")
        };

        var response = await _chatClient.CompleteChatAsync(messages, cancellationToken: ct);

        // Parse response and create KnowledgeEntry objects
        // For now, return a placeholder — full parsing will be implemented in Phase 4
        return
        [
            new KnowledgeEntry
            {
                TenantId = session.TenantId,
                SessionId = session.Id,
                Title = "会議分析結果",
                Content = response.Value.Content[0].Text,
                Type = KnowledgeType.Insight,
                ConfidenceScore = 0.8
            }
        ];
    }

    public async Task<string> GenerateSummaryAsync(
        IReadOnlyList<TranscriptEntry> entries,
        CancellationToken ct = default)
    {
        if (entries.Count == 0)
            return "トランスクリプトがありません。";

        var transcript = FormatTranscript(entries);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(
                """
                あなたは会議のトランスクリプトから簡潔なサマリーを作成するAIアシスタントです。
                以下の構成でサマリーを作成してください:
                1. 概要（2-3文）
                2. 主要な議題
                3. 決定事項
                4. アクションアイテム
                5. 次のステップ
                """),
            new UserChatMessage($"以下の会議トランスクリプトのサマリーを作成してください:\n\n{transcript}")
        };

        var response = await _chatClient.CompleteChatAsync(messages, cancellationToken: ct);
        return response.Value.Content[0].Text;
    }

    private static string FormatTranscript(IReadOnlyList<TranscriptEntry> entries)
    {
        var sb = new StringBuilder();
        foreach (var entry in entries)
        {
            sb.AppendLine($"[{entry.Timestamp:HH:mm:ss}] {entry.SpeakerName}: {entry.Text}");
        }
        return sb.ToString();
    }
}
