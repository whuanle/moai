using Maomi;
using MoAI.AiModel.Api;

namespace MoAI.AiModel.Core;

/// <summary>
/// AiModelCoreModule.
/// </summary>
[InjectModule<AiModelApiModule>]
public class AiModelCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
    }
}