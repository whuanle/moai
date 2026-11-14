using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using MoAI.Subsystem.Client.ApiSdk;

namespace MoAI.Subsystem.Client;

/// <summary>
/// 依赖注入注册。接入方在 <c>Program.cs</c>：
/// <code>
/// builder.Services.AddMoaiSubsysClient(builder.Configuration);
/// // 或代码配置：
/// builder.Services.AddMoaiSubsysClient(o => { o.BaseUrl = "https://moai.example.com"; o.AppId = "..."; o.Secret = "..."; });
/// </code>
/// 之后注入 <see cref="SubsysClient"/> 使用。
/// </summary>
public static class SubsysClientServiceCollectionExtensions
{
    /// <summary>业务请求命名 HttpClient。</summary>
    public const string HttpClientName = "MoAI.Subsystem.Client.Api";

    /// <summary>token 端点命名 HttpClient（与业务管道隔离，避免刷新时互相触发重试）。</summary>
    public const string TokenHttpClientName = "MoAI.Subsystem.Client.Token";

    /// <summary>默认配置节名。</summary>
    public const string ConfigSectionName = "MoaiSubsys";

    /// <summary>从 IConfiguration 注册（读 <see cref="ConfigSectionName"/> 配置节）。</summary>
    /// <param name="services">服务集合。</param>
    /// <param name="configuration">应用配置。</param>
    /// <returns>服务集合（链式）。</returns>
    public static IServiceCollection AddMoaiSubsysClient(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddMoaiSubsysClient(configuration.GetSection(ConfigSectionName));
    }

    /// <summary>从配置节注册。</summary>
    /// <param name="services">服务集合。</param>
    /// <param name="section">包含客户端配置的配置节。</param>
    /// <returns>服务集合（链式）。</returns>
    public static IServiceCollection AddMoaiSubsysClient(this IServiceCollection services, IConfigurationSection section)
    {
        services.AddOptions<MoaiSubsysClientOptions>()
            .Bind(section)
            .Validate(ValidateOptions)
            .ValidateOnStart();
        return AddCore(services);
    }

    /// <summary>以代码注册。</summary>
    /// <param name="services">服务集合。</param>
    /// <param name="configure">配置回调。</param>
    /// <returns>服务集合（链式）。</returns>
    public static IServiceCollection AddMoaiSubsysClient(this IServiceCollection services, Action<MoaiSubsysClientOptions> configure)
    {
        services.AddOptions<MoaiSubsysClientOptions>()
            .Configure(configure)
            .Validate(ValidateOptions)
            .ValidateOnStart();
        return AddCore(services);
    }

    private static IServiceCollection AddCore(IServiceCollection services)
    {
        services.AddHttpClient();

        // 双管道：业务管道挂 401 重试 + 错误转换；token 管道仅挂错误转换（避免与 token 管理器重入）
        services.AddTransient<SubsysErrorHandler>();
        services.AddTransient<SubsysAuthRetryHandler>();
        services.AddHttpClient(HttpClientName, static (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<MoaiSubsysClientOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        }).AddHttpMessageHandler<SubsysAuthRetryHandler>();
        services.AddHttpClient(TokenHttpClientName, static (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<MoaiSubsysClientOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        }).AddHttpMessageHandler<SubsysErrorHandler>();

        // token 管理器：token 客户端延迟到首次取 token 时构建（避免在 HttpClientFactory 建 handler 链时嵌套创建命名客户端）
        services.AddSingleton<ISubsysTokenProvider>(static sp => new SubsysTokenProvider(
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<IOptions<MoaiSubsysClientOptions>>().Value));

        // 业务请求适配器：BaseBearer 认证包装 token 管理器
        services.AddSingleton<IRequestAdapter>(static sp =>
        {
            var options = sp.GetRequiredService<IOptions<MoaiSubsysClientOptions>>().Value;
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var adapter = new HttpClientRequestAdapter(
                new BaseBearerTokenAuthenticationProvider(sp.GetRequiredService<ISubsysTokenProvider>()),
                httpClient: httpClientFactory.CreateClient(HttpClientName));
            adapter.BaseUrl = options.BaseUrl;
            return adapter;
        });
        services.AddSingleton<SubsysClient>();
        return services;
    }

    private static bool ValidateOptions(MoaiSubsysClientOptions options)
    {
        return Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri)
            && string.IsNullOrWhiteSpace(uri.PathAndQuery.Trim('/'))
            && !string.IsNullOrWhiteSpace(options.AppId)
            && (!string.IsNullOrWhiteSpace(options.Secret)
                || (options.IsAppIdDirect && options.AppId.StartsWith("moai-sa-", StringComparison.Ordinal)));
    }
}
