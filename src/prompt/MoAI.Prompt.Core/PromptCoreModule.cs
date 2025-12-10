using Maomi;
using MoAI.Prompt;

namespace MoAIPrompt.Core;

/// <summary>
/// PromptCoreModule.
/// </summary>
[InjectModule<PromptSharedModule>]
[InjectModule<PromptApiModule>]
public class PromptCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
    }
}
