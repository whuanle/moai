using System;
using System.Threading;
using System.Threading.Tasks;
using MoAI.Statistics.Services;

namespace MoAI.Skill.Services;

/// <summary>
/// 技能使用次数计数器默认实现：委托统计模块领域服务推送 Redis.
/// </summary>
public class SkillUsageCounter : ISkillUsageCounter
{
    private readonly IResourceUsageService _resourceUsageService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkillUsageCounter"/> class.
    /// </summary>
    /// <param name="resourceUsageService">资源使用统计领域服务.</param>
    public SkillUsageCounter(IResourceUsageService resourceUsageService)
    {
        _resourceUsageService = resourceUsageService;
    }

    /// <inheritdoc/>
    public Task IncrementAsync(Guid skillId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(skillId, Guid.Empty);

        return _resourceUsageService.RecordAsync(ResourceUsageNames.Skill, skillId.ToString("N"), 1, cancellationToken);
    }
}
