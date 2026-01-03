namespace MoAI.Wiki.Plugins.Mcp.Models;

/// <summary>
/// 查询知识库 MCP 配置响应.
/// </summary>
public class QueryWikiMcpConfigCommandResponse
{
    /// <summary>
    /// 配置 Id.
    /// </summary>
    public int ConfigId { get; set; }

    /// <summary>
    /// 知识库 Id.
    /// </summary>
    public int WikiId { get; set; }

    /// <summary>
    /// 是否已启用.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// MCP 访问密钥.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// MCP 服务地址.
    /// </summary>
    public string McpUrl { get; set; } = string.Empty;

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset? CreateTime { get; set; }
}
