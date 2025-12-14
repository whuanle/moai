namespace MoAI.AiModel.Models;

/// <summary>
/// 知识库配置.
/// </summary>
public class EmbeddingConfig
{
    /// <summary>
    /// 指定进行文档向量化的模型.
    /// </summary>
    public int EmbeddingModelId { get; set; }

    /// <summary>
    /// 维度，跟模型有关，小于嵌入向量的最大值.
    /// </summary>
    public int EmbeddingDimensions { get; set; }
}