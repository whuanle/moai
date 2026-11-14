namespace MoAI.Auth.Services;

/// <summary>
/// 登录票据内容.
/// </summary>
/// <param name="UserId">待二次验证的用户 ID.</param>
/// <param name="Factor">二次验证方式，见 <see cref="Models.LoginChallengeFactors"/>.</param>
public sealed record LoginTicketPayload(long UserId, string Factor);

/// <summary>
/// 登录二次验证票据服务：密码校验通过后签发一次性票据，verify 端点凭票据完成二次验证.
/// </summary>
public interface ILoginChallengeService
{
    /// <summary>
    /// 签发一次性登录票据（Redis 存储，5 分钟有效）.
    /// </summary>
    /// <param name="userId">用户 ID.</param>
    /// <param name="factor">二次验证方式，见 <see cref="Models.LoginChallengeFactors"/>.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>票据字符串.</returns>
    Task<string> CreateTicketAsync(long userId, string factor, CancellationToken cancellationToken);

    /// <summary>
    /// 读取票据内容（不消费，用于重发验证码）.
    /// </summary>
    /// <param name="ticket">票据字符串.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>票据内容，不存在或已过期返回 null.</returns>
    Task<LoginTicketPayload?> PeekTicketAsync(string ticket, CancellationToken cancellationToken);

    /// <summary>
    /// 消费票据（一次性：读取后立即删除）.
    /// </summary>
    /// <param name="ticket">票据字符串.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>票据内容，不存在或已过期返回 null.</returns>
    Task<LoginTicketPayload?> ConsumeTicketAsync(string ticket, CancellationToken cancellationToken);
}
