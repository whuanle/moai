using Maomi;

namespace MoAI.Auth;

/// <summary>
/// AuthCoreModule.
/// </summary>
[InjectModule<AuthSharedModule>]
[InjectModule<AuthApiModule>]
public class AuthCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
    }
}
