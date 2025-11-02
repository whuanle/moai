namespace MoAI.Database.Audits;

/// <summary>
/// 创建审计属性.
/// </summary>
public interface ICreationAudited
{
    /// <summary>
    /// 创建人的用户名.
    /// </summary>
    int CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    DateTimeOffset CreateTime { get; set; }
}