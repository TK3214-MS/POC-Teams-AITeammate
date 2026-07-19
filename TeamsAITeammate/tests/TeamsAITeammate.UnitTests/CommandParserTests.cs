using TeamsAITeammate.Infrastructure.Services;

namespace TeamsAITeammate.UnitTests;

public class CommandParserTests
{
    private readonly CommandParser _parser = new();

    [Theory]
    [InlineData("join", "join")]
    [InlineData("status", "status")]
    [InlineData("summarize", "summarize")]
    [InlineData("ask", "ask")]
    [InlineData("pause", "pause")]
    [InlineData("resume", "resume")]
    [InlineData("settings", "settings")]
    [InlineData("leave", "leave")]
    [InlineData("help", "help")]
    public void Parse_ExactEnglishCommand_ReturnsCorrectCommand(string input, string expected)
    {
        var result = _parser.Parse(input);

        Assert.Equal(expected, result.Command);
        Assert.True(result.IsRecognized);
        Assert.Null(result.Argument);
    }

    [Theory]
    [InlineData("参加", "join")]
    [InlineData("参加して", "join")]
    [InlineData("ステータス", "status")]
    [InlineData("状態", "status")]
    [InlineData("まとめ", "summarize")]
    [InlineData("まとめて", "summarize")]
    [InlineData("要約", "summarize")]
    [InlineData("サマリー", "summarize")]
    [InlineData("一時停止", "pause")]
    [InlineData("再開", "resume")]
    [InlineData("設定", "settings")]
    [InlineData("退出", "leave")]
    [InlineData("抜けて", "leave")]
    [InlineData("ヘルプ", "help")]
    public void Parse_JapaneseCommand_ReturnsCorrectCommand(string input, string expected)
    {
        var result = _parser.Parse(input);

        Assert.Equal(expected, result.Command);
        Assert.True(result.IsRecognized);
    }

    [Theory]
    [InlineData("start", "join")]
    [InlineData("connect", "join")]
    [InlineData("summary", "summarize")]
    [InlineData("question", "ask")]
    [InlineData("continue", "resume")]
    [InlineData("config", "settings")]
    [InlineData("exit", "leave")]
    [InlineData("disconnect", "leave")]
    public void Parse_EnglishAlias_ReturnsCorrectCommand(string input, string expected)
    {
        var result = _parser.Parse(input);

        Assert.Equal(expected, result.Command);
        Assert.True(result.IsRecognized);
    }

    [Fact]
    public void Parse_AskWithArgument_ReturnsCommandAndArgument()
    {
        var result = _parser.Parse("ask what is the deadline?");

        Assert.Equal("ask", result.Command);
        Assert.Equal("what is the deadline?", result.Argument);
        Assert.True(result.IsRecognized);
    }

    [Fact]
    public void Parse_SummarizeWithArgument_ReturnsCommandAndArgument()
    {
        var result = _parser.Parse("summarize last 10 minutes");

        Assert.Equal("summarize", result.Command);
        Assert.Equal("last 10 minutes", result.Argument);
        Assert.True(result.IsRecognized);
    }

    [Fact]
    public void Parse_TeamsAtMention_StripsTagsAndParsesCommand()
    {
        var result = _parser.Parse("<at>AI Teammate</at> join");

        Assert.Equal("join", result.Command);
        Assert.True(result.IsRecognized);
    }

    [Fact]
    public void Parse_TeamsAtMentionWithArgument_StripsTagsAndParsesCommandAndArgument()
    {
        var result = _parser.Parse("<at>AI Teammate</at> ask プロジェクトの進捗は？");

        Assert.Equal("ask", result.Command);
        Assert.Equal("プロジェクトの進捗は？", result.Argument);
        Assert.True(result.IsRecognized);
    }

    [Fact]
    public void Parse_EmptyText_ReturnsHelp()
    {
        var result = _parser.Parse("");

        Assert.Equal("help", result.Command);
        Assert.True(result.IsRecognized);
    }

    [Fact]
    public void Parse_WhitespaceOnly_ReturnsHelp()
    {
        var result = _parser.Parse("   ");

        Assert.Equal("help", result.Command);
        Assert.True(result.IsRecognized);
    }

    [Fact]
    public void Parse_AtMentionOnly_ReturnsHelp()
    {
        var result = _parser.Parse("<at>AI Teammate</at>");

        Assert.Equal("help", result.Command);
        Assert.True(result.IsRecognized);
    }

    [Fact]
    public void Parse_UnrecognizedText_ReturnsFallbackAsk()
    {
        var result = _parser.Parse("what is the meaning of life?");

        Assert.Equal("ask", result.Command);
        Assert.False(result.IsRecognized);
        Assert.NotNull(result.Argument);
    }

    [Fact]
    public void Parse_NaturalLanguageContainingKeyword_RecognizesCommand()
    {
        var result = _parser.Parse("今の状態を教えて");

        Assert.Equal("status", result.Command);
        Assert.True(result.IsRecognized);
    }

    [Fact]
    public void Parse_PreservesOriginalText()
    {
        var original = "<at>AI Teammate</at> summarize";
        var result = _parser.Parse(original);

        Assert.Equal(original, result.OriginalText);
    }

    [Theory]
    [InlineData("JOIN")]
    [InlineData("Join")]
    [InlineData("jOiN")]
    public void Parse_CaseInsensitive_ReturnsCorrectCommand(string input)
    {
        var result = _parser.Parse(input);

        Assert.Equal("join", result.Command);
        Assert.True(result.IsRecognized);
    }

    [Theory]
    [InlineData("rejoindre", "join")]
    [InlineData("résumer", "summarize")]
    [InlineData("quitter", "leave")]
    [InlineData("beitreten", "join")]
    [InlineData("unirse", "join")]
    public void Parse_MultiLanguageCommand_ReturnsCorrectCommand(string input, string expected)
    {
        var result = _parser.Parse(input);

        Assert.Equal(expected, result.Command);
        Assert.True(result.IsRecognized);
    }
}
