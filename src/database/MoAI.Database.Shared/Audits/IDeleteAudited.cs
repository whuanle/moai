using System.ComponentModel.DataAnnotations;

namespace MoAI.Database.Audits;

/// <summary>
/// 删除审计属性.
/// </summary>
public interface IDeleteAudited : IModificationAudited
{
    /// <summary>
    /// 是否删除.
    /// </summary>
    [Required]
    long IsDeleted { get; set; }
}