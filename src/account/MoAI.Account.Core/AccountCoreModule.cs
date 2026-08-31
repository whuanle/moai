using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Account.Services;

namespace MoAI.Account;

/// <summary>
/// AccountCoreModule.
/// </summary>
[InjectModule<AccountSharedModule>]
[InjectModule<AccountApiModule>]
public class AccountCoreModule : IModule
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
        context.Services.AddScoped<IUserAccountService, UserAccountService>();
    }
}
