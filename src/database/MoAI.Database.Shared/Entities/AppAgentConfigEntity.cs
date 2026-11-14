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
/// Agent 应用配置，与 app 一一对应（app_type=0）.
/// </summary>
public partial class AppAgentConfigEntity : IFullAudited
{
    /// <summary>
    /// 配置ID.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 所属团队ID，逻辑关联app.team_id，冗余用于团队维度过滤.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 所属应用ID，逻辑关联app.id（1:1，不建物理外键）.
    /// </summary>
    public Guid AppId { get; set; }

    /// <summary>
    /// 系统提示词，最长4000字符.
    /// </summary>
    public string Prompt { get; set; } = default!;

    /// <summary>
    /// 默认对话模型ID，逻辑关联ai_model.id（uuid）；必须属于模型可选列表.
    /// </summary>
    public Guid ModelId { get; set; }

    /// <summary>
    /// 开放给用户选择的对话模型ID列表（含默认模型），JSON uuid 字符串数组文本，如 &apos;[&quot;...&quot;]&apos;；
    /// 空为&apos;[]&apos;，运行时视为仅默认模型.
    /// </summary>
    public string ModelIds { get; set; } = default!;

    /// <summary>
    /// 绑定的知识库ID列表，JSON 数组文本，元素为 wiki.id（整数），如 &apos;[1,2]&apos;.
    /// </summary>
    public string WikiIds { get; set; } = default!;

    /// <summary>
    /// 绑定的插件ID列表，JSON 数组文本，元素为 plugin.id（uuid 字符串），如 &apos;[&quot;...&quot;]&apos;.
    /// </summary>
    public string Plugins { get; set; } = default!;

    /// <summary>
    /// 对话影响参数，JSON 对象文本（temperature/topP/maxTokens 等），空为 &apos;{}&apos;.
    /// </summary>
    public string ExecutionSettings { get; set; } = default!;

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

    /// <summary>
    /// 绑定的技能ID列表，JSON 数组文本，元素为 skill.id（uuid 字符串），如 [&quot;...&quot;].
    /// </summary>
    public string Skills { get; set; } = default!;

    /// <summary>
    /// 对话开场白，最长4000字符，空为&apos;&apos;.
    /// </summary>
    public string OpeningStatement { get; set; } = default!;

    /// <summary>
    /// 是否启用对话开场白.
    /// </summary>
    public bool OpeningStatementEnabled { get; set; }

    /// <summary>
    /// 开放给用户选择的可选专家提示词ID列表，JSON int 数组文本，元素为 prompt.id，如 [1,2].
    /// </summary>
    public string Prompts { get; set; } = default!;

    /// <summary>
    /// 发布配置快照 JSON（camelCase：prompt/modelId/wikiIds/graphIds/plugins/workflowApps/agentApps/skills/executionSettings/openingStatement/openingStatementEnabled/quickInputs），发布应用时写入，正式会话按此快照执行；null=从未发布.
    /// </summary>
    public string? PublishedConfig { get; set; }

    /// <summary>
    /// 配置状态，0=草稿有未发布变更 1=当前草稿与已发布一致.
    /// </summary>
    public short Status { get; set; }

    /// <summary>
    /// 快捷输入列表，JSON 数组文本，元素为字符串（管理员配置，用户在对话欢迎态点击即发送），空为&apos;[]&apos;.
    /// </summary>
    public string QuickInputs { get; set; } = default!;

    /// <summary>
    /// 绑定的流程应用ID列表（作为工具使用），JSON 数组文本，元素为 app.id（uuid 字符串，须为本团队已发布流程应用），空为&apos;[]&apos;.
    /// </summary>
    public string WorkflowApps { get; set; } = default!;

    /// <summary>
    /// 绑定的 Agent 应用ID列表（作为工具使用），JSON 数组文本，元素为 app.id（uuid 字符串，须为本团队已发布内部 Agent 应用且不含自身），空为&apos;[]&apos;.
    /// </summary>
    public string AgentApps { get; set; } = default!;

    /// <summary>
    /// 绑定的知识图谱ID列表，JSON 数组文本，元素为 knowledge_graph.id（整数），如 &apos;[1,2]&apos;.
    /// </summary>
    public string GraphIds { get; set; } = default!;

    /// <summary>
    /// 绑定的团队智能体ID列表，JSON 数组文本，元素为 team_agent.id（uuid 字符串），须为本团队已启用智能体，空为&apos;[]&apos;.
    /// </summary>
    public string TeamAgentIds { get; set; } = default!;

    /// <summary>
    /// 默认团队智能体ID，逻辑关联 team_agent.id（uuid）；空=取列表第一个；列表为空时忽略.
    /// </summary>
    public Guid? DefaultTeamAgentId { get; set; }

    /// <summary>
    /// 团队智能体内嵌定义 JSON（SnapshotTeamAgent 数组），发布应用时随快照写入，草稿行恒为&apos;&apos;；
    /// 已发布会话运行时按此解析智能体定义（发布后改团队智能体不影响线上）.
    /// </summary>
    public string TeamAgentDefs { get; set; } = default!;
}
