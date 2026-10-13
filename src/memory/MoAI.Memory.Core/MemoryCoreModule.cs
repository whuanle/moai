using CommunityToolkit.VectorData.PgVector;
using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Infra;
using MoAI.Memory.Services;

namespace MoAI.Memory;

/// <summary>
/// MemoryCoreModule.
/// </summary>
[InjectModule<MemorySharedModule>]
[InjectModule<MemoryApiModule>]
public class MemoryCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        context.Services.AddSingleton(sp => new PostgresVectorStore(sp.GetRequiredService<SystemOptions>().Database));
        context.Services.AddScoped<IMemoryVectorStore, PgVectorAgentMemoryVectorStore>();
        context.Services.AddScoped<IMemoryService, MemoryService>();
        context.Services.AddScoped<IMemoryExtractionTrigger, MemoryExtractionTrigger>();
    }
}
