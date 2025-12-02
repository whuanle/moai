using MediatR;
using MoAI.Wiki.Models;
using MoAI.Wiki.Plugins.Template.Models;

namespace MoAI.Wiki.Plugins.Template.Queries;

/// <summary>
/// 查询插件配置内容.
/// </summary>
/// <typeparam name="T"></typeparam>
public class QueryWikiPluginConfigCommand<T> : IRequest<QueryWikiPluginrConfigCommandResponse<T>>
    where T : IWikiPluginKey
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
