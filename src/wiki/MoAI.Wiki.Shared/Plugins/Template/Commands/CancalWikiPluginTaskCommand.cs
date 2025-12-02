using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Plugins.Crawler.Commands;

/// <summary>
/// 取消知识库插件任务.
/// </summary>
public class CancalWikiPluginTaskCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 配置.
    /// </summary>
    public int ConfigId { get; init; }
}
