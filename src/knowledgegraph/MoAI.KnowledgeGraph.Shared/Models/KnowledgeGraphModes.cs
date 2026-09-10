namespace MoAI.KnowledgeGraph.Models;

/// <summary>
/// 图谱来源常量.
/// </summary>
public static class KnowledgeGraphModes
{
    /// <summary>
    /// 平台托管.
    /// </summary>
    public const string Managed = "managed";

    /// <summary>
    /// 外部接入.
    /// </summary>
    public const string Connected = "connected";

    /// <summary>
    /// 是否合法.
    /// </summary>
    /// <param name="mode">来源值.</param>
    /// <returns>合法返回 true.</returns>
    public static bool IsValid(string? mode)
        => mode == Managed || mode == Connected;
}
