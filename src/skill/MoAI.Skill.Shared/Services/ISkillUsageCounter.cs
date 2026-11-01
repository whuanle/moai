using System;
using System.Threading;
using System.Threading.Tasks;

namespace MoAI.Skill.Services;

/// <summary>
/// 技能使用次数计数器：写入 Redis，由统计模块定期批量落库到 skills.use_count.
/// </summary>
public interface ISkillUsageCounter
{
    /// <summary>
    /// 按技能 id 累加一次使用.
    /// </summary>
    /// <param name="skillId">技能 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task IncrementAsync(Guid skillId, CancellationToken cancellationToken = default);
}
