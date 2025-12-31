using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.AI.HttpMessageHandlers;
using MoAI.Infra;

namespace MoAI.AI;

/// <summary>
/// 提供 AI 最核心的抽象功能.
/// </summary>
[InjectModule<AiSharedModule>]
[InjectModule<AiKitModule>]
public class AiCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        context.Services.AddHttpClient("GoogleAIGemini")
            .AddHttpMessageHandler<GoogleHttpMessageHandler>();

        context.Services.AddHttpClient("ai_stream")
            .AddHttpMessageHandler<AiStreamHttpMessageHandler>();
    }
}
