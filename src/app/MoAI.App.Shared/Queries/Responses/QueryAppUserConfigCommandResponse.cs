namespace MoAI.App.Queries.Responses;

/// <summary>
/// 用户级应用配置（含应用默认技能目录）.
/// </summary>
public class QueryAppUserConfigCommandResponse
{
    /// <summary>
    /// 用户当前选择的专家提示词 id，0=未选择；无用户配置行时为 0.
    /// </summary>
    public int PromptId { get; init; }

    /// <summary>
    /// 用户当前勾选的技能 id 列表（应用默认技能的子集）；
    /// 无用户配置行时返回应用默认技能全集（默认全部启用）.
    /// </summary>
    public IReadOnlyList<Guid> Skills { get; init; } = Array.Empty<Guid>();

    /// <summary>
    /// 应用默认使用的团队技能目录（管理员配置，按配置顺序），用户可取消勾选.
    /// </summary>
    public IReadOnlyList<AppUserSkillOption> DefaultSkills { get; init; } = Array.Empty<AppUserSkillOption>();

    /// <summary>
    /// 工具审批模式：auto=自动执行；approval=重要工具调用前需人工批准；无用户配置行时为 auto.
    /// </summary>
    public string ToolApprovalMode { get; init; } = "auto";

    /// <summary>
    /// 无需展示审批卡的工具名（只读检索类），与 <see cref="ToolApprovalExemptPrefixes"/> 供前端渲染审批卡判断.
    /// </summary>
    public IReadOnlyList<string> ToolApprovalExemptNames { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 无需展示审批卡的工具名前缀（技能装载）.
    /// </summary>
    public IReadOnlyList<string> ToolApprovalExemptPrefixes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 审批策略自动放行的工具名（应用配置白名单插件产出的工具，含 MCP/OpenAPI 函数工具）：
    /// 审批模式下后端直接执行，前端不展示审批卡.
    /// </summary>
    public IReadOnlyList<string> ToolApprovalAutoApprovedNames { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 审批策略自动放行的工具名前缀（沙箱工具 sandbox_）：审批模式下后端直接执行，前端不展示审批卡.
    /// </summary>
    public IReadOnlyList<string> ToolApprovalAutoApprovedPrefixes { get; init; } = Array.Empty<string>();
}

/// <summary>
/// 应用设置中展示的默认技能项.
/// </summary>
public class AppUserSkillOption
{
    /// <summary>
    /// 技能 id.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 技能全局唯一标识.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// 技能名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 技能描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 是否平台内置技能.
    /// </summary>
    public bool IsSystem { get; init; }

    /// <summary>
    /// 所属团队 id，0=系统内置或市场公开.
    /// </summary>
    public int TeamId { get; init; }
}
