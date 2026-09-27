using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Elasticsearch 查询插件配置（每个实例独立保存）.
/// </summary>
public class ElasticsearchQueryConfig
{
    /// <summary>
    /// Elasticsearch 服务地址.
    /// </summary>
    [Description("Elasticsearch 服务地址（HTTP 根地址），例如 http://es:9200；可含路径前缀，如 http://proxy:443/es")]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Basic 认证用户名（可选）.
    /// </summary>
    [Description("Basic 认证用户名（可选）；与 ApiKey 都未填则匿名访问")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Basic 认证密码（可选）.
    /// </summary>
    [Description("Basic 认证密码（可选）")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// ApiKey（可选）：Elasticsearch 「已编码凭据」或原始凭据文本，已填时优先于用户名密码.
    /// </summary>
    [Description("ApiKey（可选）：Kibana 认证页生成的已编码 API Key，走 Authorization: ApiKey 头；已填时优先于用户名密码")]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// 单次请求超时秒数（1-300）.
    /// </summary>
    [Description("单次请求超时秒数，取值 1-300（默认 30）")]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// search 模式最多返回的命中条数（1-1000）.
    /// </summary>
    [Description("search 模式最多返回的命中条数，取值 1-1000（默认 100）；超出部分被丢弃并把 Truncated 置为 true")]
    public int MaxHits { get; set; } = 100;

    /// <summary>
    /// 单条命中 _source 的最大字符数（100-200000）.
    /// </summary>
    [Description("单条命中 _source 的最大字符数，取值 100-200000（默认 4000）；超出被截断并把该条 SourceTruncated 置为 true")]
    public int MaxSourceCharsPerHit { get; set; } = 4000;

    /// <summary>
    /// 聚合/映射等整段 JSON 文本的最大字符数（1-200000）.
    /// </summary>
    [Description("AggregationsJson 与 MappingJson 的最大字符数，取值 1-200000（默认 20000）")]
    public int MaxResponseChars { get; set; } = 20000;
}
