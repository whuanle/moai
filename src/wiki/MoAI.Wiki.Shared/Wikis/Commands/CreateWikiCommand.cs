using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Wikis.Commands;

/// <summary>
/// 创建知识库.
/// </summary>
public class CreateWikiCommand : IRequest<SimpleInt>
{
    /// <summary>
    /// 团队名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 团队描述.
    /// </summary>
    public string Description { get; init; } = default!;

    /// <summary>
    /// 是否是系统知识库，创建后不允许修改此属性.
    /// </summary>
    public bool IsPublic { get; init; }
}
