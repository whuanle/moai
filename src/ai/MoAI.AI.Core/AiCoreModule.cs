using Maomi;
using MoAI.Infra;

namespace MoAI.AI;

/// <summary>
/// 提供 AI 最核心的抽象功能.
/// </summary>
[InjectModule<AiKitModule>]
public class AiCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        // todo: 依赖反了，需要将 MoAI.AiModel.Shared 换过来
    }
}
