using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Login.Services;

namespace MoAI.Login;

/// <summary>
/// LoginCoreModule.
/// </summary>
[InjectModule<LoginApiModule>]
public class LoginCoreModule : IModule
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
