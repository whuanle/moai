using System;
using System.Collections.Generic;
using MoAI.Database.Audits;

namespace MoAI.Database.Entities;

/// <summary>
/// 提示词.
/// </summary>
public partial class PromptEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 提示词内容.
    /// </summary>
    public string Content { get; set; } = default!;

    /// <summary>
    /// 分类id.
    /// </summary>
    public int PromptClassId { get; set; }

    public bool IsPublic { get; set; }

    /// <summary>
    /// 创建人.
    /// </summary>
    public int CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 更新人.
    /// </summary>
    public int UpdateUserId { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }
}
