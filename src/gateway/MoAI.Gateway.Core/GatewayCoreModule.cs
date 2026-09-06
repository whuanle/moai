using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Gateway.Protocols;
using MoAI.Gateway.Protocols.Inbound;
using MoAI.Gateway.Protocols.Upstream;
using MoAI.Gateway.Services;

namespace MoAI.Gateway;

/// <summary>
/// GatewayCoreModule.
/// </summary>
[InjectModule<GatewaySharedModule>]
[InjectModule<GatewayApiModule>]
public class GatewayCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        context.Services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromMinutes(10) });

        // 依赖 scoped 的 DatabaseContext，必须与请求同生命周期.
        context.Services.AddScoped<GatewayModelResolver>();
        context.Services.AddScoped<GatewayUsageService>();
        context.Services.AddScoped<GatewayChatCore>();
        context.Services.AddSingleton<UpstreamDispatcher>();
        context.Services.AddSingleton<InboundProtocolRegistry>();
        context.Services.AddSingleton<UpstreamProtocolRegistry>();
    }
}
