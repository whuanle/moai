using MediatR;
using MoAI.AiModel.Models;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Wikis.Commands;

/// <summary>
/// 更新知识库设置信息.
/// </summary>
public class UpdateWikiConfigCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 知识库名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 知识库描述.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 算法公开.
    /// </summary>
    public bool IsPublic { get; init; }

    /// <summary>
    /// 指定进行文档向量化的模型.
    /// </summary>
    public int EmbeddingModelId { get; set; }

    /// <summary>
    /// 维度，跟模型有关，小于嵌入向量的最大值.
    /// </summary>
    public int EmbeddingDimensions { get; set; }
}
