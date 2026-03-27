using Microsoft.Extensions.DependencyInjection;
using WaitWise.Services.Queues;
using WaitWise.Services.Tickets;

namespace WaitWise.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDomainServices(this IServiceCollection services)
    {
        services.AddScoped<IQueueService, QueueService>();
        services.AddScoped<ITicketService, TicketService>();
        return services;
    }
}
