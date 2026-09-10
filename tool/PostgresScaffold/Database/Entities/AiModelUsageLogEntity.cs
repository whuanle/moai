using System;
using System.Collections.Generic;
using MoAI.Database.Audits;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

/// <summary>
/// 模型使用日志,记录每次请求使用记录.
/// </summary>
public partial class AiModelUsageLogEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 模型id.
    /// </summary>
    public Guid ModelId { get; set; }

    /// <summary>
    /// 本次调用计入额度的团队id，0=个人使用；外部用户通过团队公开应用/知识库调用时也计入该团队.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 实际调用者用户id.
    /// </summary>
    public long UserId { get; set; }

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
    /// 使用来源类型：0=个人会话 1=应用 2=知识库 3=工作流，新增类型依次递增.
    /// </summary>
    public int UseType { get; set; }

    /// <summary>
    /// 使用来源资源id，use_type=1时为应用id、=2时为知识库id、=3时为工作流id；use_type=0时为0.
    /// </summary>
    public int UseResourceId { get; set; }

    /// <summary>
    /// 渠道.
    /// </summary>
    public string Channel { get; set; } = default!;

    /// <summary>
    /// 创建人.
    /// </summary>
    public long CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 更新人.
    /// </summary>
    public long UpdateUserId { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }
}
