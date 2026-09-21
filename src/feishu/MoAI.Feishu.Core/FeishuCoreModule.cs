using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Feishu.Services;

namespace MoAI.Feishu;

/// <summary>
/// FeishuCoreModule.
/// </summary>
[InjectModule<FeishuSharedModule>]
[InjectModule<FeishuApiModule>]
public class FeishuCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        // 飞书开放平台 HTTP 客户端（tenant_access_token、发消息等）
        context.Services.AddHttpClient(FeishuApiClient.HttpClientName);

        // 云文档客户端与长连接状态以接口暴露，供知识库外部源等业务模块消费，避免反向依赖飞书模块实现
        context.Services.AddSingleton<IFeishuDriveClient>(sp => sp.GetRequiredService<FeishuDriveClient>());
        context.Services.AddSingleton<IFeishuConnectionStatus>(sp => sp.GetRequiredService<FeishuConnectionManager>());

        // 启动时为全部启用的飞书应用建立长连接
        context.Services.AddHostedService<FeishuConnectionHostedService>();
    }
}
