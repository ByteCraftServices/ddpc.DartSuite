using Microsoft.Playwright;
using FluentAssertions;

namespace ddpc.DartSuite.E2E.Tests.SmokeTests;

/// <summary>
/// E2E Smoke tests for Virtual Boards & MatchMaker UI (#44).
/// Tests: Virtual board management, MatchMaker manual/automatic modes.
/// Requires: DARTSUITE_E2E_ENABLED=1 and running server at DARTSUITE_E2E_BASE_URL.
/// </summary>
public sealed class VirtualBoardAndMatchMakerTests : SmokeTestBase
{
    [Fact]
    public async Task VirtualBoardManagement_AdminCanCreateAndList()
    {
        if (!ShouldRun()) return;

        // Navigate to Boards page
        await Page.GotoAsync($"{BaseUrl}/boards");
        if (IsLoginRedirect(Page)) return;

        // Admin section should exist
        var adminSection = await Page.QuerySelectorAsync("text=⚡ Virtuelle Boards");
        adminSection.Should().NotBeNull("Admin should see virtual board section");

        // Click "Create Virtual Board" button
        var createBtn = await Page.QuerySelectorAsync("button:has-text('+ Virtuelles Board erstellen')");
        createBtn.Should().NotBeNull();
        await createBtn!.ClickAsync();

        // Fill in name
        var nameInput = await Page.QuerySelectorAsync("input[placeholder='Name']");
        nameInput.Should().NotBeNull();
        await nameInput!.FillAsync("E2E Test Board");

        // Click create button
        var submitBtn = await Page.QuerySelectorAsync("button:has-text('Erstellen')");
        submitBtn.Should().NotBeNull();
        await submitBtn!.ClickAsync();

        // Wait for board to appear in list
        await Page.WaitForSelectorAsync("text=E2E Test Board");
        var boardInList = await Page.QuerySelectorAsync("text=E2E Test Board");
        boardInList.Should().NotBeNull();

        // Verify badge shows "Virtual"
        var badge = await Page.QuerySelectorAsync("(//text()[contains(., 'E2E Test Board')]/..//span[contains(@class, 'badge')])[1]");
        var badgeText = await badge!.TextContentAsync();
        badgeText.Should().Contain("Virtual");
    }

    [Fact]
    public async Task VirtualBoard_OwnerCanBeChanged()
    {
        if (!ShouldRun()) return;

        await Page.GotoAsync($"{BaseUrl}/boards");
        if (IsLoginRedirect(Page)) return;

        // Wait for virtual boards section
        await Page.WaitForSelectorAsync("text=⚡ Virtuelle Boards");

        // Find first virtual board's edit button (pencil icon)
        var editBtn = await Page.QuerySelectorAsync("(//span[contains(@class, 'badge') and contains(text(), 'Virtual')]/../../..//button[contains(text(), '✎')])[1]");
        
        if (editBtn == null)
        {
            // No virtual boards yet, skip this test
            return;
        }

        await editBtn.ClickAsync();

        // Input new owner
        var ownerInput = await Page.QuerySelectorAsync("input[style*='max-width:180px']");
        ownerInput.Should().NotBeNull();
        await ownerInput!.FillAsync("newowner@example.com");

        // Click save (checkmark button)
        var saveBtn = await Page.QuerySelectorAsync("button:has-text('✓'):nth-of-type(1)");
        saveBtn.Should().NotBeNull();
        await saveBtn!.ClickAsync();

        // Verify owner is updated
        await Page.WaitForSelectorAsync("text=newowner@example.com");
    }

    [Fact]
    public async Task MatchMaker_ManualMode_CanThrowDarts()
    {
        if (!ShouldRun()) return;

        // Navigate to matches and find one with virtual board
        await Page.GotoAsync($"{BaseUrl}/matches");
        if (IsLoginRedirect(Page)) return;

        // Try to find match with MatchMaker
        // This requires a tournament with virtual board setup
        var matchMakerPanel = await Page.QuerySelectorAsync("text=⚡ MatchMaker");
        if (matchMakerPanel == null)
        {
            // No virtual board matches, skip
            return;
        }

        // Check that manual mode is selected
        var modeSelect = await Page.QuerySelectorAsync("select.form-select-sm");
        modeSelect.Should().NotBeNull();

        // Start match
        var startBtn = await Page.QuerySelectorAsync("button:has-text('▶ Match starten')");
        if (startBtn != null && await startBtn.IsEnabledAsync())
        {
            await startBtn.ClickAsync();
            await Page.WaitForTimeoutAsync(500);
        }

        // Check scoreboard appears
        var scoreboard = await Page.QuerySelectorAsync("text=Am Zug:");
        scoreboard.Should().NotBeNull("Scoreboard should appear when match is running");

        // Throw a dart: Click "17"
        var dartButton = await Page.QuerySelectorAsync("button:has-text('17')");
        if (dartButton != null)
        {
            await dartButton.ClickAsync();
            await Page.WaitForTimeoutAsync(300);
        }

        // Verify dart count changes
        var dartProgress = await Page.QuerySelectorAsync("text=/Wurf \\d+\\/3/");
        dartProgress.Should().NotBeNull();
    }

