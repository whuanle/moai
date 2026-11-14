using CommunityToolkit.VectorData.PgVector;
using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Account;
using MoAI.Infra;
using MoAI.Statistics.Services;
using MoAI.Storage;
using MoAI.Wiki.Mcp;
using MoAI.Wiki.Services;

namespace MoAI.Wiki;

/// <summary>
/// WikiCoreModule.
/// </summary>
[InjectModule<WikiSharedModule>]
[InjectModule<WikiApiModule>]
[InjectModule<StorageSharedModule>]
[InjectModule<AccountSharedModule>]
public class WikiCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        context.Services.AddScoped<IExternalWikiAuthorizer>(sp => sp.GetRequiredService<ExternalWikiAuthorizer>());
        context.Services.AddSingleton(sp => new PostgresVectorStore(sp.GetRequiredService<SystemOptions>().Database));
        context.Services.AddScoped<IWikiEmbeddingVectorStore, PgVectorWikiEmbeddingVectorStore>();
        context.Services.AddTextExtraction();
        context.Services.AddScoped<IWikiWorkflowProcessor>(sp => sp.GetRequiredService<WikiWorkflowProcessor>());
        context.Services.AddScoped<IWikiUsageCounter, WikiUsageCounter>();
        context.Services.AddScoped<IResourceUsageWriter, WikiUsageCounterActivatorJob>();

        // 知识库 MCP 服务器（/api/teamres/wiki/{wikiId}/mcp）：
        // 无状态模式（不维护 Mcp-Session-Id，每个 JSON-RPC 请求独立走接入 key 鉴权），
        // 端点挂载在 MoAI 宿主 Program.cs 的 MapWikiMcp()
        context.Services.AddMcpServer()
            .WithHttpTransport(options => options.Stateless = true)
            .WithTools<WikiMcpTools>();

        // 检索经验聚类周期清理任务（每日 UTC 0 点删除超期未命中聚类）
        context.Services.AddHostedService<Jobs.WikiRecallClusterCleanupRegistration>();
    }
}
