using Maomi;

namespace MoAI.Message;

/// <summary>
/// Message 核心层模块.
/// </summary>
[InjectModule<MessageSharedModule>]
[InjectModule<MessageApiModule>]
public class MessageCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
    }
}
