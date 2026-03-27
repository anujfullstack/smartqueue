using Microsoft.Extensions.DependencyInjection;

namespace WaitWise.Agent;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgents(this IServiceCollection services)
    {
        // Agent tool classes registered here in Phase 2
        return services;
    }
}
