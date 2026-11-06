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
    /// 默认对话模型 id（uuid，与正式对话同一生效口径：已发布取快照）；未配置模型时为空 Guid.
    /// </summary>
    public Guid DefaultModelId { get; init; }

    /// <summary>
    /// 开放给用户选择的对话模型 id 列表（含默认模型，uuid）；仅默认模型或未配置时为单元素/空列表，
    /// 前端据此决定是否展示模型切换器.
    /// </summary>
    public IReadOnlyList<Guid> ModelIds { get; init; } = Array.Empty<Guid>();

    /// <summary>
    /// 应用绑定的团队智能体切换器选项（与正式对话同一生效口径：已发布取快照内嵌定义，否则实时行）；
    /// 仅 1 个或为空时前端不展示智能体切换器.
    /// </summary>
    public IReadOnlyList<AppTeamAgentOption> TeamAgents { get; init; } = Array.Empty<AppTeamAgentOption>();

    /// <summary>
    /// 默认团队智能体 id（uuid）；空=取列表第一个，前端初始选中项.
    /// </summary>
    public Guid? DefaultTeamAgentId { get; init; }

    /// <summary>
    /// 长期记忆模式：0=跟随应用默认；1=对我关闭；2=仅本应用内；3=跨应用.
    /// </summary>
    public int MemoryMode { get; init; }

    /// <summary>
    /// 应用是否开启长期记忆（未开启时用户无需调整记忆偏好）.
    /// </summary>
    public bool MemoryEnabled { get; init; }

    /// <summary>
    /// 用户全局记忆接入模式：0=默认（应用允许时接入）；1=不接入全局记忆.
    /// </summary>
    public int MemoryGlobalMode { get; init; }

    /// <summary>
    /// 应用是否允许用户接入全局记忆（memory.allowGlobalMemory；false 时前端不展示全局接入开关）.
    /// </summary>
    public bool MemoryGlobalAllowed { get; init; }


    /// <summary>
    /// 用户当前勾选的技能 id 列表（可选技能池的子集）；
    /// 无用户配置行时返回应用默认技能（绑定目录，默认全部启用）.
    /// </summary>
    public IReadOnlyList<Guid> Skills { get; init; } = Array.Empty<Guid>();

    /// <summary>
    /// 应用默认技能目录（管理员配置，按配置顺序），无用户配置行时默认全部启用.
    /// </summary>
    public IReadOnlyList<AppUserSkillOption> DefaultSkills { get; init; } = Array.Empty<AppUserSkillOption>();

    /// <summary>
    /// 可选技能候选池 = 应用默认 ∪ 市场公开 ∪ 本人个人（排除禁用；按 默认→市场→个人 排序去重），
    /// 用户勾选只能落在该池内.
    /// </summary>
    public IReadOnlyList<AppUserSkillOption> SkillOptions { get; init; } = Array.Empty<AppUserSkillOption>();

    /// <summary>
    /// 可选专家候选池 = 应用绑定 ∪ 市场公开 ∪ 本人个人（按 绑定→市场→个人 排序去重），
    /// 不含团队提示词（团队资源经管理员绑定才会进入候选）.
    /// </summary>
    public IReadOnlyList<AppUserPromptOption> PromptOptions { get; init; } = Array.Empty<AppUserPromptOption>();

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
    /// 所属团队 id，0=系统内置/市场公开/个人技能.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 是否已上架市场公开.
    /// </summary>
    public bool IsPublic { get; init; }

    /// <summary>
    /// 使用次数（技能装载工具被模型调用时自增），面板技能卡片热门排序依据.
    /// </summary>
    public int? UseCount { get; init; }
}

/// <summary>
/// 应用设置中展示的可选专家（提示词）项.
/// </summary>
public class AppUserPromptOption
{
    /// <summary>
    /// 提示词 id.
    /// </summary>
    public int PromptId { get; init; }

    /// <summary>
    /// 专家名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 专家描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 头像存储路径（objectKey，空串=未设置）.
    /// </summary>
    public string AvatarPath { get; init; } = string.Empty;

    /// <summary>
    /// 所属团队 id，0=个人提示词.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 是否已上架市场公开.
    /// </summary>
    public bool IsPublic { get; init; }

    /// <summary>
    /// 被选用次数（选用专家时自增），面板专家卡片热门排序依据.
    /// </summary>
    public int UseCount { get; init; }
}
