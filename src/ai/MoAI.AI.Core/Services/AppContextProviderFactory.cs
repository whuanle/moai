using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.Agents.AI;
using MoAI.Database.Entities;

namespace MoAI.AI.Services;

/// <summary>
/// 构建应用 Agent 的上下文（应用/配置/用户/会话）.
/// </summary>
public sealed class AppAgentBuildContext
{
    /// <summary>
    /// 应用.
    /// </summary>
    public AppEntity App { get; init; } = default!;

    /// <summary>
    /// 应用配置，可为空.
    /// </summary>
    public AppAgentConfigEntity? Config { get; init; }

    /// <summary>
    /// 应用 id.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 团队 id.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 用户 id.
    /// </summary>
    public long UserId { get; init; }

    /// <summary>
    /// 会话 id.
    /// </summary>
    public Guid SessionId { get; init; }

    /// <summary>
    /// 绑定的知识库 id.
    /// </summary>
    public IReadOnlyList<long> WikiIds { get; init; } = [];

    /// <summary>
    /// 绑定的插件 id.
    /// </summary>
    public IReadOnlyList<Guid> PluginIds { get; init; } = [];
}

/// <summary>
/// 上下文提供者贡献者：把一种能力（知识库 RAG、压缩等）装配成 <see cref="AIContextProvider"/>.
/// </summary>
public interface IAppContextProviderContributor
{
    /// <summary>
    /// 执行顺序，越小越先执行（压缩应放最后）.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// 创建上下文提供者；不适用时返回 null.
    /// </summary>
    /// <param name="context">构建上下文.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>上下文提供者或 null.</returns>
    Task<AIContextProvider?> CreateAsync(AppAgentBuildContext context, CancellationToken cancellationToken);
}

/// <summary>
/// 应用上下文提供者工厂：按 Order 收集所有贡献者，新增能力无需改动工厂.
/// </summary>
[InjectOnScoped]
public sealed class AppContextProviderFactory
{
    private readonly IEnumerable<IAppContextProviderContributor> _contributors;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppContextProviderFactory"/> class.
    /// </summary>
    /// <param name="contributors">上下文提供者贡献者.</param>
    public AppContextProviderFactory(IEnumerable<IAppContextProviderContributor> contributors)
    {
        _contributors = contributors;
    }

    /// <summary>
    /// 构建上下文提供者列表.
    /// </summary>
    /// <param name="context">构建上下文.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>上下文提供者列表.</returns>
    public async Task<IReadOnlyList<AIContextProvider>> BuildAsync(AppAgentBuildContext context, CancellationToken cancellationToken)
    {
        var result = new List<AIContextProvider>();
        foreach (var contributor in System.Linq.Enumerable.OrderBy(_contributors, x => x.Order))
        {
            var provider = await contributor.CreateAsync(context, cancellationToken).ConfigureAwait(false);
            if (provider != null)
            {
                result.Add(provider);
            }
        }

        return result;
    }
}
