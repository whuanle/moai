namespace MoAI.Auth.Services;

/// <summary>
/// 公共匿名端点 IP 限流器（Redis 固定窗口，键自动 5 分钟滑动过期）：
/// 防止验证码图片生成与邮件发码被脚本滥用；验证码图片每次请求都计数，邮件发码只计成功发出的次数.
/// </summary>
public interface IPublicAuthRateLimiter
{
    /// <summary>
    /// 命中一次计数：先累加再判断，超过 <paramref name="limit"/> 抛出 429 业务异常（每次请求都计数，适用于低成本高频端点如验证码图片生成）.
    /// </summary>
    /// <param name="bucket">桶名（如 captcha_new）.</param>
    /// <param name="identity">限流维度标识（通常为客户端 IP）.</param>
    /// <param name="limit">窗口内允许次数.</param>
    /// <param name="window">窗口时长.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task HitAsync(string bucket, string identity, int limit, TimeSpan window, CancellationToken cancellationToken);

    /// <summary>
    /// 只读检查是否已超限，超限抛出 429 业务异常（不计数，与 <see cref="CountAsync"/> 搭配实现"只计成功动作"的语义）.
    /// </summary>
    /// <param name="bucket">桶名.</param>
    /// <param name="identity">限流维度标识.</param>
    /// <param name="limit">窗口内允许次数.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task CheckAsync(string bucket, string identity, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// 计数 +1（成功动作后调用），首次计数时落窗口 TTL.
    /// </summary>
    /// <param name="bucket">桶名.</param>
    /// <param name="identity">限流维度标识.</param>
    /// <param name="window">窗口时长（TTL）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task CountAsync(string bucket, string identity, TimeSpan window, CancellationToken cancellationToken);

    /// <summary>
    /// 读取当前计数（不累加），供 after_fail 策略判定（如该 IP 已发码次数）.
    /// </summary>
    /// <param name="bucket">桶名.</param>
    /// <param name="identity">限流维度标识.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>当前计数，无记录为 0.</returns>
    Task<int> GetCountAsync(string bucket, string identity, CancellationToken cancellationToken);
}
