using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using MoAI.Statistics.Services;

namespace MoAI.Prompt.Services;

/// <summary>
/// 提示词（专家）使用次数计数器默认实现：委托统计模块领域服务推送 Redis.
/// </summary>
public class PromptUsageCounter : IPromptUsageCounter
{
    private readonly IResourceUsageService _resourceUsageService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PromptUsageCounter"/> class.
    /// </summary>
    /// <param name="resourceUsageService">资源使用统计领域服务.</param>
    public PromptUsageCounter(IResourceUsageService resourceUsageService)
    {
        _resourceUsageService = resourceUsageService;
    }

    /// <inheritdoc/>
    public Task IncrementAsync(int promptId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(promptId);

        return _resourceUsageService.RecordAsync(ResourceUsageNames.Prompt, promptId.ToString(CultureInfo.InvariantCulture), 1, cancellationToken);
    }
}
