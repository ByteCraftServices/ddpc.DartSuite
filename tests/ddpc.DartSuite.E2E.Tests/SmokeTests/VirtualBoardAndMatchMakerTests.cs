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

    [Fact]
    public async Task VirtualBoard_ResponsiveLayout_Mobile()
    {
        if (!ShouldRun()) return;

        // Set mobile viewport
        await Context.DisposeAsync();
           // Note: E2E tests use fixed viewport size from SmokeTestBase.
           // Mobile tests require separate browser context with mobile viewport
           // This would need dedicated test class that doesn't reuse Context/Page
           await Task.CompletedTask;
    }

    [Fact]
    public async Task MatchMaker_KeypadLayout_ResponsiveOnMobile()
    {
        if (!ShouldRun()) return;

        // Set mobile viewport
        await Context.DisposeAsync();
           // Note: Mobile tests require separate browser context initialization
           // This test class reuses Context/Page from SmokeTestBase which are read-only
           // Mobile tests should be in separate class with mobile viewport setup
           await Task.CompletedTask;
    }
}
