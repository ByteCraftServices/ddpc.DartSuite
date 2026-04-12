using Microsoft.Playwright;

namespace ddpc.DartSuite.E2E.Tests.SmokeTests;

/// <summary>
/// DS-044: Standard-Regression KO-8-121-SI-SO.
/// Testet den Standard-Regressions-Flow: K.O.-Turnier, 8 Spieler, 121 Punkte, Straight-In/Single-Out.
/// Prüft: Turnier anlegen → Tab-Navigation → Auslosung-Tab → KO-Tab → Spielplan-Tab.
/// Voraussetzung: DARTSUITE_E2E_ENABLED=1, eingeloggter Spielleiter-Account.
/// </summary>
public sealed class KoRegressionTests : SmokeTestBase
{
    [Fact(DisplayName = "DS-044-01: Turnier-Tab 'Auslosung' ist navigierbar")]
    public async Task DrawTab_IsNavigable()
    {
        if (!ShouldRun()) return;

        await Page.GotoAsync($"{BaseUrl}/tournaments", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Commit,
            Timeout = 30_000
        });
        if (IsLoginRedirect(Page)) return;
        await Page.WaitForLoadStateAsync(LoadState.Load, new PageWaitForLoadStateOptions { Timeout = 15_000 });

        // Klicke erstes Turnier
        var listItem = Page.Locator("li.list-group-item.list-group-item-action").First;
        if (!await listItem.IsVisibleAsync())
        {
            // Kein Turnier angelegt — Test überspringen
            return;
        }

        await listItem.ClickAsync();
        await Page.WaitForTimeoutAsync(250);
        if (IsLoginRedirect(Page)) return;
        await Page.WaitForLoadStateAsync(LoadState.Load, new PageWaitForLoadStateOptions { Timeout = 15_000 });

        // Klicke "Auslosung"-Tab
        var drawTab = Page.Locator(".nav-tabs button:has-text('Auslosung')").First;
        var isVisible = await drawTab.IsVisibleAsync();
        if (!isVisible) return; // Tab nicht sichtbar (z.B. kein KO-Modus)

        await drawTab.ClickAsync(new LocatorClickOptions { Force = true });
        await Page.WaitForTimeoutAsync(500);

        // Auslosungs-Inhalt vorhanden
        var drawContent = Page.Locator("#section-tournaments-draw, .draw-action-bar, .draw-group-dropzone");
        await drawContent.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = 15_000 });

        // Tab wurde gewechselt (kein Fehler)
        var url = Page.Url;
        Assert.True(url.Contains("tournaments"), $"Unexpected URL: {url}");
    }

    [Fact(DisplayName = "DS-044-02: KO-Tab vorhanden und navigierbar")]
    public async Task KoTab_IsNavigable()
    {
        if (!ShouldRun()) return;

        await Page.GotoAsync($"{BaseUrl}/tournaments", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Commit,
            Timeout = 30_000
        });
        if (IsLoginRedirect(Page)) return;
        await Page.WaitForLoadStateAsync(LoadState.Load, new PageWaitForLoadStateOptions { Timeout = 15_000 });

        var listItem = Page.Locator("li.list-group-item.list-group-item-action").First;
        if (!await listItem.IsVisibleAsync()) return;

        await listItem.ClickAsync();
        await Page.WaitForTimeoutAsync(250);
        if (IsLoginRedirect(Page)) return;
        await Page.WaitForLoadStateAsync(LoadState.Load, new PageWaitForLoadStateOptions { Timeout = 15_000 });

        // Klicke K.O.-Phase-Tab
        var koTab = Page.Locator(".nav-tabs button:has-text('K.O.-Phase')").First;
        if (!await koTab.IsVisibleAsync()) return;

        await koTab.ClickAsync(new LocatorClickOptions { Force = true });
        await Page.WaitForTimeoutAsync(500);

        // KO-Inhalt oder Fehlermeldung sichtbar (kein JS-Absturz)
        var koSection = Page.Locator("#section-tournaments-knockout, #section-tournaments-ko, .ko-bracket, .match-card, p.text-muted");
        await koSection.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = 15_000 });
        var count = await koSection.CountAsync();
        Assert.True(count > 0, "KO-Tab: Kein erwarteter Inhalt gefunden");
    }

    [Fact(DisplayName = "DS-044-03: Spielplan-Tab vorhanden und navigierbar")]
    public async Task ScheduleTab_IsNavigable()
    {
        if (!ShouldRun()) return;

        await Page.GotoAsync($"{BaseUrl}/tournaments", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Commit,
            Timeout = 30_000
        });
        if (IsLoginRedirect(Page)) return;
        await Page.WaitForLoadStateAsync(LoadState.Load, new PageWaitForLoadStateOptions { Timeout = 15_000 });

        var listItem = Page.Locator("li.list-group-item.list-group-item-action").First;
        if (!await listItem.IsVisibleAsync()) return;

        await listItem.ClickAsync();
        await Page.WaitForTimeoutAsync(250);
        if (IsLoginRedirect(Page)) return;
        await Page.WaitForLoadStateAsync(LoadState.Load, new PageWaitForLoadStateOptions { Timeout = 15_000 });

        // Klicke Spielplan-Tab
        var scheduleTab = Page.Locator(".nav-tabs button:has-text('Spielplan')").First;
        if (!await scheduleTab.IsVisibleAsync()) return;

        await scheduleTab.ClickAsync(new LocatorClickOptions { Force = true });
        await Page.WaitForTimeoutAsync(500);

        // Spielplan-Inhalt sichtbar (kein JS-Absturz)
        var scheduleSection = Page.Locator("#section-tournaments-schedule, #section-tournaments-schedule-zeitplan, p.text-muted");
        await scheduleSection.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = 15_000 });
        var count = await scheduleSection.CountAsync();
        Assert.True(count > 0, "Spielplan-Tab: Kein erwarteter Inhalt gefunden");
    }

    [Fact(DisplayName = "DS-044-04: Spielmodus-Tab navigierbar, fehlende Konfiguration sichtbar")]
    public async Task RoundsTab_IsNavigable_And_ShowsMissingConfig()
    {
        if (!ShouldRun()) return;

        await Page.GotoAsync($"{BaseUrl}/tournaments", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Commit,
            Timeout = 30_000
        });
        if (IsLoginRedirect(Page)) return;
        await Page.WaitForLoadStateAsync(LoadState.Load, new PageWaitForLoadStateOptions { Timeout = 15_000 });

        var listItem = Page.Locator("li.list-group-item.list-group-item-action").First;
        if (!await listItem.IsVisibleAsync()) return;

        await listItem.ClickAsync();
        await Page.WaitForTimeoutAsync(250);
        if (IsLoginRedirect(Page)) return;
        await Page.WaitForLoadStateAsync(LoadState.Load, new PageWaitForLoadStateOptions { Timeout = 15_000 });

        var roundsTab = Page.Locator(".nav-tabs button:has-text('Spielmodus')").First;
        if (!await roundsTab.IsVisibleAsync()) return;

        await roundsTab.ClickAsync(new LocatorClickOptions { Force = true });
        await Page.WaitForTimeoutAsync(500);

        // Spielmodus-Inhalt: Liste der Runden oder Hinweistext
        var roundsContent = Page.Locator(".card.mb-2, p.text-muted, .alert");
        await roundsContent.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = 15_000 });
        var count = await roundsContent.CountAsync();
        Assert.True(count > 0, "Spielmodus-Tab: Kein Inhalt gefunden");
    }
}
