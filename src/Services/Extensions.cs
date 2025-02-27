using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Services;

public static class Extensions
{
    public static void AddServices(this IServiceCollection services)
    {
        services.AddSingleton<IBlobService, BlobService>(o =>
        {
            var logger = o.GetRequiredService<ILogger<BlobService>>();
            var connectionString = o.GetRequiredService<IConfiguration>()["AzureWebJobsStorage"]!;
            return new BlobService(logger, connectionString);
        });
    }
}