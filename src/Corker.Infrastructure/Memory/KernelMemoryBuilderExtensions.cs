using Corker.Core.Interfaces;
using Corker.Infrastructure.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI; // For ITextEmbeddingGenerator
using Microsoft.KernelMemory.MemoryStorage.DevTools; // For SimpleVectorDbConfig

namespace Corker.Infrastructure.Memory;

public static class KernelMemoryBuilderExtensions
{
    public static IServiceCollection AddCorkerMemory(this IServiceCollection services, string modelPath, string storagePath)
    {
        // Register the Embedding Generator
        services.AddSingleton<ITextEmbeddingGenerator>(sp =>
            new Lfm2EmbeddingGenerator(modelPath, sp.GetRequiredService<ILogger<Lfm2EmbeddingGenerator>>()));

        // Build Kernel Memory
        services.AddSingleton<IKernelMemory>(sp =>
        {
            var embeddingGenerator = sp.GetRequiredService<ITextEmbeddingGenerator>();

            var memoryBuilder = new KernelMemoryBuilder(services)
                .WithCustomEmbeddingGenerator(embeddingGenerator)
                .WithSimpleVectorDb(new SimpleVectorDbConfig { Directory = storagePath });

            return memoryBuilder.Build();
        });

        return services;
    }
}
