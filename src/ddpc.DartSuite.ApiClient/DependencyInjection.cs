using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace ddpc.DartSuite.ApiClient;

public static class DependencyInjection
{
    public static IServiceCollection AddDartSuiteApiClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AutodartsOptions>(configuration.GetSection("Autodarts"));
        services.AddHttpClient<IAutodartsClient, AutodartsClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<AutodartsOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(options.ApiBaseUrl))
            {
                client.BaseAddress = new Uri(options.ApiBaseUrl);
            }
        });
        return services;
    }
}