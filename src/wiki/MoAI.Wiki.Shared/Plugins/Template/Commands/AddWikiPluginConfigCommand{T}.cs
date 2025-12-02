using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Plugins.Template.Commands;

/// <summary>
/// 添加配置.
/// </summary>
/// <typeparam name="T">类型.</typeparam>
public class AddWikiPluginConfigCommand<T> : IRequest<SimpleInt>
    where T : class, IWikiPluginKey
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 标题.
    /// </summary>
    public string Title { get; init; } = default!;

    /// <summary>
    /// 配置，前端传递不用填.
    /// </summary>
    public T Config { get; init; } = default!;
}