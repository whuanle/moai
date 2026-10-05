namespace MoAI.AIChannel.Models;

/// <summary>
/// 重排序结果项：文档在其输入集合中的位置与相关性得分.
/// </summary>
public readonly record struct RerankResultItem
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RerankResultItem"/> struct.
    /// </summary>
    /// <param name="index">文档在请求 documents 数组中的下标.</param>
    /// <param name="relevanceScore">与查询的相关性得分（越大越相关）.</param>
    public RerankResultItem(int index, double relevanceScore)
    {
        Index = index;
        RelevanceScore = relevanceScore;
    }

    /// <summary>
    /// 文档在请求 documents 数组中的下标.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// 与查询的相关性得分（越大越相关）.
    /// </summary>
    public double RelevanceScore { get; }
}
