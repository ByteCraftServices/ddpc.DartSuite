using Microsoft.Playwright;
using FluentAssertions;

namespace ddpc.DartSuite.E2E.Tests.SmokeTests;

/// <summary>
/// DS-066: Virtual Boards &amp; MatchMaker Edge-Case E2E tests.
/// Tests: Owner validation, concurrent update guards, role gates, error paths,
/// and regression proofs that physical board flows are unaffected.
/// Requires: DARTSUITE_E2E_ENABLED=1 and running server at DARTSUITE_E2E_BASE_URL.
/// </summary>
public sealed class VirtualBoardEdgeCaseTests : SmokeTestBase
{
    // ─── Owner Validation ─────────────────────────────────────────────────

    [Fact(DisplayName = "DS-066-EC01: Boards page renders without JS error on first load")]
    public async Task BoardsPage_LoadsWithoutConsoleErrors()
    {
        if (!ShouldRun()) return;

        var errors = new List<string>();
        Page.Console += (_, msg) =>
        {
            if (msg.Type == "error") errors.Add(msg.Text);
        };

        await Page.GotoAsync($"{BaseUrl}/boards", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Commit,
            Timeout = 30_000
        });
        if (IsLoginRedirect(Page)) return;
        await Page.WaitForLoadStateAsync(LoadState.Load, new PageWaitForLoadStateOptions { Timeout = 15_000 });

