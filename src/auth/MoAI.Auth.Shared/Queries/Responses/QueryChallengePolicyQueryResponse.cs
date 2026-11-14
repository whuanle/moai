namespace MoAI.Auth.Queries.Responses;

/// <summary>
/// 当前客户端的验证策略.
/// </summary>
public class QueryChallengePolicyQueryResponse
{
    /// <summary>
    /// 是否需要图形验证码.
    /// </summary>
    public bool CaptchaRequired { get; set; }

    /// <summary>
    /// 是否需要邮箱验证码（仅 login 用途有意义；账号未绑定邮箱或 SMTP 未启用时为 false）.
    /// </summary>
    public bool EmailCodeRequired { get; set; }
}
