using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Infra.BoCha;
using MoAI.Infra.DingTalk;
using MoAI.Infra.Doc2x;
using MoAI.Infra.Feishu;
using MoAI.Infra.OAuth;
using MoAI.Infra.Put;
using MoAI.Infra.WeixinWork;
using Refit;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MoAI.Infra;

/// <summary>
/// 外部第三方接口对接.
/// </summary>
public class InfraExternalHttpModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        var settings = new RefitSettings(new SystemTextJsonContentSerializer(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
        }))
        {
            Buffered = true
        };

        context.Services.AddTransient<ExternalHttpMessageHandler>();

        context.Services.AddRefitClient<IOAuthClient>(settings)
            .AddHttpMessageHandler<ExternalHttpMessageHandler>()
            .SetHandlerLifetime(TimeSpan.FromSeconds(30));

        context.Services.AddRefitClient<IOAuthClientAccessToken>(settings).
            AddHttpMessageHandler<ExternalHttpMessageHandler>()
            .SetHandlerLifetime(TimeSpan.FromSeconds(30));

        context.Services.AddRefitClient<IFeishuAuthClient>(settings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://open.feishu.cn"))
            .AddHttpMessageHandler<ExternalHttpMessageHandler>()
            .SetHandlerLifetime(TimeSpan.FromSeconds(30));

        context.Services.AddRefitClient<IFeishuApiClient>(settings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://open.feishu.cn"))
            .AddHttpMessageHandler<ExternalHttpMessageHandler>()
            .SetHandlerLifetime(TimeSpan.FromSeconds(30));

        context.Services.AddRefitClient<IFeishuWebHookClient>(settings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://open.feishu.cn"))
            .AddHttpMessageHandler<ExternalHttpMessageHandler>()
            .SetHandlerLifetime(TimeSpan.FromSeconds(30));

        context.Services.AddRefitClient<IWeixinWorkClient>(settings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://qyapi.weixin.qq.com"))
            .AddHttpMessageHandler<ExternalHttpMessageHandler>()
            .SetHandlerLifetime(TimeSpan.FromSeconds(30));

        context.Services.AddRefitClient<IDingTalkClient>(settings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://api.dingtalk.com"))
            .AddHttpMessageHandler<ExternalHttpMessageHandler>()
            .SetHandlerLifetime(TimeSpan.FromSeconds(30));

        context.Services.AddRefitClient<IDoc2xClient>(settings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://v2.doc2x.noedgeai.com"))
            .AddHttpMessageHandler<ExternalHttpMessageHandler>()
            .SetHandlerLifetime(TimeSpan.FromSeconds(30));

        context.Services.AddRefitClient<IBoChaClient>(settings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://api.bocha.cn"))
            .AddHttpMessageHandler<ExternalHttpMessageHandler>()
            .SetHandlerLifetime(TimeSpan.FromSeconds(30));

        context.Services.AddRefitClient<IPutClient>(settings)
            .AddHttpMessageHandler<ExternalHttpMessageHandler>()
            .SetHandlerLifetime(TimeSpan.FromSeconds(30));
    }
}
