namespace MoAI.Database.Audits;

/// <summary>
/// 全部审计属性.
/// </summary>
public class FullAudited : IFullAudited
{
    /// <inheritdoc/>
    public virtual long IsDeleted { get; set; }

    /// <inheritdoc/>
    public virtual DateTimeOffset CreateTime { get; set; }

    /// <inheritdoc/>
    public virtual DateTimeOffset UpdateTime { get; set; }

    /// <inheritdoc/>
    public virtual long CreateUserId { get; set; }

    /// <inheritdoc/>
    public virtual long UpdateUserId { get; set; }
}