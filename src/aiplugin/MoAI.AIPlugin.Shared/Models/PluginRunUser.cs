namespace MoAI.AIPlugin.Models;

/// <summary>
/// 插件执行上下文中的当前用户详细信息（由执行引擎按 UserId 查询用户表懒加载）.
/// </summary>
public class PluginRunUser
{
    /// <summary>
    /// 用户 id.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// 用户名.
    /// </summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>
    /// 昵称.
    /// </summary>
    public string NickName { get; init; } = string.Empty;

    /// <summary>
    /// 邮箱.
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// 手机号.
    /// </summary>
    public string Phone { get; init; } = string.Empty;

    /// <summary>
    /// 头像路径.
    /// </summary>
    public string AvatarPath { get; init; } = string.Empty;

    /// <summary>
    /// 是否管理员.
    /// </summary>
    public bool IsAdmin { get; init; }
}
