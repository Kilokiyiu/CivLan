using CivLan.Application;
using CivLan.Application.Abstractions;
using CivLan.Application.Options;
using CivLan.Domain.Repositories;
using CivLan.Domain.Services;
using CivLan.Infrastructure.Repositories;
using CivLan.Infrastructure.WireGuard;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CivLan.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IRoomRepository>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<CivLanOptions>>().Value;
            return new JsonRoomRepository(options.DataDirectory);
        });

        services.AddSingleton<IVirtualIpAllocator>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<WireGuardOptions>>().Value;
            return new VirtualIpAllocator(options.NetworkPrefix);
        });

        services.AddSingleton<IWireGuardKeyGenerator, WireGuardKeyGenerator>();
        services.AddSingleton<IAccessTokenGenerator, AccessTokenGenerator>();
        services.AddSingleton<IWireGuardConfigurator, WireGuardConfigurator>();

        services.AddApplication();
        return services;
    }
}
