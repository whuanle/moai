using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.DocumentManager.Queries;

/// <summary>
/// 查询 wiki 文件列表.
/// </summary>
public class QueryWikiDocumentListCommand : PagedParamter, IRequest<QueryWikiDocumentListCommandResponse>
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
    /// 包括文件类型，如 .md、.docx 等.
    /// </summary>
    public IReadOnlyCollection<string> IncludeFileTypes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 排除文件类型，如 .md、.docx 等.
    /// </summary>
    public IReadOnlyCollection<string> ExcludeFileTypes { get; init; } = Array.Empty<string>();
}
