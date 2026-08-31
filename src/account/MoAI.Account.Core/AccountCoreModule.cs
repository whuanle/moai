using Maomi;

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
    }
}
