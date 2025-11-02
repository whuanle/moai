using MediatR;
using MoAI.Wiki.Documents.Queries.Responses;

namespace MoAI.Wiki.Documents.Queries;

/// <summary>
/// 查询文档任务列表.
/// </summary>
public class QueryWikiDocumentTaskListCommand : IRequest<IReadOnlyCollection<WikiDocumentTaskItem>>
{
    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; set; }

    /// <summary>
    /// 文档id.
    /// </summary>
    public int DocumentId { get; set; }
}
