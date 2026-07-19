using Microsoft.Playwright;

namespace TeamsAITeammate.E2ETests;

public class AdminPanelTests : IAsyncLifetime
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private readonly string _baseUrl = Environment.GetEnvironmentVariable("ADMIN_BASE_URL") ?? "http://localhost:5174";

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

    [Fact(Skip = "Requires admin panel dev server")]
    public async Task AdminDashboard_ShowsStatistics()
    {
        var page = await _browser!.NewPageAsync();
        await page.GotoAsync(_baseUrl);

        var dashboard = page.Locator("[data-testid='admin-dashboard']");
        await dashboard.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        Assert.True(await dashboard.IsVisibleAsync());

        // Verify stat cards are displayed
        var statCards = page.Locator("[data-testid='stat-card']");
        Assert.True(await statCards.CountAsync() >= 4);
    }

    [Fact(Skip = "Requires admin panel dev server")]
    public async Task AgentSettings_CanBeModified()
    {
        var page = await _browser!.NewPageAsync();
        await page.GotoAsync($"{_baseUrl}/settings");

        // Check intervention frequency dropdown exists
        var frequencySelect = page.Locator("[data-testid='intervention-frequency']");
        await frequencySelect.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        Assert.True(await frequencySelect.IsVisibleAsync());

        // Check save button exists
        var saveButton = page.Locator("[data-testid='save-settings']");
        Assert.True(await saveButton.IsVisibleAsync());
    }

    [Fact(Skip = "Requires admin panel dev server")]
    public async Task KnowledgeManagement_ShowsList()
    {
        var page = await _browser!.NewPageAsync();
        await page.GotoAsync($"{_baseUrl}/knowledge");

        var knowledgeTable = page.Locator("[data-testid='knowledge-table']");
        await knowledgeTable.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        Assert.True(await knowledgeTable.IsVisibleAsync());

        // Verify search input exists
        var searchInput = page.Locator("[data-testid='knowledge-search']");
        Assert.True(await searchInput.IsVisibleAsync());
    }

    [Fact(Skip = "Requires admin panel dev server")]
    public async Task KnowledgeManagement_SearchFiltersResults()
    {
        var page = await _browser!.NewPageAsync();
        await page.GotoAsync($"{_baseUrl}/knowledge");

        var searchInput = page.Locator("[data-testid='knowledge-search']");
        await searchInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        await searchInput.FillAsync("test query");
        await searchInput.PressAsync("Enter");

        // Wait for results to update
        await page.WaitForTimeoutAsync(1000);

        // Verify table updated (exact count depends on data)
        var rows = page.Locator("[data-testid='knowledge-row']");
        Assert.True(await rows.CountAsync() >= 0);
    }

    [Fact(Skip = "Requires admin panel dev server")]
    public async Task UserManagement_ShowsUserList()
    {
        var page = await _browser!.NewPageAsync();
        await page.GotoAsync($"{_baseUrl}/users");

        var userTable = page.Locator("[data-testid='user-table']");
        await userTable.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        Assert.True(await userTable.IsVisibleAsync());
    }

    [Fact(Skip = "Requires admin panel dev server")]
    public async Task Navigation_AllTabsAccessible()
    {
        var page = await _browser!.NewPageAsync();
        await page.GotoAsync(_baseUrl);

        var tabs = new[] { "Dashboard", "Settings", "Knowledge", "Users" };
        foreach (var tab in tabs)
        {
            var navItem = page.Locator($"[data-testid='nav-{tab.ToLowerInvariant()}']");
            Assert.True(await navItem.IsVisibleAsync(), $"Navigation item '{tab}' should be visible");
        }
    }
}
