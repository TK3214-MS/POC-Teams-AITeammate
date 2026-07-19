using TeamsAITeammate.Core.Interfaces;
using TeamsAITeammate.Core.Models;

namespace TeamsAITeammate.Infrastructure.Services;

public class CommandParser : ICommandParser
{
    private static readonly Dictionary<string, string> CommandAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        // English
        ["join"] = "join",
        ["start"] = "join",
        ["connect"] = "join",
        ["status"] = "status",
        ["state"] = "status",
        ["summarize"] = "summarize",
        ["summary"] = "summarize",
        ["ask"] = "ask",
        ["question"] = "ask",
        ["pause"] = "pause",
        ["resume"] = "resume",
        ["continue"] = "resume",
        ["settings"] = "settings",
        ["config"] = "settings",
        ["leave"] = "leave",
        ["exit"] = "leave",
        ["disconnect"] = "leave",
        ["help"] = "help",

        // Japanese
        ["参加"] = "join",
        ["参加して"] = "join",
        ["開始"] = "join",
        ["ステータス"] = "status",
        ["状態"] = "status",
        ["状況"] = "status",
        ["まとめ"] = "summarize",
        ["まとめて"] = "summarize",
        ["要約"] = "summarize",
        ["要約して"] = "summarize",
        ["サマリー"] = "summarize",
        ["質問"] = "ask",
        ["聞いて"] = "ask",
        ["教えて"] = "ask",
        ["一時停止"] = "pause",
        ["停止"] = "pause",
        ["再開"] = "resume",
        ["再開して"] = "resume",
        ["設定"] = "settings",
        ["退出"] = "leave",
        ["退出して"] = "leave",
        ["抜けて"] = "leave",
        ["ヘルプ"] = "help",

        // French
        ["rejoindre"] = "join",
        ["résumé"] = "summarize",
        ["résumer"] = "summarize",
        ["quitter"] = "leave",

        // German
        ["beitreten"] = "join",
        ["zusammenfassung"] = "summarize",
        ["verlassen"] = "leave",

        // Spanish
        ["unirse"] = "join",
        ["resumen"] = "summarize",
        ["salir"] = "leave",

        // Chinese (Simplified)
        ["加入"] = "join",
        ["总结"] = "summarize",
        ["离开"] = "leave",

        // Korean
        ["참가"] = "join",
        ["요약"] = "summarize",
        ["나가기"] = "leave",
    };

    public CommandResult Parse(string mentionText)
    {
        var text = StripMentionTags(mentionText).Trim();

        if (string.IsNullOrWhiteSpace(text))
        {
            return new CommandResult
            {
                Command = "help",
                IsRecognized = true,
                OriginalText = mentionText,
            };
        }

        // Try exact match first
        if (CommandAliases.TryGetValue(text, out var exactCommand))
        {
            return new CommandResult
            {
                Command = exactCommand,
                IsRecognized = true,
                OriginalText = mentionText,
            };
        }

        // Try prefix match for commands with arguments (e.g., "ask what is X")
        var parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0 && CommandAliases.TryGetValue(parts[0], out var prefixCommand))
        {
            return new CommandResult
            {
                Command = prefixCommand,
                Argument = parts.Length > 1 ? parts[1] : null,
                IsRecognized = true,
                OriginalText = mentionText,
            };
        }

        // Try partial match for natural language input
        foreach (var (alias, command) in CommandAliases)
        {
            if (text.Contains(alias, StringComparison.OrdinalIgnoreCase))
            {
                var argStart = text.IndexOf(alias, StringComparison.OrdinalIgnoreCase) + alias.Length;
                var arg = text[argStart..].Trim();
                return new CommandResult
                {
                    Command = command,
                    Argument = string.IsNullOrWhiteSpace(arg) ? null : arg,
                    IsRecognized = true,
                    OriginalText = mentionText,
                };
            }
        }

        // Unrecognized — treat as an implicit "ask" with the full text as argument
        return new CommandResult
        {
            Command = "ask",
            Argument = text,
            IsRecognized = false,
            OriginalText = mentionText,
        };
    }

    private static string StripMentionTags(string text)
    {
        // Remove <at>...</at> tags from Teams @mention formatting
        var result = text;
        while (true)
        {
            var start = result.IndexOf("<at>", StringComparison.OrdinalIgnoreCase);
            if (start < 0) break;
            var end = result.IndexOf("</at>", start, StringComparison.OrdinalIgnoreCase);
            if (end < 0) break;
            result = string.Concat(result.AsSpan(0, start), result.AsSpan(end + 5));
        }
        return result;
    }
}
