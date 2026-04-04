using ddpc.DartSuite.Api.Hubs;
using ddpc.DartSuite.ApiClient;
using ddpc.DartSuite.Api.Services;
using ddpc.DartSuite.Infrastructure;
using ddpc.DartSuite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
        policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
});

builder.Services.AddDartSuiteInfrastructure(builder.Configuration);
builder.Services.AddDartSuiteApiClient(builder.Configuration);
builder.Services.AddSingleton<AutodartsSessionStore>();
builder.Services.AddSingleton<AutodartsMatchListenerService>();
builder.Services.AddSingleton<BoardExtensionSyncRequestStore>();
builder.Services.AddScoped<TournamentAuthorizationService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<AutodartsMatchListenerService>());

var app = builder.Build();

if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("EnableSwagger"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Default");
app.MapControllers();
app.MapHub<BoardStatusHub>("/hubs/boards");
app.MapHub<TournamentHub>("/hubs/tournaments");

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<DartSuiteDbContext>();
    await dbContext.Database.MigrateAsync();
    await DartSuiteSeeder.SeedAsync(dbContext);
}

app.Run();
