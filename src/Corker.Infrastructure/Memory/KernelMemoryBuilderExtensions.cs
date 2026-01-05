using Corker.Core.Interfaces;
using Corker.Infrastructure.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Corker.Infrastructure.Memory;

public static class KernelMemoryBuilderExtensions
{
    public static IServiceCollection AddCorkerMemory(this IServiceCollection services, string modelPath, string storagePath)
    {
        // For now, register a simple memory service
        services.AddSingleton<IMemoryService>(sp =>
            new SimpleMemoryService(storagePath, sp.GetRequiredService<ILogger<SimpleMemoryService>>()));

        return services;
    }
}
