using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.Logging;

namespace MoAI.AI.Services;

/// <summary>
/// 上下文压缩贡献者：放在管线最后，对「历史 + RAG + 记忆」后的全量消息做压缩.
/// </summary>
[InjectOnScoped]
public sealed class CompactionContextProviderContributor : IAppContextProviderContributor
{
    private readonly AppCompactionStrategyFactory _strategyFactory;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompactionContextProviderContributor"/> class.
    /// </summary>
    /// <param name="strategyFactory">压缩策略工厂.</param>
    /// <param name="loggerFactory">日志工厂.</param>
    public CompactionContextProviderContributor(AppCompactionStrategyFactory strategyFactory, ILoggerFactory loggerFactory)
    {
        _strategyFactory = strategyFactory;
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc/>
    public int Order => 100;

    /// <inheritdoc/>
    public async Task<AIContextProvider?> CreateAsync(AppAgentBuildContext context, CancellationToken cancellationToken)
    {
        // 压缩阈值按本轮实际对话模型推导（对话端多模型切换后跟随所选模型的上下文窗口）
        var strategy = await _strategyFactory.BuildAsync(context.Config, cancellationToken, context.ChatModelId).ConfigureAwait(false);
        return new CompactionProvider(strategy, "moai:compaction", _loggerFactory);
    }
}
