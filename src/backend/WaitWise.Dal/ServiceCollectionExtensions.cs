using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WaitWise.Dal.Repositories;

namespace WaitWise.Dal;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataAccess(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<WaitWiseDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));

        services.AddScoped<ILocationRepository, LocationRepository>();
        services.AddScoped<IQueueRepository, QueueRepository>();
        services.AddScoped<IQueueTicketRepository, QueueTicketRepository>();

        return services;
    }
}
