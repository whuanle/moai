using FluentValidation;
using MediatR;
using MoAI.Wiki.Queries.Responses;

namespace MoAI.Wiki.Queries;

/// <summary>
/// 查询知识库文档向量化详情（配置 + 文档状态 + 切片列表），仅团队成员可访问.
/// </summary>
public class QueryWikiDocumentEmbeddingCommand : IRequest<QueryWikiDocumentEmbeddingCommandResponse>, IModelValidator<QueryWikiDocumentEmbeddingCommand>
{
    /// <summary>
    /// 知识库 id，由 Controller 从路由参数回填.
    /// </summary>
    public long WikiId { get; init; }

    /// <summary>
    /// 文档 id，由 Controller 从路由参数回填.
    /// </summary>
    public long DocumentId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryWikiDocumentEmbeddingCommand> validate)
    {
        // WikiId/DocumentId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验.
    }
}
