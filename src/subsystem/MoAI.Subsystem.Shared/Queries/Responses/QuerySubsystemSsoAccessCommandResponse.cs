namespace MoAI.Subsystem.Queries.Responses;

/// <summary>
/// 委托 SSO 进入前预检结果：前端据此决定直接展示错误、先登录或继续签发委托码.
/// </summary>
public class QuerySubsystemSsoAccessCommandResponse
{
    /// <summary>
    /// 依据预检原因码：ok=可继续 / login_required=需先登录 / unavailable=不存在或不可用 /
    /// callback_mismatch=回调地址未注册 / no_grants=未配置任何授权 / not_authorized=当前用户未获授权.
    /// </summary>
    public const string ReasonOk = "ok";

    /// <summary>
    /// 匿名访问且子系统可用：需要先登录后再判定用户授权.
    /// </summary>
    public const string ReasonLoginRequired = "login_required";

    /// <summary>
    /// 子系统不存在、已停用或未开放登录（统一原因码，避免匿名枚举探测）.
    /// </summary>
    public const string ReasonUnavailable = "unavailable";

    /// <summary>
    /// 计划回跳地址不在子系统注册列表内.
    /// </summary>
    public const string ReasonCallbackMismatch = "callback_mismatch";

    /// <summary>
    /// 指定团队范围但未配置任何团队/组织授权，任何人都无法使用.
    /// </summary>
    public const string ReasonNoGrants = "no_grants";

    /// <summary>
    /// 登录用户所在团队与组织均未获授权.
    /// </summary>
    public const string ReasonNotAuthorized = "not_authorized";

    /// <summary>
    /// 当前访问者是否可继续进入子系统（匿名时仅代表系统级可用性）.
    /// </summary>
    public bool Allowed { get; init; }

    /// <summary>
    /// 是否需先登录（匿名访问且子系统可用）.
    /// </summary>
    public bool RequiresLogin { get; init; }

    /// <summary>
    /// 子系统名称（可用时回显，供前端展示）.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 预检原因码，见本类 <c>Reason*</c> 常量.
    /// </summary>
    public string Reason { get; init; } = ReasonUnavailable;
}
