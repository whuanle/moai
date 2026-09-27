using CommunityToolkit.VectorData.PgVector;
using Maomi;
using Maomi.ToMarkdown;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Infra;
using MoAI.KnowledgeGraph.Mcp;
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
        context.Services.AddTextExtraction();
        context.Services.AddSingleton<GraphDriverProvider>();
        context.Services.AddScoped<CypherKnowledgeGraphStore>();
        context.Services.AddScoped<IKnowledgeGraphStore>(sp => sp.GetRequiredService<CypherKnowledgeGraphStore>());
        context.Services.AddScoped<IKnowledgeGraphAuthorizer>(sp => sp.GetRequiredService<KnowledgeGraphAuthorizer>());
        context.Services.AddScoped<IExternalKnowledgeGraphAuthorizer>(sp => sp.GetRequiredService<ExternalKnowledgeGraphAuthorizer>());
        context.Services.AddScoped<IKnowledgeGraphIntrospectionCache, KnowledgeGraphIntrospectionCache>();
        context.Services.AddScoped<IKgCypherAccessService>(sp => sp.GetRequiredService<KgCypherAccessService>());
        context.Services.AddSingleton(sp => new PostgresVectorStore(sp.GetRequiredService<SystemOptions>().Database));
        context.Services.AddScoped<IKgEmbeddingVectorStore, PgVectorKgEmbeddingVectorStore>();

        // 知识图谱 MCP 服务器（/api/external/knowledge-graph/{kgId}/mcp）：
        // 无状态模式（不维护 Mcp-Session-Id，每个 JSON-RPC 请求独立走接入 key 鉴权），端点挂载在 MoAI 宿主 Program.cs 的 MapKnowledgeGraphMcp()。
        // AddMcpServer 与知识库 MCP 共享全局 McpServerOptions（重复调用幂等合并工具集），两个端点的工具域由 KnowledgeGraphMcpToolGate 按请求路径过滤
        context.Services.AddMcpServer()
            .WithHttpTransport(options => options.Stateless = true)
            .WithTools<KnowledgeGraphMcpTools>()
            .AddListToolsFilter(KnowledgeGraphMcpToolGate.ListToolsFilter)
            .AddCallToolFilter(KnowledgeGraphMcpToolGate.CallToolFilter);
    }
}
