using MoAI.Storage.Queries;

namespace MoAI.App.Manager.ManagerApp.Models;

/// <summary>
/// 应用简单信息响应.
/// </summary>
public class QueryAppSimpleInfoCommandResponse : IAvatarPath
{
    /// <summary>
    /// 应用id.
    /// </summary>
    public Guid AppId { get; set; }

    /// <summary>
    /// 应用名称.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string Avatar { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string AvatarKey { get; set; } = string.Empty;
}
