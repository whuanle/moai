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
        context.Services.AddSingleton<GraphDriverProvider>();
        context.Services.AddScoped<CypherKnowledgeGraphStore>();
        context.Services.AddScoped<IKnowledgeGraphStore>(sp => sp.GetRequiredService<CypherKnowledgeGraphStore>());
        context.Services.AddScoped<IKnowledgeGraphAuthorizer>(sp => sp.GetRequiredService<KnowledgeGraphAuthorizer>());
        context.Services.AddScoped<IExternalKnowledgeGraphAuthorizer>(sp => sp.GetRequiredService<ExternalKnowledgeGraphAuthorizer>());
        context.Services.AddScoped<IKnowledgeGraphIntrospectionCache, KnowledgeGraphIntrospectionCache>();
        context.Services.AddScoped<IKgCypherAccessService>(sp => sp.GetRequiredService<KgCypherAccessService>());
    }
}
