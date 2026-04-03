using ddpc.DartSuite.Web.Components;
using ddpc.DartSuite.Web.Localization;
using ddpc.DartSuite.Web.Services;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

Console.WriteLine("Api:BaseUrl: " + builder.Configuration["Api:BaseUrl"]);
Console.WriteLine("ENVIRONMENT: " + builder.Environment.EnvironmentName);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddLocalization();
builder.Services.AddScoped<AppStateService>();
builder.Services.AddScoped<TournamentHubService>();
builder.Services.AddHttpClient<DartSuiteApiService>(client =>
{
     client.BaseAddress = new Uri(builder?.Configuration["Api:BaseUrl"] ?? throw new NullReferenceException("Api:BaseUrl configuration is missing"));
});

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
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();


