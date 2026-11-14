using System.Threading;
using System.Threading.Tasks;

namespace MoAI.Prompt.Services;

/// <summary>
/// 提示词（专家）使用次数计数器：写入 Redis，由统计模块定期批量落库到 prompts.use_count.
/// </summary>
public interface IPromptUsageCounter
{
    /// <summary>
    /// 按提示词 id 累加一次使用.
    /// </summary>
    /// <param name="promptId">提示词 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task IncrementAsync(int promptId, CancellationToken cancellationToken = default);
}
