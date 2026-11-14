namespace MoAI.LoginSafety.Models;

/// <summary>
/// 登录事件上下文：登录链路（auth 模块）提交给风控评估的完整环境信息.
/// </summary>
public class LoginEventContext
{
    /// <summary>
    /// 用户 id，用户不存在时为 0.
    /// </summary>
    public long UserId { get; init; }

    /// <summary>
    /// 用户名（登录尝试值）.
    /// </summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>
    /// 用户邮箱（命中中/高风险时给用户发提醒），可空.
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// 是否管理员（admin/root），管理员新环境登录升级处置.
    /// </summary>
    public bool IsAdmin { get; init; }

    /// <summary>
    /// 登录方式.
    /// </summary>
    public LoginType LoginType { get; init; }

    /// <summary>
    /// 是否登录成功.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// 失败原因，成功时为空.
    /// </summary>
    public string FailReason { get; init; } = string.Empty;

    /// <summary>
    /// 来源 IP.
    /// </summary>
    public string Ip { get; init; } = string.Empty;

    /// <summary>
    /// User-Agent.
    /// </summary>
    public string UserAgent { get; init; } = string.Empty;
}
