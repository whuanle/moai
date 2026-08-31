using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
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
        context.Services.AddScoped<IUserContextProvider, UserContextProvider>();
        context.Services.AddScoped<UserContext>(s =>
        {
            return s.GetRequiredService<IUserContextProvider>().GetUserContext();
        });

        context.Services.AddScoped<CustomAuthorizaMiddleware>();
    }
}
