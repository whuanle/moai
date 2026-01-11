using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.DocumentManager.Queries;

/// <summary>
/// 查询 wiki 文件列表.
/// </summary>
public class QueryWikiDocumentListCommand : PagedParamter, IRequest<QueryWikiDocumentListCommandResponse>, IModelValidator<QueryWikiDocumentListCommand>, IDynamicOrderable
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 筛选文件名称.
    /// </summary>
    public string? Query { get; init; } = default!;

    /// <summary>
    /// 是否已经向量化.
    /// </summary>
    public bool? IsEmbedding { get; init; }

    /// <summary>
    /// 包括文件类型，如 .md、.docx 等.
    /// </summary>
    public IReadOnlyCollection<string> IncludeFileTypes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 排除文件类型，如 .md、.docx 等.
    /// </summary>
    public IReadOnlyCollection<string> ExcludeFileTypes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 排序，支持 FileName、FileSize、CreateTime、UpdateTime 排序.
    /// </summary>
    public IReadOnlyCollection<KeyValueBool> OrderByFields { get; init; } = Array.Empty<KeyValueBool>();

    private static readonly HashSet<string> allowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        nameof(QueryWikiDocumentListItem.FileName),
        nameof(QueryWikiDocumentListItem.FileSize),
        nameof(QueryWikiDocumentListItem.CreateTime),
        nameof(QueryWikiDocumentListItem.UpdateTime)
    };

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryWikiDocumentListCommand> validate)
    {
        validate.RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        validate.RuleFor(x => x.OrderByFields)
            .Must(fields =>
            {
                return fields.All(field => allowedFields.Contains(field.Key));
            })
            .WithMessage("只支持排序 FileName、FileSize, CreateTime, UpdateTime.");
    }
}
