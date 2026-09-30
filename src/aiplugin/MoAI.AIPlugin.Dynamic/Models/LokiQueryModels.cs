using System.Collections.Generic;
using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Loki 日志查询插件配置（每个实例独立保存）.
/// </summary>
public class LokiQueryConfig
{
    /// <summary>
    /// Loki 服务地址.
    /// </summary>
    [Description("Loki 服务地址，例如 http://loki:3100；支持子路径部署，如 http://host/loki")]
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
    [Description("Bearer Token（可选，如 Loki Gateway）；已填时优先于用户名密码")]
    public string BearerToken { get; set; } = string.Empty;

    /// <summary>
    /// 单次请求超时秒数（1-300）.
    /// </summary>
    [Description("单次请求超时秒数，取值 1-300（默认 30）")]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 日志行总数上限（1-2000）.
    /// </summary>
    [Description("查询返回的日志行总数上限，取值 1-2000（默认 200）；同时作为下发给 Loki 的 limit，超出截断并把 Truncated 置为 true")]
    public int MaxLines { get; set; } = 200;

    /// <summary>
    /// 单行最大字符数（128-4096）.
    /// </summary>
    [Description("单行日志最大返回字符数，取值 128-4096（默认 1024），超出截断")]
    public int MaxLineChars { get; set; } = 1024;

    /// <summary>
    /// 标签/序列列表最多返回条数（1-500）.
    /// </summary>
    [Description("labels/label_values/series 等列表最多返回条数，取值 1-500（默认 200）")]
    public int MaxListItems { get; set; } = 200;
}

/// <summary>
/// Loki 日志查询插件请求参数.
/// </summary>
public class LokiQueryRequest
{
    /// <summary>
    /// 操作模式.
    /// </summary>
    [Description("操作模式：query=即时 LogQL（默认）/ query_range=区间 LogQL / labels=标签名列表 / label_values=某标签取值 / series=标签集；未知值会被拒绝")]
    public string Mode { get; set; } = "query";

    /// <summary>
    /// LogQL 表达式（query/query_range 必填）.
    /// </summary>
    [Description("LogQL 表达式（query/query_range 模式必填），例如 {job=\"nginx\"} |= \"error\"")]
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// query 模式的求值时间（可空=当前）.
    /// </summary>
    [Description("query 模式的求值时间（RFC3339 或 Unix 秒），缺省为当前时间")]
    public string? Time { get; set; }

    /// <summary>
    /// query_range 模式的开始时间.
    /// </summary>
    [Description("query_range 模式的开始时间（RFC3339 或 Unix 秒）；Start/End 全缺省时向历史回看 1 小时")]
    public string? Start { get; set; }

    /// <summary>
    /// query_range 模式的结束时间.
    /// </summary>
    [Description("query_range 模式的结束时间（RFC3339 或 Unix 秒），缺省为当前时间")]
    public string? End { get; set; }

    /// <summary>
    /// query_range 模式的方向.
    /// </summary>
    [Description("query_range 模式的方向：backward=最新在前（默认）/ forward")]
    public string? Direction { get; set; }

    /// <summary>
    /// label_values 模式：目标标签名.
    /// </summary>
    [Description("label_values 模式：目标标签名，例如 job")]
    public string? Label { get; set; }

    /// <summary>
    /// series 模式：match[] 选择器.
    /// </summary>
    [Description("series 模式：match[] 选择器（必填），例如 {job=\"nginx\"}")]
    public string? Selector { get; set; }
}

/// <summary>
/// Loki 日志查询插件响应.
/// </summary>
public class LokiQueryResponse
{
    /// <summary>
    /// 结果类型：query / query_range / labels / label_values / series.
    /// </summary>
    public string ResultType { get; set; } = string.Empty;

    /// <summary>
    /// query/query_range 模式：日志流列表.
    /// </summary>
    public IReadOnlyList<LokiStream> Streams { get; set; } = new List<LokiStream>();

    /// <summary>
    /// labels 模式：标签名列表.
    /// </summary>
    public IReadOnlyList<string> Labels { get; set; } = new List<string>();

    /// <summary>
    /// label_values 模式：标签取值列表.
    /// </summary>
    public IReadOnlyList<string> LabelValues { get; set; } = new List<string>();

    /// <summary>
    /// series 模式：标签集列表.
    /// </summary>
    public IReadOnlyList<IReadOnlyDictionary<string, string>> Series { get; set; } = new List<IReadOnlyDictionary<string, string>>();

    /// <summary>
    /// 是否因超过上限被截断.
    /// </summary>
    public bool Truncated { get; set; }
}

/// <summary>
/// Loki 日志流（同一标签集下的日志行）.
/// </summary>
public class LokiStream
{
    /// <summary>
    /// 流标签.
    /// </summary>
    public IReadOnlyDictionary<string, string> Labels { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// 日志行（时间 ISO + 内容）.
    /// </summary>
    public IReadOnlyList<LokiLine> Lines { get; set; } = new List<LokiLine>();
}

/// <summary>
/// Loki 日志行.
/// </summary>
public class LokiLine
{
    /// <summary>
    /// 时间戳（ISO 8601 UTC，由纳秒换算）.
    /// </summary>
    public string Timestamp { get; set; } = string.Empty;

    /// <summary>
    /// 日志内容（按 MaxLineChars 截断）.
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