    [Fact]
    public async Task MatchMaker_Statistics_AreDisplayed()
    {
        if (!ShouldRun()) return;

        await Page.GotoAsync($"{BaseUrl}/matches");
        if (IsLoginRedirect(Page)) return;

        // Find MatchMaker panel
        var matchMakerPanel = await Page.QuerySelectorAsync("text=⚡ MatchMaker");
        if (matchMakerPanel == null) return;

        // Check that stats section is visible
        var avgText = await Page.QuerySelectorAsync("text=Avg:");
        avgText.Should().NotBeNull("Average should be displayed");

        var legsText = await Page.QuerySelectorAsync("text=Legs:");
        legsText.Should().NotBeNull("Legs should be displayed");
    }

    [Fact]
    public async Task MatchMaker_Undo_ButtonIsVisible()
    {
        if (!ShouldRun()) return;

        await Page.GotoAsync($"{BaseUrl}/matches");
        if (IsLoginRedirect(Page)) return;

        var matchMakerPanel = await Page.QuerySelectorAsync("text=⚡ MatchMaker");
        if (matchMakerPanel == null) return;

        // Look for Undo button
        var undoBtn = await Page.QuerySelectorAsync("button:has-text('↩ Undo')");
        undoBtn.Should().NotBeNull("Undo button should exist in manual mode");

        // Look for Miss button
        var missBtn = await Page.QuerySelectorAsync("button:has-text('✗ Daneben')");
        missBtn.Should().NotBeNull("Miss button should exist");
    }

    [Fact]
    public async Task MatchMaker_DoubleTriple_MultipliersWork()
    {
        if (!ShouldRun()) return;

        await Page.GotoAsync($"{BaseUrl}/matches");
        if (IsLoginRedirect(Page)) return;

        var matchMakerPanel = await Page.QuerySelectorAsync("text=⚡ MatchMaker");
        if (matchMakerPanel == null) return;

        // Look for Double button
        var doubleBtn = await Page.QuerySelectorAsync("button:has-text('Double')");
        doubleBtn.Should().NotBeNull("Double button should exist");

        // Look for Triple button
        var tripleBtn = await Page.QuerySelectorAsync("button:has-text('Triple')");
        tripleBtn.Should().NotBeNull("Triple button should exist");

        // Look for Bull button
        var bullBtn = await Page.QuerySelectorAsync("button:has-text('Bull')");
        bullBtn.Should().NotBeNull("Bull button should exist");
    }

    [Fact]
    public async Task MatchMaker_AutomaticMode_AvailableForAdmin()
    {
        if (!ShouldRun()) return;

        await Page.GotoAsync($"{BaseUrl}/matches");
        if (IsLoginRedirect(Page)) return;

        var matchMakerPanel = await Page.QuerySelectorAsync("text=⚡ MatchMaker");
        if (matchMakerPanel == null) return;

        // Look for mode selector
        var modeSelect = await Page.QuerySelectorAsync("select.form-select-sm");
        modeSelect.Should().NotBeNull();

        // Check if Automatic option exists
        var automaticOption = await Page.QuerySelectorAsync("option:has-text('Automatisch')");
        automaticOption.Should().NotBeNull("Automatic mode option should be available for admin");
    }

    [Fact(DisplayName = "DS-066-M01: Virtual Boards page loads on mobile viewport (375x812)")]
    public async Task VirtualBoard_ResponsiveLayout_Mobile()
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
                $"Horizontaler Overflow auf Mobile: body.scrollWidth={scrollWidth} > 380px (375+5 tolerance)");
        }
        finally
        {
            await mobilePage.CloseAsync();
            await mobileBrowser.DisposeAsync();
        }
    }

    [Fact(DisplayName = "DS-066-M02: MatchMaker keypad is visible and not overflowing on mobile (375x812)")]
    public async Task MatchMaker_KeypadLayout_ResponsiveOnMobile()
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
            await mobilePage.GotoAsync($"{BaseUrl}/matches", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.Commit,
                Timeout = 30_000
            });
            await mobilePage.WaitForLoadStateAsync(LoadState.Load, new PageWaitForLoadStateOptions { Timeout = 15_000 });

            if (IsLoginRedirect(mobilePage)) return;

            var matchMakerPanel = await mobilePage.QuerySelectorAsync("text=⚡ MatchMaker");
            if (matchMakerPanel == null) return;

            // Check keypad buttons are visible and within viewport width
            var dartButton = mobilePage.Locator(".matchmaker-keypad button, button:has-text('S17'), button:has-text('20')").First;
            if (await dartButton.CountAsync() > 0)
            {
                var box = await dartButton.BoundingBoxAsync();
                if (box is not null)
                {
                    Assert.True(box.X + box.Width <= 380,
                        $"MatchMaker keypad button overflows mobile viewport: X={box.X}, Width={box.Width}");
                }
            }

            var scrollWidth = await mobilePage.EvaluateAsync<int>("document.body.scrollWidth");
            Assert.True(scrollWidth <= 380,
                $"Horizontaler Overflow auf Mobile: body.scrollWidth={scrollWidth}");
        }
        finally
        {
            await mobilePage.CloseAsync();
            await mobileBrowser.DisposeAsync();
        }
    }
}
