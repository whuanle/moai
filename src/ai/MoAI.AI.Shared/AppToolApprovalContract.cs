namespace MoAI.AI;

/// <summary>
/// 工具调用人工审批契约：模式/状态/范围常量与 Redis 键格式，
/// 由 AI.Core（闸口等待）与 App 模块（决策接口）共用，键格式变更须两端同步.
/// </summary>
public static class AppToolApprovalContract
{
    /// <summary>
    /// 对话 SSE 请求上携带审批模式的请求头（auto/approval），未携带按 auto.
    /// </summary>
    public const string HeaderName = "X-Moai-Tool-Approval";

    /// <summary>
    /// 自动模式：工具直接执行.
    /// </summary>
    public const string ModeAuto = "auto";

    /// <summary>
    /// 审批模式：重要工具先挂起等待人工批准/拒绝.
    /// </summary>
    public const string ModeApproval = "approval";

    /// <summary>
    /// 等待人工决策的最长时间（秒），超时按拒绝处理并向模型说明.
    /// </summary>
    public const int TimeoutSeconds = 300;

    /// <summary>
    /// 决策轮询间隔（毫秒）.
    /// </summary>
    public const int PollIntervalMilliseconds = 400;

    /// <summary>
    /// 需要审批的工具来源类型：沙箱、各类插件、流程应用、Agent 应用、技能与知识库/知识图谱检索
    /// （知识库/图谱检索一次调用覆盖全部绑定资源，按「全部绑定资源均自动放通才放行」判定）；
    /// 内部工具（长期记忆 memory、子代理 subagent）不拦截.
    /// </summary>
    public static readonly string[] RequireApprovalKinds = ["sandbox", "dynamic", "static", "mcp", "openapi", "workflow", "agentapp", "skill", "wiki", "graph"];

    /// <summary>
    /// 沙箱工具的来源类型（运行代码/Shell/文件读写等）.
    /// </summary>
    public const string KindSandbox = "sandbox";

    /// <summary>
    /// 沙箱工具名统一前缀（sandbox_run_code 等），审批策略开启沙箱自动放行时按前缀识别.
    /// </summary>
    public const string SandboxToolPrefix = "sandbox_";

    /// <summary>
    /// 知识库检索工具名（覆盖全部绑定知识库）.
    /// </summary>
    public const string WikiSearchToolName = "search_knowledge_base";

    /// <summary>
    /// 知识库片段获取工具名（覆盖全部绑定知识库）.
    /// </summary>
    public const string WikiChunkToolName = "get_knowledge_base_chunk";

    /// <summary>
    /// 知识图谱检索工具名（覆盖全部绑定图谱）.
    /// </summary>
    public const string GraphSearchToolName = "search_knowledge_graph";

    /// <summary>
    /// 技能装载工具名前缀（skill_{key}）.
    /// </summary>
    public const string SkillToolPrefix = "skill_";

    /// <summary>
    /// 流程应用工具名前缀（workflow__{应用名}，同名加短 id 后缀）.
    /// </summary>
    public const string WorkflowToolPrefix = "workflow__";

    /// <summary>
    /// Agent 应用工具名前缀（agent__{应用名}，同名加短 id 后缀）.
    /// </summary>
    public const string AgentAppToolPrefix = "agent__";

    /// <summary>
    /// 前端无需展示审批卡的工具名：检索/技能装载已纳入审批范围，当前无整类豁免（保留契约字段供历史前端兼容）.
    /// </summary>
    public static readonly string[] ExemptToolNames = [];

    /// <summary>
    /// 前端无需展示审批卡的工具名前缀：当前无整类豁免（保留契约字段供历史前端兼容）.
    /// </summary>
    public static readonly string[] ExemptToolPrefixes = [];

    /// <summary>
    /// 状态：等待决策.
    /// </summary>
    public const string StatusPending = "pending";

    /// <summary>
    /// 状态：已批准.
    /// </summary>
    public const string StatusApproved = "approved";

    /// <summary>
    /// 状态：已拒绝.
    /// </summary>
    public const string StatusRejected = "rejected";

    /// <summary>
    /// 状态：等待超时.
    /// </summary>
    public const string StatusTimeout = "timeout";

    /// <summary>
    /// 状态：无匹配的待审批记录（已处理、超时或该工具本不需要审批）.
    /// </summary>
    public const string StatusMissing = "missing";

    /// <summary>
    /// 判断是否为合法审批模式.
    /// </summary>
    /// <param name="mode">模式值.</param>
    /// <returns>合法返回 true.</returns>
    public static bool IsValidMode(string? mode) => mode == ModeAuto || mode == ModeApproval;

    /// <summary>
    /// 判断工具来源类型是否需要审批.
    /// </summary>
    /// <param name="kind">工具来源类型.</param>
    /// <returns>需要审批返回 true.</returns>
    public static bool KindRequiresApproval(string? kind)
        => kind != null && System.Array.IndexOf(RequireApprovalKinds, kind) >= 0;

    /// <summary>
    /// 审批记录 Redis 键.
    /// </summary>
    /// <param name="approvalId">审批 id.</param>
    /// <returns>键.</returns>
    public static string RecordKey(Guid approvalId) => $"appagent:toolapproval:{approvalId:N}";

    /// <summary>
    /// 会话待审批索引 Redis 键（hash：field=审批 id，value=工具名），决策接口按会话+工具名定位记录.
    /// </summary>
    /// <param name="sessionId">会话 id.</param>
    /// <returns>键.</returns>
    public static string PendingIndexKey(Guid sessionId) => $"appagent:toolapproval:pending:{sessionId:N}";
}

/// <summary>
/// 工具审批记录（Redis 热态，不落库）：AI.Core 创建并等待，App 模块决策接口改写状态.
/// </summary>
public sealed class AppToolApprovalRecord
{
    /// <summary>
    /// 审批 id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 会话 id.
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// 应用 id.
    /// </summary>
    public Guid AppId { get; set; }

    /// <summary>
    /// 发起对话的用户 id（会话归属人）.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 真实工具名（call_tool 内层 toolName）.
    /// </summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// 工具展示标题.
    /// </summary>
    public string ToolTitle { get; set; } = string.Empty;

    /// <summary>
    /// 工具参数 JSON 文本，供审批卡展示.
    /// </summary>
    public string ArgsJson { get; set; } = string.Empty;

    /// <summary>
    /// 状态：pending/approved/rejected/timeout.
    /// </summary>
    public string Status { get; set; } = AppToolApprovalContract.StatusPending;

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
