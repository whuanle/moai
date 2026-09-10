using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph;

/// <summary>
/// KnowledgeGraphCoreModule.
/// </summary>
[InjectModule<KnowledgeGraphSharedModule>]
[InjectModule<KnowledgeGraphApiModule>]
public class KnowledgeGraphCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        context.Services.AddSingleton<Neo4jDriverProvider>();
        context.Services.AddScoped<Neo4jKnowledgeGraphStore>();
        context.Services.AddScoped<IKnowledgeGraphStore>(sp => sp.GetRequiredService<Neo4jKnowledgeGraphStore>());
        context.Services.AddScoped<IKnowledgeGraphAuthorizer>(sp => sp.GetRequiredService<KnowledgeGraphAuthorizer>());
    }
}
