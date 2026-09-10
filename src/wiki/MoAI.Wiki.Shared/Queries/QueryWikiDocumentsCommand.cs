using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.Queries.Responses;

namespace MoAI.Wiki.Queries;

/// <summary>
/// 查询知识库文档列表，仅团队成员可访问.
/// </summary>
public class QueryWikiDocumentsCommand : PagedParamter, IRequest<QueryWikiDocumentsCommandResponse>, IModelValidator<QueryWikiDocumentsCommand>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public long WikiId { get; init; }

    /// <summary>
    /// 筛选文件名称.
    /// </summary>
    public string? Query { get; init; }

    /// <summary>
    /// 是否已经向量化（null 表示不过滤）.
    /// </summary>
    public bool? IsEmbedding { get; init; }

    /// <summary>
    /// 包含的文件类型（如 .md、.docx）.
    /// </summary>
    public IReadOnlyCollection<string> IncludeFileTypes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 排除的文件类型（如 .md、.docx）.
    /// </summary>
    public IReadOnlyCollection<string> ExcludeFileTypes { get; init; } = Array.Empty<string>();

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryWikiDocumentsCommand> validate)
    {
        // WikiId 来自路由参数由 Controller 回填，自动验证发生在回填前，此处不做校验（否则恒 400）
    }
}
