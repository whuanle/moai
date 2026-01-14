using System.ComponentModel;

namespace MoAI.Wiki.Mcp;

/// <summary>
/// 知识库搜索.
/// </summary>
public class WikiMcpSearchRequest
{
    /// <summary>
    /// 知识库id.
    /// </summary>
    [Description("知识库id")]
    public int WikiId { get; init; }

    /// <summary>
    /// 提问.
    /// </summary>
    [Description("提问")]
    public string Query { get; init; } = string.Empty;

    /// <summary>
    /// 是否需要 ai 回答整理问题.
    /// </summary>
    public bool IsAnswer { get; init; }
}
