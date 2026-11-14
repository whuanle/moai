namespace MoAI.Subsystem.Queries.Responses;

/// <summary>
/// 委托 SSO 委托码签发结果：code 一次性、60 秒有效；回跳地址取子系统注册值（防开放重定向）.
/// </summary>
public class SubsystemSsoAuthorizeCommandResponse
{
    /// <summary>
    /// 一次性委托码.
    /// </summary>
    public string Code { get; init; } = default!;

    /// <summary>
    /// 注册的登录回调地址（子系统侧完整 URL）.
    /// </summary>
    public string CallbackUrl { get; init; } = default!;

    /// <summary>
    /// 子系统名称（供前端展示"即将跳转"）.
    /// </summary>
    public string Name { get; init; } = default!;
}
