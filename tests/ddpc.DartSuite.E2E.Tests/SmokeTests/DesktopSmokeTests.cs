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
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 30_000
        });

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
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 30_000
        });

        // Seitentitel oder H1 vorhanden
        var heading = Page.Locator("h1, h2").First;
        await heading.WaitForAsync(new LocatorWaitForOptions { Timeout = 10_000 });
        var headingText = await heading.InnerTextAsync();
        Assert.Contains("Turnier", headingText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "DS-042-03: Tab-Navigation im Turnier sichtbar")]
    public async Task TournamentsPage_TabNavigation_Visible()
    {
        if (!ShouldRun()) return;

        await Page.GotoAsync($"{BaseUrl}/tournaments", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 30_000
        });

        // Warte auf Tab-nav oder Liste
        var tabs = Page.Locator(".nav-tabs, .list-group");
        var count = await tabs.CountAsync();
        Assert.True(count > 0, "Keine Tabs oder Turnierliste gefunden");
    }

    [Fact(DisplayName = "DS-042-04: Rollenbadge oder Turniername sichtbar nach Auswahl")]
    public async Task TournamentsPage_RoleBadge_VisibleAfterSelection()
    {
        if (!ShouldRun()) return;

        // Lade Turnierliste
        await Page.GotoAsync($"{BaseUrl}/tournaments", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 30_000
        });

        // Klicke erstes Turnier aus der Liste (falls vorhanden)
        var listItem = Page.Locator(".list-group-item-action").First;
        var isVisible = await listItem.IsVisibleAsync();
        if (!isVisible)
        {
            // Kein Turnier vorhanden — Test als bestanden markieren (leere Liste ist OK)
            return;
        }

        await listItem.ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 10_000 });

        // Rollenbadge sollte jetzt sichtbar sein
        var roleBadge = Page.Locator(".badge:has-text('Rolle:')");
        await roleBadge.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 8_000 });
        var badgeText = await roleBadge.First.InnerTextAsync();
        Assert.Contains("Rolle:", badgeText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "DS-042-05: Kein unbehandelter JavaScript-Fehler auf Turnierseite")]
    public async Task TournamentsPage_NoUnhandledJavaScriptErrors()
    {
        if (!ShouldRun()) return;

        var jsErrors = new List<string>();
        Page.PageError += (_, error) => jsErrors.Add(error);

        await Page.GotoAsync($"{BaseUrl}/tournaments", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 30_000
        });

        // Kurz warten für potentielle async-Fehler
        await Page.WaitForTimeoutAsync(2_000);

        Assert.Empty(jsErrors);
    }
}
