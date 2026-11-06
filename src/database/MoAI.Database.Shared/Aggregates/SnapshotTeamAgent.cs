using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace MoAI.Database.Aggregates;

/// <summary>
/// 团队智能体内嵌定义快照：发布 Agent 应用时随 <see cref="AppAgentConfigSnapshot.TeamAgentDefs"/> 写入，
/// 正式会话运行时按此解析智能体定义（主提示词/偏好模型/子智能体），不回查 team_agent 表——
/// 发布后编辑或删除团队智能体不影响已发布应用.
/// </summary>
public sealed class SnapshotTeamAgent
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// 团队智能体ID（team_agent.id）.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 智能体标识 key（团队内唯一，小写蛇形，如 plan/build）.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 智能体名称（展示名，可中文）.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 智能体描述.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 头像文件 objectKey，空串=未设置.
    /// </summary>
    public string AvatarObjectKey { get; set; } = string.Empty;

    /// <summary>
    /// 主智能体系统提示词.
    /// </summary>
    public string MainPrompt { get; set; } = string.Empty;

    /// <summary>
    /// 主智能体声明的插件依赖ID列表.
    /// </summary>
    public List<Guid> MainPluginIds { get; set; } = [];

    /// <summary>
    /// 主智能体声明的知识库依赖ID列表.
    /// </summary>
    public List<long> MainWikiIds { get; set; } = [];

    /// <summary>
    /// 主智能体声明的知识图谱依赖ID列表.
    /// </summary>
    public List<long> MainGraphIds { get; set; } = [];

    /// <summary>
    /// 子智能体定义列表（≤20）.
    /// </summary>
    public List<SnapshotTeamAgentSub> Subs { get; set; } = [];

    /// <summary>
    /// 把定义列表序列化为 JSON 文本（写入配置行 team_agent_defs 与发布快照）.
    /// </summary>
    /// <param name="defs">定义列表.</param>
    /// <returns>JSON 文本.</returns>
    public static string SerializeDefs(IReadOnlyList<SnapshotTeamAgent> defs)
    {
        return JsonSerializer.Serialize(defs, SerializerOptions);
    }

    /// <summary>
    /// 解析内嵌定义 JSON；空白或非法 JSON 返回空列表（调用方按未绑定处理）.
    /// </summary>
    /// <param name="json">JSON 文本.</param>
    /// <returns>定义列表.</returns>
    public static IReadOnlyList<SnapshotTeamAgent> TryParseDefs(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<SnapshotTeamAgent>>(json, SerializerOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// 按 id 查找定义.
    /// </summary>
    /// <param name="defs">定义列表.</param>
    /// <param name="id">智能体 id.</param>
    /// <returns>定义或 null.</returns>
    public static SnapshotTeamAgent? FindById(IReadOnlyList<SnapshotTeamAgent> defs, Guid id)
    {
        return defs.FirstOrDefault(x => x.Id == id);
    }
}

/// <summary>
/// 团队智能体的子智能体内嵌定义（结构与 team_agent.subs JSON 项一致）.
/// </summary>
public sealed class SnapshotTeamAgentSub
{
    /// <summary>
    /// 子智能体标识 key（小写蛇形，运行时作为 subagent_{key} 派发工具名）.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 子智能体展示名称（可中文）.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 子智能体描述（主智能体判断何时派发的依据）.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 子智能体系统提示词.
    /// </summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// 声明的插件依赖ID列表（运行时工具白名单：仅注入 ∩ 应用绑定插件）.
    /// </summary>
    public List<Guid> PluginIds { get; set; } = [];

    /// <summary>
    /// 声明的知识库依赖ID列表（运行时工具白名单：仅注入 ∩ 应用绑定知识库）.
    /// </summary>
    public List<long> WikiIds { get; set; } = [];

    /// <summary>
    /// 声明的知识图谱依赖ID列表（运行时工具白名单：仅注入 ∩ 应用绑定图谱）.
    /// </summary>
    public List<long> GraphIds { get; set; } = [];
}
