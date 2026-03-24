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
builder.Services.AddHostedService(sp => sp.GetRequiredService<AutodartsMatchListenerService>());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Default");
app.MapControllers();
app.MapHub<BoardStatusHub>("/hubs/boards");

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<DartSuiteDbContext>();
    await dbContext.Database.EnsureCreatedAsync();

    // Add HomeSets/AwaySets columns if missing (EnsureCreated doesn't alter existing tables)
    var conn = dbContext.Database.GetDbConnection();
    await conn.OpenAsync();
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = "PRAGMA table_info(Matches)";
    var columns = new HashSet<string>();
    await using (var reader = await cmd.ExecuteReaderAsync())
    {
        while (await reader.ReadAsync())
            columns.Add(reader.GetString(1));
    }
    if (!columns.Contains("HomeSets"))
    {
        await using var alter = conn.CreateCommand();
        alter.CommandText = "ALTER TABLE Matches ADD COLUMN HomeSets INTEGER NOT NULL DEFAULT 0";
        await alter.ExecuteNonQueryAsync();
    }
    if (!columns.Contains("AwaySets"))
    {
        await using var alter = conn.CreateCommand();
        alter.CommandText = "ALTER TABLE Matches ADD COLUMN AwaySets INTEGER NOT NULL DEFAULT 0";
        await alter.ExecuteNonQueryAsync();
    }

    // Add Status column to Tournaments if missing
    await using var cmdTournaments = conn.CreateCommand();
    cmdTournaments.CommandText = "PRAGMA table_info(Tournaments)";
    var tournamentColumns = new HashSet<string>();
    await using (var reader = await cmdTournaments.ExecuteReaderAsync())
    {
        while (await reader.ReadAsync())
            tournamentColumns.Add(reader.GetString(1));
    }
    if (!tournamentColumns.Contains("Status"))
    {
        await using var alter = conn.CreateCommand();
        alter.CommandText = "ALTER TABLE Tournaments ADD COLUMN Status INTEGER NOT NULL DEFAULT 0";
        await alter.ExecuteNonQueryAsync();
    }

    await DartSuiteSeeder.SeedAsync(dbContext);
}

app.Run();
