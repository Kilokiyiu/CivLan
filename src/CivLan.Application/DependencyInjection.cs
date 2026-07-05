using CivLan.Application.Abstractions;
using CivLan.Application.Options;
using CivLan.Application.Services;
using CivLan.Domain.Repositories;
using CivLan.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CivLan.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IVirtualIpAllocator>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<WireGuardOptions>>().Value;
            return new VirtualIpAllocator(options.NetworkPrefix);
        });

        services.AddScoped<RoomAppService>();
        return services;
    }
}
