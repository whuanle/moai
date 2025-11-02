namespace MoAI.Infra.Models;

/// <summary>
/// 数据子项.
/// </summary>
public abstract class AuditsInfo
{
    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 创建人ID.
    /// </summary>
    public int CreateUserId { get; set; }

    /// <summary>
    /// 创建者 名字.
    /// </summary>
    public string CreateUserName { get; set; } = string.Empty;

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 更新人ID.
    /// </summary>
    public int UpdateUserId { get; set; }

    /// <summary>
    /// 更新人 名字.
    /// </summary>
    public string UpdateUserName { get; set; } = string.Empty;
}
