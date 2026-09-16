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
/// Agent 应用会话（会话列表），一个会话属于一个应用与一个用户.
/// </summary>
public partial class AppAgentSessionEntity : IFullAudited
{
    /// <summary>
    /// 会话ID.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 所属团队ID，逻辑关联app.team_id.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 所属应用ID，逻辑关联app.id.
    /// </summary>
    public Guid AppId { get; set; }

    /// <summary>
    /// 会话标题，最长100字符，首轮对话后可由模型生成.
    /// </summary>
    public string Title { get; set; } = default!;

    /// <summary>
    /// 发起用户类型，对齐 MoAI.Infra.Models.UserType：0=识别不到，1=外部用户，2=外部应用，3=内部普通用户（当前仅内部用户）.
    /// </summary>
    public int UserType { get; set; }

    /// <summary>
    /// 输入token累计.
    /// </summary>
    public int InputTokens { get; set; }

    /// <summary>
    /// 输出token累计.
    /// </summary>
    public int OutTokens { get; set; }

    /// <summary>
    /// token 总数累计.
    /// </summary>
    public int TotalTokens { get; set; }

    /// <summary>
    /// 最后一条消息时间，会话列表按此倒序.
    /// </summary>
    public DateTimeOffset LastMessageTime { get; set; }

    /// <summary>
    /// 会话归属用户（内部用户为用户ID）.
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
    /// 软删除，0=未删除（legacy bigint 约定）.
    /// </summary>
    public long IsDeleted { get; set; }

    /// <summary>
    /// Agent 会话冷快照（AgentSession 序列化，含上下文压缩索引），Redis 热态失效后恢复.
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// 会话绑定的专家提示词 id（prompt.id），0 表示未绑定.
    /// </summary>
    public int PromptId { get; set; }
}
