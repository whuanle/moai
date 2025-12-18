using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Batch.Models;

/// <summary>
/// 任务列表.
/// </summary>
public class WikiBatchProcessDocumenItem : AuditsInfo
{
    /// <summary>
    /// 任务 id.
    /// </summary>
    public Guid TaskId { get; init; }

    /// <summary>
    /// 任务 id.
    /// </summary>
    public WorkerState State { get; init; }

    /// <summary>
    /// 消息、错误信息.
    /// </summary>
    public string Message { get; set; } = default!;

    /// <summary>
    /// 自定义数据,json格式.
    /// </summary>
    public string Data { get; set; } = default!;
}