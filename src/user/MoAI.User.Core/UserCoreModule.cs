using Maomi;

namespace MoAI.User;

/// <summary>
/// UserCoreModule.
/// </summary>
[InjectModule<UserSharedModule>]
[InjectModule<UserApiModule>]
public class UserCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
    }
}