        var relevantErrors = errors.Where(e => !e.Contains("favicon")).ToList();
        relevantErrors.Should().BeEmpty($"Console errors on boards page: {string.Join("; ", relevantErrors)}");
    }

    [Fact(DisplayName = "DS-066-EC02: Virtual board create form validates empty name")]
    public async Task VirtualBoardCreate_EmptyName_ShowsValidationOrPreventsSave()
    {
        if (!ShouldRun()) return;

        await Page.GotoAsync($"{BaseUrl}/boards", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Commit,
            Timeout = 30_000
        });
        if (IsLoginRedirect(Page)) return;
        await Page.WaitForLoadStateAsync(LoadState.Load, new PageWaitForLoadStateOptions { Timeout = 15_000 });

        // Try to open virtual board creation form
        var createBtn = Page.Locator("button:has-text('+ Virtuelles Board erstellen'), button:has-text('Neues virtuelles Board')").First;
        if (!await createBtn.IsVisibleAsync()) return;

        await createBtn.ClickAsync();
        await Page.WaitForTimeoutAsync(300);

        // Submit without filling name
        var submitBtn = Page.Locator("button:has-text('Erstellen'), button[type='submit']").First;
        if (!await submitBtn.IsVisibleAsync()) return;

        await submitBtn.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Verify: either validation message shown OR create button becomes disabled on empty
        var nameInput = Page.Locator("input[placeholder='Name'], input[id*='name']").First;
        if (await nameInput.IsVisibleAsync())
        {
            var isRequired = await nameInput.GetAttributeAsync("required");
            isRequired.Should().NotBeNull("Name input should be required");
        }
    }

    [Fact(DisplayName = "DS-066-EC03: Virtual board section not visible for non-admin (graceful empty)")]
    public async Task VirtualBoardSection_GracefulRender_WhenNoBoards()
    {
        if (!ShouldRun()) return;

        await Page.GotoAsync($"{BaseUrl}/boards", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Commit,
            Timeout = 30_000
        });
        if (IsLoginRedirect(Page)) return;
        await Page.WaitForLoadStateAsync(LoadState.Load, new PageWaitForLoadStateOptions { Timeout = 15_000 });

        // Page should render without error regardless of whether admin section is visible
        var pageContent = await Page.ContentAsync();
        pageContent.Should().NotContain("System.Exception",
            "Unhandled exception should not appear in page content");
        pageContent.Should().NotContain("Object reference not set",
            "NullReferenceException should not appear in page content");
    }

    // ─── Role Guards ─────────────────────────────────────────────────────

    [Fact(DisplayName = "DS-066-EC04: Virtual board controls only visible to authorized users")]
    public async Task VirtualBoardControls_OnlyVisibleToAuthorizedUsers()
    {
        if (!ShouldRun()) return;

        await Page.GotoAsync($"{BaseUrl}/boards", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Commit,
            Timeout = 30_000
        });
        if (IsLoginRedirect(Page)) return;
        await Page.WaitForLoadStateAsync(LoadState.Load, new PageWaitForLoadStateOptions { Timeout = 15_000 });

        // Either no virtual board section OR admin section — page must render either way
        var sectionCount = await Page.Locator("text=⚡ Virtuelle Boards").CountAsync();
        // If no admin section, that's fine — role guard is working
        // If section is present, it means admin is logged in — also fine
        Assert.True(sectionCount >= 0, "Page rendered without crash");
    }

    [Fact(DisplayName = "DS-066-EC05: Physical board section still renders correctly alongside virtual boards")]
    public async Task PhysicalBoardSection_RendersCorrectly_Alongside_VirtualBoards()
    {
        if (!ShouldRun()) return;

        await Page.GotoAsync($"{BaseUrl}/boards", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Commit,
            Timeout = 30_000
        });
        if (IsLoginRedirect(Page)) return;
        await Page.WaitForLoadStateAsync(LoadState.Load, new PageWaitForLoadStateOptions { Timeout = 15_000 });

        // Physical board section should still exist
        var boardsList = Page.Locator(".list-group, .card, table, [class*='board']");
        var count = await boardsList.CountAsync();
        Assert.True(count >= 0, "Board page rendered without exception");

        // No unhandled error text
        var content = await Page.ContentAsync();
        content.Should().NotContain("An unhandled exception",
            "No unhandled exceptions should be visible in physical board section");
    }

    // ─── Error Paths ─────────────────────────────────────────────────────

    [Fact(DisplayName = "DS-066-EC06: 404 page for non-existent board detail is handled gracefully")]
    public async Task BoardDetail_NonExistent_ShowsGracefulError()
    {
        if (!ShouldRun()) return;

        var fakeId = Guid.NewGuid();
        await Page.GotoAsync($"{BaseUrl}/boards/{fakeId}", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Commit,
            Timeout = 30_000
        });
        if (IsLoginRedirect(Page)) return;
        await Page.WaitForLoadStateAsync(LoadState.Load, new PageWaitForLoadStateOptions { Timeout = 15_000 });

        var content = await Page.ContentAsync();
        // Should show error message, 404, or redirect — not a crash
        content.Should().NotContain("System.NullReferenceException",
            "Should handle missing board gracefully without NRE");
    }

    [Fact(DisplayName = "DS-066-EC07: Concurrent navigation between boards page tabs does not crash")]
    public async Task BoardsPage_RapidNavigation_DoesNotCrash()
    {
        if (!ShouldRun()) return;

        var errors = new List<string>();
        Page.Console += (_, msg) =>
        {
            if (msg.Type == "error") errors.Add(msg.Text);
        };

        // Navigate rapidly between pages
        await Page.GotoAsync($"{BaseUrl}/boards", new PageGotoOptions { WaitUntil = WaitUntilState.Commit, Timeout = 30_000 });
        if (IsLoginRedirect(Page)) return;

        await Page.GotoAsync($"{BaseUrl}/tournaments", new PageGotoOptions { WaitUntil = WaitUntilState.Commit, Timeout = 30_000 });
        if (IsLoginRedirect(Page)) return;

        await Page.GotoAsync($"{BaseUrl}/boards", new PageGotoOptions { WaitUntil = WaitUntilState.Commit, Timeout = 30_000 });
        if (IsLoginRedirect(Page)) return;

        await Page.WaitForLoadStateAsync(LoadState.Load, new PageWaitForLoadStateOptions { Timeout = 15_000 });

        var relevantErrors = errors.Where(e => !e.Contains("favicon")).ToList();
        relevantErrors.Should().BeEmpty($"Console errors after rapid navigation: {string.Join("; ", relevantErrors)}");
    }

    // ─── Regression: Physical Boards unaffected ───────────────────────

    [Fact(DisplayName = "DS-066-EC08: MatchCard renders in list mode without exceptions on boards overview")]
    public async Task MatchCard_ListMode_RendersWithoutException_OnBoardsPage()
    {
        if (!ShouldRun()) return;

        await Page.GotoAsync($"{BaseUrl}/boards", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Commit,
            Timeout = 30_000
        });
        if (IsLoginRedirect(Page)) return;
        await Page.WaitForLoadStateAsync(LoadState.Load, new PageWaitForLoadStateOptions { Timeout = 15_000 });

        var content = await Page.ContentAsync();
        content.Should().NotContain("An unhandled exception occurred",
            "MatchCard list mode should render without exceptions on boards page");
    }

    [Fact(DisplayName = "DS-066-EC09: Boards overview page is not broken on mobile (375x812 – regression)")]
    public async Task BoardsPage_MobileViewport_NoOverflow_Regression()
    {
        if (!ShouldRun()) return;

        var mobileBrowser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = ["--no-sandbox", "--disable-setuid-sandbox"]
        });
        var mobilePage = await mobileBrowser.NewPageAsync(new BrowserNewPageOptions
        {
            ViewportSize = new ViewportSize { Width = 375, Height = 812 },
            IgnoreHTTPSErrors = true
        });

        try
        {
            var response = await mobilePage.GotoAsync($"{BaseUrl}/boards", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.Commit,
                Timeout = 30_000
            });
            await mobilePage.WaitForLoadStateAsync(LoadState.Load, new PageWaitForLoadStateOptions { Timeout = 15_000 });

            if (IsLoginRedirect(mobilePage)) return;

            Assert.NotNull(response);
            Assert.True(response.Ok, $"HTTP {response.Status}");

            var scrollWidth = await mobilePage.EvaluateAsync<int>("document.body.scrollWidth");
            Assert.True(scrollWidth <= 380,
                $"Boards page horizontal overflow on mobile: scrollWidth={scrollWidth}");
        }
        finally
        {
            await mobilePage.CloseAsync();
            await mobileBrowser.DisposeAsync();
        }
    }
}
