using Maomi;

namespace MoAI.Modules;

/// <summary>
/// 聚合 API 项目中的各个子模块.
/// </summary>
[InjectModule<ConfigureLoggerModule>]
[InjectModule<ConfigureAuthorizaModule>]
[InjectModule<ConfigureMVCModule>]
[InjectModule<FastEndpointModule>]
[InjectModule<ConfigureMediatRModule>]
public class ApiModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
    }
}
