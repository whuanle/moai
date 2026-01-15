namespace MoAI.Wiki.Plugins.Mcp.Models;

/// <summary>
/// 知识库 MCP 插件配置.
/// </summary>
public class WikiMcpConfig
{
    /// <summary>
    /// 是否启用.
    /// </summary>
    public bool IsEnable { get; set; }

    /// <summary>
    /// MCP 访问密钥.
    /// </summary>
    public string Key { get; set; } = string.Empty;
}
