using Microsoft.Playwright;

namespace TeamsAITeammate.E2ETests;

public class SidePanelTests : IAsyncLifetime
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null) await _browser.DisposeAsync();
        _playwright?.Dispose();
    }

    [Fact(Skip = "Requires side panel dev server running on localhost:5173")]
    public async Task SidePanel_LoadsDashboard()
    {
        var page = await _browser!.NewPageAsync();
        await page.GotoAsync("http://localhost:5173");

        var dashboard = page.Locator("[data-testid='analysis-dashboard']");
        await dashboard.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        Assert.True(await dashboard.IsVisibleAsync());
    }

    [Fact(Skip = "Requires side panel dev server running on localhost:5173")]
    public async Task SidePanel_ShowsTabNavigation()
    {
        var page = await _browser!.NewPageAsync();
        await page.GotoAsync("http://localhost:5173");

        var tabs = page.Locator("[role='tablist']");
        await tabs.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        Assert.True(await tabs.IsVisibleAsync());
    }

    [Fact(Skip = "Requires side panel dev server running on localhost:5173")]
    public async Task SidePanel_KnowledgeTab_ShowsList()
    {
        var page = await _browser!.NewPageAsync();
        await page.GotoAsync("http://localhost:5173");

        // Click Knowledge tab
        await page.Locator("text=Knowledge").ClickAsync();

        var knowledgeList = page.Locator("[data-testid='knowledge-list']");
        await knowledgeList.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        Assert.True(await knowledgeList.IsVisibleAsync());
    }

    [Fact(Skip = "Requires side panel dev server running on localhost:5173")]
    public async Task SidePanel_QuestionsTab_ShowsQueue()
    {
        var page = await _browser!.NewPageAsync();
        await page.GotoAsync("http://localhost:5173");

        // Click Questions tab
        await page.Locator("text=Questions").ClickAsync();

        var questionQueue = page.Locator("[data-testid='question-queue']");
        await questionQueue.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        Assert.True(await questionQueue.IsVisibleAsync());
    }

    [Fact(Skip = "Requires side panel dev server running on localhost:5173")]
    public async Task SidePanel_SettingsTab_ShowsSettings()
    {
        var page = await _browser!.NewPageAsync();
        await page.GotoAsync("http://localhost:5173");

        // Click Settings tab
        await page.Locator("text=Settings").ClickAsync();

        var settings = page.Locator("[data-testid='agent-settings']");
        await settings.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        Assert.True(await settings.IsVisibleAsync());
    }
}

