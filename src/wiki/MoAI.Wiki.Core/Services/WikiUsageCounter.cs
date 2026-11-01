using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using MoAI.Statistics.Services;

namespace MoAI.Wiki.Services;

/// <summary>
/// 知识库使用次数计数器默认实现：委托统计模块领域服务推送 Redis.
/// </summary>
public class WikiUsageCounter : IWikiUsageCounter
{
    private readonly IResourceUsageService _resourceUsageService;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiUsageCounter"/> class.
    /// </summary>
    /// <param name="resourceUsageService">资源使用统计领域服务.</param>
    public WikiUsageCounter(IResourceUsageService resourceUsageService)
    {
        _resourceUsageService = resourceUsageService;
    }

    /// <inheritdoc/>
    public Task IncrementAsync(int wikiId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(wikiId);

        return _resourceUsageService.RecordAsync(ResourceUsageNames.Wiki, wikiId.ToString(CultureInfo.InvariantCulture), 1, cancellationToken);
    }
}
