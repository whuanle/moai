using MediatR;
using MoAI.Wiki.Plugins.Feishu.Models;

namespace MoAI.Wiki.Plugins.Feishu.Queries;

/// <summary>
/// 查询这个爬虫的所有任务状态.
/// </summary>
public class QueryWikiFeishuPageTasksCommand : IRequest<QueryWikiFeishuPageTasksCommandResponse>
{
    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// id.
    /// </summary>
    public int ConfigId { get; init; }
}