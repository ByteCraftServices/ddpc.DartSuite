using ddpc.DartSuite.Application.Abstractions;
using ddpc.DartSuite.Domain.Services;
using ddpc.DartSuite.Infrastructure.Configuration;
using ddpc.DartSuite.Infrastructure.Persistence;
using ddpc.DartSuite.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ddpc.DartSuite.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDartSuiteInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(DatabaseOptions.SectionName);
        var options = new DatabaseOptions
        {
            Provider = section["Provider"] ?? "InMemory",
            ConnectionString = section["ConnectionString"] ?? "Data Source=dartsuite.db"
        };

        services.AddDbContext<DartSuiteDbContext>(dbOptions =>
        {
            if (string.Equals(options.Provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                dbOptions.UseSqlite(options.ConnectionString);
            }
            else
            {
                dbOptions.UseInMemoryDatabase("DartSuiteDb");
            }
        });

        services.AddScoped<IMatchPredictionService, MatchPredictionService>();
        services.AddScoped<IBoardManagementService, BoardManagementService>();
        services.AddScoped<ITournamentManagementService, TournamentManagementService>();
        services.AddScoped<IMatchManagementService, MatchManagementService>();

        return services;
    }
}