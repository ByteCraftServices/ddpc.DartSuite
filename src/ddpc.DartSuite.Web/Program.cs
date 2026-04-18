using ddpc.DartSuite.Web.Components;
using ddpc.DartSuite.Web.Localization;
using ddpc.DartSuite.Web.Services;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

var httpsRedirectPort = ResolveHttpsRedirectPort(builder.Configuration, builder.Environment);

Console.WriteLine("Api:BaseUrl: " + builder.Configuration["Api:BaseUrl"]);
Console.WriteLine("ENVIRONMENT: " + builder.Environment.EnvironmentName);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddLocalization();
builder.Services.AddScoped<AppStateService>();
builder.Services.AddScoped<BoardHubService>();
builder.Services.AddScoped<TournamentHubService>();
builder.Services.AddSingleton<IUiHelpService, UiHelpService>();
builder.Services.Configure<MatchCardDefaultsOptions>(builder.Configuration.GetSection("MatchCardDefaults"));
builder.Services.AddHttpClient<DartSuiteApiService>(client =>
{
     client.BaseAddress = new Uri(builder?.Configuration["Api:BaseUrl"] ?? throw new NullReferenceException("Api:BaseUrl configuration is missing"));
});

var matchCardDefaultsRaw = builder.Configuration.GetSection("MatchCardDefaults:Scopes").Get<Dictionary<string, MatchCardViewSettings>>();
var matchCardDefaults = matchCardDefaultsRaw?.ToDictionary(
    kvp => kvp.Key.Replace("--", "::", StringComparison.Ordinal),
    kvp => kvp.Value,
    StringComparer.OrdinalIgnoreCase);
MatchCardViewSettings.ConfigureDefaults(matchCardDefaults);

if (httpsRedirectPort.HasValue)
{
    builder.Services.AddHttpsRedirection(options =>
    {
        options.HttpsPort = httpsRedirectPort.Value;
    });
}

var supportedCultures = new[] { "de", "en-US" };
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("de");
    options.SetDefaultCulture("de");
    options.AddSupportedCultures(supportedCultures);
    options.AddSupportedUICultures(supportedCultures);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Test"))
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

var localization = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<RequestLocalizationOptions>>();
app.UseRequestLocalization(localization.Value);

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
if (httpsRedirectPort.HasValue)
{
    app.UseHttpsRedirection();
}

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static int? ResolveHttpsRedirectPort(IConfiguration configuration, IWebHostEnvironment environment)
{
    var configuredPort = configuration.GetValue<int?>("HttpsRedirection:HttpsPort")
        ?? configuration.GetValue<int?>("ASPNETCORE_HTTPS_PORT")
        ?? configuration.GetValue<int?>("HTTPS_PORT");

    if (configuredPort.HasValue)
        return configuredPort;

    // Development profiles expose multiple HTTPS endpoints; force localhost endpoint to avoid ambiguity.
    if (environment.IsDevelopment() || environment.IsEnvironment("Test"))
        return 7144;

    return null;
}


