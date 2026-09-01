using Maomi;

namespace MoAI.OauthConnect;

/// <summary>
/// OauthConnectCoreModule.
/// </summary>
[InjectModule<OauthConnectSharedModule>]
[InjectModule<OauthConnectApiModule>]
public class OauthConnectCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
    }
}
