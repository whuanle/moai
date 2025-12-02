using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.Plugins.Feishu.Models;
using MoAI.Wiki.Plugins.Template.Commands;

namespace MoAI.Wiki.Plugins.Feishu.Commands;

/// <summary>
/// 飞书配置.
/// </summary>
public class AddWikiFeishuConfigCommand : AddWikiPluginConfigCommand<WikiFeishuConfig>, IRequest<SimpleInt>
{
}