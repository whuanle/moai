using Maomi;

namespace MoAI.User;

/// <summary>
/// UserCoreModule.
/// </summary>
[InjectModule<UserApiModule>]
public class UserCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
    }
}
