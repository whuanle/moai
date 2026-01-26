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
/// 普通应用对话表.
/// </summary>
public partial class AppChatappChatEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// appid.
    /// </summary>
    public Guid AppId { get; set; }

    /// <summary>
    /// 对话标题.
    /// </summary>
    public string Title { get; set; } = default!;

    /// <summary>
    /// 输入token统计.
    /// </summary>
    public int InputTokens { get; set; }

    /// <summary>
    /// 输出token统计.
    /// </summary>
    public int OutTokens { get; set; }

    /// <summary>
    /// 使用的 token 总数.
    /// </summary>
    public int TotalTokens { get; set; }

    /// <summary>
    /// 用户类型.
    /// </summary>
    public int UserType { get; set; }

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
