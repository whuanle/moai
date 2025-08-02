using System;
using System.Collections.Generic;
using MoAI.Database.Audits;

namespace MoAI.Database.Entities;

/// <summary>
/// 模型使用日志,记录每次请求使用记录.
/// </summary>
public partial class AiModelUseageLogEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 模型id.
    /// </summary>
    public int ModelId { get; set; }

    /// <summary>
    /// 用户id.
    /// </summary>
    public int UseriId { get; set; }

    /// <summary>
    /// 完成数量.
    /// </summary>
    public int CompletionTokens { get; set; }

    /// <summary>
    /// 输入数量.
    /// </summary>
    public int PromptTokens { get; set; }

    /// <summary>
    /// 总数量.
    /// </summary>
    public int TotalTokens { get; set; }

    /// <summary>
    /// 渠道.
    /// </summary>
    public string Channel { get; set; } = default!;

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
