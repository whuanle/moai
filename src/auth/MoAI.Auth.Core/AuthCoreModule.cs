using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Auth.Services;

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
        context.Services.AddScoped<IOAuthUserProfileService, OAuthUserProfileService>();
    }
}
