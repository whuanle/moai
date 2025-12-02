using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.Plugins.Feishu.Models;
using MoAI.Wiki.Plugins.Template.Commands;

namespace MoAI.Wiki.Plugins.Feishu.Commands;

/// <summary>
/// 修改一个网页爬取配置.
/// </summary>
public class UpdateWikiFeishuConfigCommand : UpdateWikiPluginConfigCommand<WikiFeishuConfig>, IRequest<EmptyCommandResponse>
{
}
