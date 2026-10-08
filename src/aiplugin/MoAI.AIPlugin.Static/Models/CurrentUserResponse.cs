using System.ComponentModel;

namespace MoAI.AIPlugin.Static.Models;

/// <summary>
/// 当前用户信息响应.
/// </summary>
public class CurrentUserResponse
{
    /// <summary>
    /// 执行入口是否注入了上下文（历史调用路径未注入时为 false）.
    /// </summary>
    [Description("执行入口是否注入了上下文")]
    public bool IsContextInjected { get; set; }

    /// <summary>
    /// 调用方是否为已认证用户.
    /// </summary>
    [Description("调用方是否为已认证用户")]
    public bool IsAuthenticated { get; set; }

    /// <summary>
    /// 用户 id，未认证时为 0.
    /// </summary>
    [Description("用户 id，未认证时为 0")]
    public long UserId { get; set; }

    /// <summary>
    /// 用户名，取不到时为空字符串.
    /// </summary>
    [Description("用户名，取不到时为空字符串")]
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// 昵称，取不到时为空字符串.
    /// </summary>
    [Description("昵称，取不到时为空字符串")]
    public string NickName { get; set; } = string.Empty;

    /// <summary>
    /// 邮箱，取不到时为空字符串.
    /// </summary>
    [Description("邮箱，取不到时为空字符串")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// 是否管理员.
    /// </summary>
    [Description("是否管理员")]
    public bool IsAdmin { get; set; }

    /// <summary>
    /// 用户类型（none/external/externalapp/normal）.
    /// </summary>
    [Description("用户类型（none/external/externalapp/normal）")]
    public string UserType { get; set; } = string.Empty;

    /// <summary>
    /// 调用方所在团队 id，非团队维度调用时为 null.
    /// </summary>
    [Description("调用方所在团队 id，非团队维度调用时为 null")]
    public long? TeamId { get; set; }

    /// <summary>
    /// 调用来源（admin/team/agent）.
    /// </summary>
    [Description("调用来源（admin/team/agent）")]
    public string Source { get; set; } = string.Empty;
}
