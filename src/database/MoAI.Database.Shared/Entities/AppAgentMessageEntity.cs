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
/// Agent 应用会话消息（对话历史），追加写，按 seq 排序.
/// </summary>
public partial class AppAgentMessageEntity : IFullAudited
{
    /// <summary>
    /// 消息ID.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 所属会话ID，逻辑关联app_agent_session.id.
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// 会话内序号，从 1 递增，决定消息顺序（不依赖时间戳/UUID 排序）.
    /// </summary>
    public int Seq { get; set; }

    /// <summary>
    /// 角色：system|user|assistant|tool（对齐模型协议 role 取值）.
    /// </summary>
    public string Role { get; set; } = default!;

    /// <summary>
    /// 消息正文，空串=无文本（如纯工具调用消息）.
    /// </summary>
    public string Content { get; set; } = default!;

    /// <summary>
    /// 模型一次补全的标识，同一次补全的多条消息共用一个；空串=无.
    /// </summary>
    public string CompletionsId { get; set; } = default!;

    /// <summary>
    /// assistant 请求的工具/插件调用列表，JSON 数组文本，空为 &apos;[]&apos;.
    /// </summary>
    public string ToolCalls { get; set; } = default!;

    /// <summary>
    /// role=tool 时对应的调用ID，回填 tool_calls[].id；空串=不适用.
    /// </summary>
    public string ToolCallId { get; set; } = default!;

    /// <summary>
    /// 模型推理内容（思维链），空串=无.
    /// </summary>
    public string Reasoning { get; set; } = default!;

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
    /// 软删除，0=未删除（legacy bigint 约定）.
    /// </summary>
    public long IsDeleted { get; set; }
}
