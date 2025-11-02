namespace MoAI.Database.Audits;

/// <summary>
/// 修改审计属性.
/// </summary>
public interface IModificationAudited
{
    /// <summary>
    /// 修改人.
    /// </summary>
    int UpdateUserId { get; set; }

    /// <summary>
    /// 修改时间.
    /// </summary>
    DateTimeOffset UpdateTime { get; set; }
}