using CommunityToolkit.VectorData.PgVector;
using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Account;
using MoAI.Hangfire.Services;
using MoAI.Infra;
using MoAI.Storage;
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
        context.Services.AddScoped<ICounterActivatorJob, WikiUsageCounterActivatorJob>();
    }
}
