using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.WeixinWork.Services;

namespace MoAI.WeixinWork;

/// <summary>
/// WeixinWorkCoreModule.
/// </summary>
[InjectModule<WeixinWorkSharedModule>]
[InjectModule<WeixinWorkApiModule>]
public class WeixinWorkCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        // 企业微信应用消息客户端（access_token 获取与消息发送），服务地址随连接配置动态拼接
        context.Services.AddHttpClient(WeixinWorkApiClient.HttpClientName);
    }
}
