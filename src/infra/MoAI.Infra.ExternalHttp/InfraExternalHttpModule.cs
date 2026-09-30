using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Infra.Alertmanager;
using MoAI.Infra.BoCha;
using MoAI.Infra.ClickHouse;
using MoAI.Infra.ClickStack;
using MoAI.Infra.DingTalk;
using MoAI.Infra.Doc2x;
using MoAI.Infra.Elasticsearch;
using MoAI.Infra.Feishu;
using MoAI.Infra.Grafana;
using MoAI.Infra.Kubernetes;
using MoAI.Infra.Loki;
using MoAI.Infra.MojiWeather;
using MoAI.Infra.OAuth;
using MoAI.Infra.Paddleocr;
using MoAI.Infra.Prometheus;
using MoAI.Infra.Put;
using MoAI.Infra.Tempo;
using MoAI.Infra.WeixinWork;
using MoAI.Infra.Zabbix;
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
        context.Services.AddSingleton(settings);

        // OAuth 发现端点、令牌端点、用户信息端点地址是动态的，无法使用固定的 BaseAddress 注册，
        // 因此通过工厂在调用时根据实际地址动态创建客户端.
        context.Services.AddTransient<IOAuthClientFactory, OAuthClientFactory>();
        context.Services.AddHttpClient(OAuthClientFactory.HttpClientName)
            .AddHttpMessageHandler<ExternalHttpMessageHandler>()
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

        // 博查服务地址允许被配置覆盖（默认官方地址）：便于本地联调、指向代理或在 E2E 中指向桩服务.
        var boChaEndpoint = context.Configuration["MoAI:BoCha:Endpoint"];
        context.Services.AddRefitClient<IBoChaClient>(settings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(string.IsNullOrWhiteSpace(boChaEndpoint) ? "https://api.bocha.cn" : boChaEndpoint))
            .AddHttpMessageHandler<ExternalHttpMessageHandler>()
            .SetHandlerLifetime(TimeSpan.FromSeconds(30));

        // 墨迹天气服务地址允许被配置覆盖（默认阿里云云市场网关）：便于本地联调、指向代理或在 E2E 中指向桩服务.
        var mojiWeatherEndpoint = context.Configuration["MoAI:MojiWeather:Endpoint"];
        context.Services.AddRefitClient<IMojiWeatherClient>(settings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(string.IsNullOrWhiteSpace(mojiWeatherEndpoint) ? "https://moji.market.alicloudapi.com" : mojiWeatherEndpoint))
            .AddHttpMessageHandler<ExternalHttpMessageHandler>()
            .SetHandlerLifetime(TimeSpan.FromSeconds(30));

        context.Services.AddRefitClient<IPutClient>(settings)
            .AddHttpMessageHandler<ExternalHttpMessageHandler>()
            .SetHandlerLifetime(TimeSpan.FromSeconds(60));

        context.Services.AddRefitClient<IPaddleocrClient>(settings)
            .AddHttpMessageHandler<ExternalHttpMessageHandler>()
            .SetHandlerLifetime(TimeSpan.FromSeconds(60));

        // 智能运维观测服务（Prometheus / Elasticsearch / ClickHouse / Grafana Tempo）：地址来自动态插件实例配置，
        // 每次执行前由插件设置 BaseAddress，故不注册固定 BaseAddress.
        context.Services.AddRefitClient<IPrometheusClient>(settings)
            .AddHttpMessageHandler<ExternalHttpMessageHandler>()
            .SetHandlerLifetime(TimeSpan.FromSeconds(60));

        context.Services.AddRefitClient<IElasticsearchClient>(settings)
            .AddHttpMessageHandler<ExternalHttpMessageHandler>()
            .SetHandlerLifetime(TimeSpan.FromSeconds(60));

        context.Services.AddRefitClient<IClickHouseClient>(settings)
            .AddHttpMessageHandler<ExternalHttpMessageHandler>()
            .SetHandlerLifetime(TimeSpan.FromSeconds(60));

        context.Services.AddRefitClient<ITempoClient>(settings)
            .AddHttpMessageHandler<ExternalHttpMessageHandler>()
            .SetHandlerLifetime(TimeSpan.FromSeconds(60));

        context.Services.AddHttpClient(ZabbixRpcClient.HttpClientName)
            .AddHttpMessageHandler<ExternalHttpMessageHandler>()
            .SetHandlerLifetime(TimeSpan.FromSeconds(60));

        context.Services.AddTransient<IZabbixClient, ZabbixRpcClient>();

        context.Services.AddHttpClient(GrafanaClient.HttpClientName)
            .AddHttpMessageHandler<ExternalHttpMessageHandler>()
            .SetHandlerLifetime(TimeSpan.FromSeconds(60));

        context.Services.AddTransient<IGrafanaClient, GrafanaClient>();

        // 钉钉/企业微信群机器人服务地址允许被配置覆盖（默认官方地址）：便于本地联调、指向代理或在 E2E 中指向桩服务.
        var dingTalkRobotEndpoint = context.Configuration["MoAI:DingTalk:RobotEndpoint"];
        context.Services.AddRefitClient<IDingTalkRobotClient>(settings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(string.IsNullOrWhiteSpace(dingTalkRobotEndpoint) ? "https://oapi.dingtalk.com" : dingTalkRobotEndpoint))
            .AddHttpMessageHandler<ExternalHttpMessageHandler>()
            .SetHandlerLifetime(TimeSpan.FromSeconds(30));

        var weixinWorkRobotEndpoint = context.Configuration["MoAI:WeixinWork:RobotEndpoint"];
        context.Services.AddRefitClient<IWeixinWorkRobotClient>(settings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(string.IsNullOrWhiteSpace(weixinWorkRobotEndpoint) ? "https://qyapi.weixin.qq.com" : weixinWorkRobotEndpoint))
            .AddHttpMessageHandler<ExternalHttpMessageHandler>()
            .SetHandlerLifetime(TimeSpan.FromSeconds(30));

        // 智能运维观测/告警服务（Alertmanager / Loki / Kubernetes）：地址来自动态插件实例配置，
        // 且需要支持子路径部署，走 IHttpClientFactory 具名客户端由插件手工拼端点.
        context.Services.AddHttpClient(AlertmanagerClient.HttpClientName)
            .AddHttpMessageHandler<ExternalHttpMessageHandler>()
            .SetHandlerLifetime(TimeSpan.FromSeconds(60));

        context.Services.AddTransient<IAlertmanagerClient, AlertmanagerClient>();

        context.Services.AddHttpClient(LokiClient.HttpClientName)
            .AddHttpMessageHandler<ExternalHttpMessageHandler>()
            .SetHandlerLifetime(TimeSpan.FromSeconds(60));

        context.Services.AddTransient<ILokiClient, LokiClient>();

        context.Services.AddHttpClient(KubernetesClient.HttpClientName)
            .AddHttpMessageHandler<ExternalHttpMessageHandler>()
            .SetHandlerLifetime(TimeSpan.FromSeconds(60));

        context.Services.AddTransient<IKubernetesClient, KubernetesClient>();

        context.Services.AddHttpClient(ClickStackApiClient.HttpClientName)
            .AddHttpMessageHandler<ExternalHttpMessageHandler>()
            .SetHandlerLifetime(TimeSpan.FromSeconds(60));

        context.Services.AddTransient<IClickStackClient, ClickStackApiClient>();
    }
}
