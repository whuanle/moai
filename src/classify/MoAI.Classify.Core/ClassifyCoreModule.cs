using Maomi;

namespace MoAI.Classify;

/// <summary>
/// Classify 核心层模块.
/// </summary>
[InjectModule<ClassifySharedModule>]
[InjectModule<ClassifyApiModule>]
public class ClassifyCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
    }
}
