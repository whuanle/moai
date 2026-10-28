using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.DingTalk.Services;

namespace MoAI.DingTalk;

/// <summary>
/// DingTalkCoreModule.
/// </summary>
[InjectModule<DingTalkSharedModule>]
[InjectModule<DingTalkApiModule>]
public class DingTalkCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        // 钉钉机器人会话 webhook 客户端（回复消息）
        context.Services.AddHttpClient(DingTalkApiClient.HttpClientName);

        // 长连接状态以接口暴露，供业务模块消费，避免反向依赖钉钉模块实现
        context.Services.AddSingleton<IDingTalkConnectionStatus>(sp => sp.GetRequiredService<DingTalkConnectionManager>());

        // 启动时为全部启用的钉钉应用建立 Stream 长连接
        context.Services.AddHostedService<DingTalkConnectionHostedService>();
    }
}
