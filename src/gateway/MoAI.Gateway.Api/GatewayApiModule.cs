using Maomi;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace MoAI.Gateway;

/// <summary>
/// GatewayApiModule.
/// </summary>
public class GatewayApiModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        context.Services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, GatewayApiKeyAuthenticationHandler>(GatewayApiKeyDefaults.AuthenticationScheme, _ => { });
    }
}
