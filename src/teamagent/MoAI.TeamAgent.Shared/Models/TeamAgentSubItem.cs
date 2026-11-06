using System;
using System.Collections.Generic;

namespace MoAI.TeamAgent.Models;

/// <summary>
/// 团队智能体的子智能体定义：由主智能体在运行时以具名工具派发（Phase 2），本期为管理面定义.
/// </summary>
public class TeamAgentSubItem
{
    /// <summary>
    /// 子智能体标识 key，智能体内唯一，小写字母开头的小写字母/数字/下划线；运行时将作为 <c>subagent_{key}</c> 工具名暴露给主智能体.
    /// </summary>
    public string Key { get; set; } = default!;

    /// <summary>
    /// 子智能体名称（展示名，可中文）.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 子智能体描述：主智能体判断"何时派发它"的唯一依据，请写清职责与适用/不适用场景.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 子智能体系统提示词.
    /// </summary>
    public string Prompt { get; set; } = default!;

    /// <summary>
    /// 插件依赖（团队可用插件 id 列表）；运行时仅注入此处声明的工具，声明即边界.
    /// </summary>
    public IReadOnlyList<Guid> PluginIds { get; set; } = Array.Empty<Guid>();

    /// <summary>
    /// 知识库依赖（本团队知识库 id 列表）；运行时仅注入此处声明的知识库.
    /// </summary>
    public IReadOnlyList<long> WikiIds { get; set; } = Array.Empty<long>();

    /// <summary>
    /// 知识图谱依赖（本团队托管图谱 id 列表）；运行时仅注入此处声明的图谱.
    /// </summary>
    public IReadOnlyList<long> GraphIds { get; set; } = Array.Empty<long>();
}
