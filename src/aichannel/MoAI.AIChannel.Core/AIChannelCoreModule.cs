using System;
using System.Net.Http;
using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.AIChannel.Services;
using MoAI.Hangfire.Services;

namespace MoAI.AIChannel;

/// <summary>
/// AIChannelCoreModule.
/// </summary>
[InjectModule<AIChannelSharedModule>]
[InjectModule<AIChannelApiModule>]
public class AIChannelCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        context.Services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(30) });
        context.Services.AddSingleton<AIModelCatalogService>();
        context.Services.AddSingleton<AIModelProviderService>();
        context.Services.AddSingleton<AIClientProvider>();
        context.Services.AddSingleton<IEmbeddingGeneratorProvider>(sp => sp.GetRequiredService<AIClientProvider>());
        context.Services.AddSingleton<IChatClientProvider>(sp => sp.GetRequiredService<AIClientProvider>());
        context.Services.AddSingleton<IAiChatCompletionService, AIChatCompletionService>();
        context.Services.AddScoped<IAiModelUsageCounter, AiModelUsageCounter>();
        context.Services.AddScoped<ICounterActivatorJob, AiModelUsageCounterActivatorJob>();
    }
}
