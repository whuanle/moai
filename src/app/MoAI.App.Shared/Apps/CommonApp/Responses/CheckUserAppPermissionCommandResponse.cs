namespace MoAI.App.Apps.CommonApp.Responses;

/// <summary>
/// 检查用户是否有权使用应用的响应.
/// </summary>
public class CheckUserAppPermissionCommandResponse
{
    /// <summary>
    /// 是否有权使用.
    /// </summary>
    public bool HasPermission { get; init; }

    /// <summary>
    /// 应用是否存在.
    /// </summary>
    public bool AppExists { get; init; }

    /// <summary>
    /// 应用是否被禁用.
    /// </summary>
    public bool IsAppDisabled { get; init; }
}
