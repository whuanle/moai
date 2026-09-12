using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.Agents.AI;

namespace MoAI.AI.Services;

/// <summary>
/// 工具上下文贡献者：聚合所有 <see cref="IAppToolProvider"/> 产出的工具，暴露渐进式工具披露元工具.
/// </summary>
[InjectOnScoped]
public sealed class AppToolContextProviderContributor : IAppContextProviderContributor
{
    private readonly IEnumerable<IAppToolProvider> _toolProviders;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppToolContextProviderContributor"/> class.
    /// </summary>
    /// <param name="toolProviders">工具来源提供者.</param>
    public AppToolContextProviderContributor(IEnumerable<IAppToolProvider> toolProviders)
    {
        _toolProviders = toolProviders;
    }

    /// <inheritdoc/>
    public int Order => 20;

    /// <inheritdoc/>
    public async Task<AIContextProvider?> CreateAsync(AppAgentBuildContext context, CancellationToken cancellationToken)
    {
        var tools = new List<AppTool>();
        var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        foreach (var provider in _toolProviders.OrderBy(x => x.Order))
        {
            var produced = await provider.GetToolsAsync(context, cancellationToken).ConfigureAwait(false);
            foreach (var tool in produced)
            {
                if (!string.IsNullOrWhiteSpace(tool.Name) && seen.Add(tool.Name))
                {
                    tools.Add(tool);
                }
            }
        }

        return tools.Count == 0 ? null : new AppToolContextProvider(tools);
    }
}
