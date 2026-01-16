using MoAI.Storage.Queries;

namespace MoAI.App.Manager.ExternalApi.Models;

/// <summary>
/// 系统接入信息响应.
/// </summary>
public class QueryExternalAppInfoCommandResponse : IAvatarPath
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

    /// <summary>
    /// 应用密钥.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 是否禁用.
    /// </summary>
    public bool IsDisable { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }
}
