using MediatR;

namespace MoAI.Wiki.Plugins.Template.Queries;

/// <summary>
/// 查询定时任务信息.
/// </summary>
public class QueryRecurringJobCommand : IRequest<QueryRecurringJobCommandResponse>
{
    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 插件 id.
    /// </summary>
    public int ConfigId { get; init; }
}