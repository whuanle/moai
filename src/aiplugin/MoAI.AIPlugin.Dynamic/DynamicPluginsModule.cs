using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.AIPlugin.Dynamic.Plugins;
using MoAI.Infra;

namespace MoAI.AIPlugin.Dynamic;

/// <summary>
/// 动态插件宿主模块。
/// </summary>
public class DynamicPluginsModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        // 新闻热榜插件：多平台并发拉取，独立超时避免单个平台拖死整轮；
        // 关闭自动 Cookie（抖音渠道手动携带 passport_csrf_token），挂外部请求日志拦截器.
        context.Services.AddHttpClient(HotBoardPlugin.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            UseCookies = false,
        })
        .AddHttpMessageHandler<ExternalHttpMessageHandler>()
        .SetHandlerLifetime(TimeSpan.FromMinutes(5));
    }
}
