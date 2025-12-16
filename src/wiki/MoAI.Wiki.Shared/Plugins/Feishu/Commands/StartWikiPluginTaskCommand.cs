using MediatR;
using MoAI.AiModel.Models;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Plugins.Template.Commands;

/// <summary>
/// 开始启动知识库插件任务.
/// </summary>
public class StartWikiFeishuPluginTaskCommand : IRequest<SimpleGuid>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 知识库 web 配置 id.
    /// </summary>
    public int ConfigId { get; init; }
}
