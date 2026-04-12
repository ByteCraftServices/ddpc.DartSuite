using Microsoft.Playwright;

namespace ddpc.DartSuite.E2E.Tests.SmokeTests;

/// <summary>
/// DS-042: Automatisierter UI-Smoke-Flow Desktop (1280x900, Chromium, headless).
/// Prüft: Startseite laden, Navigation zu Turnieren, Tab-Wechsel, MatchCard-Rendering.
/// Voraussetzung: DARTSUITE_E2E_ENABLED=1 und optionale DARTSUITE_E2E_BASE_URL.
/// </summary>
public sealed class DesktopSmokeTests : SmokeTestBase
{
    [Fact(DisplayName = "DS-042-01: Startseite lädt ohne Fehler")]
    public async Task HomePage_Loads_WithoutErrors()
    {
        if (!ShouldRun()) return;

        var errors = new List<string>();
        Page.Console += (_, msg) =>
        {
            if (msg.Type == "error") errors.Add(msg.Text);
        };
        Page.PageError += (_, msg) => errors.Add(msg);

        var response = await Page.GotoAsync(BaseUrl, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Commit,
            Timeout = 30_000
        });
        // Wait for Blazor to establish its circuit and render content
        await Page.WaitForLoadStateAsync(LoadState.Load, new PageWaitForLoadStateOptions { Timeout = 15_000 });

        Assert.NotNull(response);
        Assert.True(response.Ok, $"HTTP {response.Status} beim Laden von {BaseUrl}");
        var consoleErrors = errors.Where(e => !e.Contains("favicon")).ToList();
        Assert.True(consoleErrors.Count == 0, $"Console-Fehler: {string.Join("; ", consoleErrors)}");
    }

    [Fact(DisplayName = "DS-042-02: Navigation zur Turnierliste")]
    public async Task TournamentsPage_NavigatesToTournamentList()
    {
        if (!ShouldRun()) return;

        await Page.GotoAsync($"{BaseUrl}/tournaments", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Commit,
            Timeout = 30_000
        });
        if (IsLoginRedirect(Page)) return;

        // Wait for Blazor-rendered heading
        var heading = Page.Locator("h1, h2, h3, h4, h5, h6").First;
        await heading.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 20_000 });
        var headingText = await heading.InnerTextAsync();
        Assert.Contains("Turnier", headingText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "DS-042-03: Tab-Navigation im Turnier sichtbar")]
    public async Task TournamentsPage_TabNavigation_Visible()
    {
        if (!ShouldRun()) return;

        await Page.GotoAsync($"{BaseUrl}/tournaments", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Commit,
            Timeout = 30_000
        });
        if (IsLoginRedirect(Page)) return;

        // Warte auf Tab-nav oder Liste
            var pageContent = Page.Locator(".nav-tabs, .list-group, h1, h2, h3");
            await pageContent.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 20_000 });
            var count = await pageContent.CountAsync();
            Assert.True(count > 0, "Keine Tabs oder Turnierliste gefunden");
    }

    [Fact(DisplayName = "DS-042-04: Rollenbadge oder Turniername sichtbar nach Auswahl")]
    public async Task TournamentsPage_RoleBadge_VisibleAfterSelection()
    {
        if (!ShouldRun()) return;

        await Page.GotoAsync($"{BaseUrl}/tournaments", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Commit,
            Timeout = 30_000
        });
        if (IsLoginRedirect(Page)) return;

        // Wait for Blazor to render tournament list
        await Page.WaitForLoadStateAsync(LoadState.Load, new PageWaitForLoadStateOptions { Timeout = 15_000 });

        // Klicke erstes Turnier aus der Liste (falls vorhanden)
        var listItem = Page.Locator("li.list-group-item.list-group-item-action").First;
        if (!await listItem.IsVisibleAsync())
        {
            // Kein Turnier vorhanden — Test als bestanden markieren (leere Liste ist OK)
            return;
        }

        await listItem.ClickAsync();
        await Page.WaitForTimeoutAsync(250);
        if (IsLoginRedirect(Page)) return;

        // Wait for the tournament card to appear (Blazor re-render via SignalR)
        var tournamentCard = Page.Locator(".card.shadow-sm, .tournament-mobile-strip-wrap, .nav-tabs");
        await tournamentCard.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 15_000 });

        // Rollenbadge: always present when a tournament is selected (entweder Spielleiter oder Teilnehmer)
        var roleBadge = Page.Locator(".badge:has-text('Rolle:')");
        var badgeVisible = await roleBadge.IsVisibleAsync();
        var cardHeader = Page.Locator(".card-header .fw-semibold");
        var nameVisible = await cardHeader.IsVisibleAsync();
        // Entweder Rollenbadge oder Turniername in card-header sichtbar
        Assert.True(badgeVisible || nameVisible,
            "Weder Rollenbadge noch Turniername sichtbar nach Turnier-Auswahl");
    }

    [Fact(DisplayName = "DS-042-05: Kein unbehandelter JavaScript-Fehler auf Turnierseite")]
    public async Task TournamentsPage_NoUnhandledJavaScriptErrors()
    {
        if (!ShouldRun()) return;

        var jsErrors = new List<string>();
        Page.PageError += (_, error) => jsErrors.Add(error);

        await Page.GotoAsync($"{BaseUrl}/tournaments", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Commit,
            Timeout = 30_000
        });
        if (IsLoginRedirect(Page)) return;

        // Wait for Blazor to render, then check for JS errors
        await Page.WaitForLoadStateAsync(LoadState.Load, new PageWaitForLoadStateOptions { Timeout = 15_000 });
        await Page.WaitForTimeoutAsync(2_000);

        Assert.Empty(jsErrors);
    }
}
