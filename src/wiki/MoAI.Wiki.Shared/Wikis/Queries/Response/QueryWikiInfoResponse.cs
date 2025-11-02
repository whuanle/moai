using MoAI.AiModel.Models;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Wikis.Queries.Response;

/// <summary>
/// 知识库信息.
/// </summary>
public class QueryWikiInfoResponse : AuditsInfo
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 知识库名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 知识库描述.
    /// </summary>
    public string Description { get; init; } = default!;

    /// <summary>
    /// 公开使用，所有人不需要加入团队即可使用此知识库.
    /// </summary>
    public bool IsPublic { get; init; } = default!;

    /// <summary>
    /// 是否是系统知识库.
    /// </summary>
    public bool IsSystem { get; init; }

    /// <summary>
    /// 是否该知识库成员.
    /// </summary>
    public bool IsUser { get; init; }

    /// <summary>
    /// 文档数量.
    /// </summary>
    public int DocumentCount { get; init; }

    /// <summary>
    /// 指定进行文档向量化的模型.
    /// </summary>
    public int EmbeddingModelId { get; init; }

    /// <summary>
    /// 锁定配置，锁定后不能再修改.
    /// </summary>
    public bool IsLock { get; init; }

    /// <summary>
    /// 维度，跟模型有关.
    /// </summary>
    public int EmbeddingDimensions { get; init; }
}
