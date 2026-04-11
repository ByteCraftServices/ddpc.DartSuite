using Microsoft.Playwright;

namespace ddpc.DartSuite.E2E.Tests.SmokeTests;

/// <summary>
/// Base class for Playwright E2E smoke tests.
/// Reads DARTSUITE_E2E_BASE_URL from environment (default: https://localhost:7144).
/// All tests are skipped unless the env var DARTSUITE_E2E_ENABLED=1 is set.
/// </summary>
public abstract class SmokeTestBase : IAsyncLifetime
{
    protected static readonly string BaseUrl =
        Environment.GetEnvironmentVariable("DARTSUITE_E2E_BASE_URL") ?? "https://localhost:7144";

    protected static bool E2EEnabled =>
        string.Equals(Environment.GetEnvironmentVariable("DARTSUITE_E2E_ENABLED"), "1", StringComparison.OrdinalIgnoreCase);

    protected IPlaywright Playwright { get; private set; } = null!;
    protected IBrowser Browser { get; private set; } = null!;
    protected IBrowserContext Context { get; private set; } = null!;
    protected IPage Page { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        if (!E2EEnabled) return;

        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = ["--no-sandbox", "--disable-setuid-sandbox"]
        });
        Context = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize { Width = 1280, Height = 900 }
        });
        Page = await Context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        if (!E2EEnabled) return;

        await Context.DisposeAsync();
        await Browser.DisposeAsync();
        Playwright.Dispose();
    }

    /// <summary>
    /// Returns true if E2E tests should run; otherwise the test should be skipped.
    /// Usage: if (!ShouldRun()) return; at start of test method.
    /// </summary>
    protected static bool ShouldRun() => E2EEnabled;
}
