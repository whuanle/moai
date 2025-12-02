using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.Models;
using MoAI.Wiki.Plugins.Crawler.Models;

namespace MoAI.Wiki.Plugins.Template.Commands;

/// <summary>
/// 修改一个网页爬取配置.
/// <typeparamref name="T">类型.</typeparamref>
/// </summary>
public class UpdateWikiPluginConfigCommand<T> : IRequest<EmptyCommandResponse>
    where T : class, IWikiPluginKey
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 配置 id.
    /// </summary>
    public int ConfigId { get; init; }

    /// <summary>
    /// 标题.
    /// </summary>
    public string Title { get; init; } = default!;

    /// <summary>
    /// 配置，前端传递不用填.
    /// </summary>
    public T Config { get; init; } = default!;
}