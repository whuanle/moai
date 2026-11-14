using FluentValidation;
using MediatR;
using MoAI.Wiki.Queries.Responses;

namespace MoAI.Wiki.Queries;

/// <summary>
/// 查询知识库文档的内嵌图片附件列表（文档提取 pdf/word/excel/ppt 时落出的图片），仅团队成员可访问.
/// </summary>
public class QueryWikiDocumentImagesCommand : IRequest<QueryWikiDocumentImagesCommandResponse>, IModelValidator<QueryWikiDocumentImagesCommand>
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
    public static void Validate(AbstractValidator<QueryWikiDocumentImagesCommand> validate)
    {
        // WikiId/DocumentId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验.
    }
}
