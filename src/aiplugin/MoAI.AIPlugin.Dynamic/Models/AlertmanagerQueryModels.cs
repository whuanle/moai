using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Alertmanager 查询插件配置（每个实例独立保存）.
/// </summary>
public class AlertmanagerQueryConfig
{
    /// <summary>
    /// Alertmanager 服务地址.
    /// </summary>
    [Description("Alertmanager 服务地址，例如 http://alertmanager:9093；支持子路径部署，如 http://host/alertmanager")]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Basic 认证用户名（可选）.
    /// </summary>
    [Description("Basic 认证用户名（可选）；用户名密码与 BearerToken 都未填则匿名")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Basic 认证密码（可选）.
    /// </summary>
    [Description("Basic 认证密码（可选）")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Bearer Token（可选），已填时优先于用户名密码.
    /// </summary>
    [Description("Bearer Token（可选）；已填时优先于用户名密码")]
    public string BearerToken { get; set; } = string.Empty;

    /// <summary>
    /// 单次请求超时秒数（1-300）.
    /// </summary>
    [Description("单次请求超时秒数，取值 1-300（默认 30）")]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 列表最多返回条数（1-500）.
    /// </summary>
    [Description("alerts/silences 等列表最多返回条数，取值 1-500（默认 100）")]
    public int MaxListItems { get; set; } = 100;
}

/// <summary>
/// Alertmanager 查询插件请求参数.
/// </summary>
public class AlertmanagerQueryRequest
{
    /// <summary>
    /// 操作模式.
    /// </summary>
    [Description("操作模式：alerts=当前告警（默认）/ silences=静默列表 / status=服务与集群状态；未知值会被拒绝")]
    public string Mode { get; set; } = "alerts";

    /// <summary>
    /// alerts 模式：状态过滤.
    /// </summary>
    [Description("alerts 模式：状态过滤 active/suppressed/unprocessed（可空=全部，逗号分隔多个）")]
    public string? State { get; set; }

    /// <summary>
    /// silences 模式：状态过滤.
    /// </summary>
    [Description("silences 模式：状态过滤 pending/active/expired（可空=全部，逗号分隔多个）")]
    public string? SilencesState { get; set; }
}

/// <summary>
/// Alertmanager 查询插件响应.
/// </summary>
public class AlertmanagerQueryResponse
{
    /// <summary>
    /// 结果类型：alerts / silences / status.
    /// </summary>
    public string ResultType { get; set; } = string.Empty;

    /// <summary>
    /// alerts 模式：当前告警列表.
    /// </summary>
    public IReadOnlyList<AlertmanagerAlert> Alerts { get; set; } = new List<AlertmanagerAlert>();

    /// <summary>
    /// silences 模式：静默列表.
    /// </summary>
    public IReadOnlyList<AlertmanagerSilence> Silences { get; set; } = new List<AlertmanagerSilence>();

    /// <summary>
    /// status 模式：版本与集群状态.
    /// </summary>
    public AlertmanagerStatus? Status { get; set; }

    /// <summary>
    /// 列表是否因超过 MaxListItems 被截断.
    /// </summary>
    public bool Truncated { get; set; }
}

/// <summary>
/// Alertmanager 当前告警.
/// </summary>
public class AlertmanagerAlert
{
    /// <summary>
    /// 告警标签.
    /// </summary>
    public IReadOnlyDictionary<string, string> Labels { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// 告警注解（描述/摘要等）.
    /// </summary>
    public IReadOnlyDictionary<string, string> Annotations { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// 开始时间（ISO 8601）.
    /// </summary>
    public string StartsAt { get; set; } = string.Empty;

    /// <summary>
    /// 结束时间（ISO 8601，持续中为空或零值）.
    /// </summary>
    public string EndsAt { get; set; } = string.Empty;

    /// <summary>
    /// 状态（active/suppressed/unprocessed）.
    /// </summary>
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// 触发来源地址.
    /// </summary>
    public string GeneratorUrl { get; set; } = string.Empty;

    /// <summary>
    /// 压制此告警的静默 ID 列表.
    /// </summary>
    public IReadOnlyList<string> SilencedBy { get; set; } = new List<string>();

    /// <summary>
    /// 抑制此告警的告警指纹列表.
    /// </summary>
    public IReadOnlyList<string> InhibitedBy { get; set; } = new List<string>();
}

/// <summary>
/// Alertmanager 静默.
/// </summary>
public class AlertmanagerSilence
{
    /// <summary>
    /// 静默 ID.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 状态（pending/active/expired）.
    /// </summary>
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// 匹配器.
    /// </summary>
    public IReadOnlyDictionary<string, string> Matchers { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// 开始时间（ISO 8601）.
    /// </summary>
    public string StartsAt { get; set; } = string.Empty;

    /// <summary>
    /// 结束时间（ISO 8601）.
    /// </summary>
    public string EndsAt { get; set; } = string.Empty;

    /// <summary>
    /// 创建人.
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// 备注.
    /// </summary>
    public string Comment { get; set; } = string.Empty;
}

/// <summary>
/// Alertmanager 服务与集群状态.
/// </summary>
public class AlertmanagerStatus
{
    /// <summary>
    /// Alertmanager 版本.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// 集群状态（ready/disabled 等）.
    /// </summary>
    public string ClusterStatus { get; set; } = string.Empty;

    /// <summary>
    /// 集群成员名.
    /// </summary>
    public IReadOnlyList<string> PeerNames { get; set; } = new List<string>();
}
