using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.Agents.AI;

namespace MoAI.AI.Services;

/// <summary>
/// 工具上下文贡献者：聚合所有 <see cref="IAppToolProvider"/> 产出的工具，暴露渐进式工具披露元工具.
/// 审批模式下同时装配人工审批闸口（重要工具挂起等待决策）.
/// </summary>
[InjectOnScoped]
public sealed class AppToolContextProviderContributor : IAppContextProviderContributor
{
    private readonly IEnumerable<IAppToolProvider> _toolProviders;
    private readonly AppToolApprovalService _approvalService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppToolContextProviderContributor"/> class.
    /// </summary>
    /// <param name="toolProviders">工具来源提供者.</param>
    /// <param name="approvalService">工具审批服务.</param>
    public AppToolContextProviderContributor(IEnumerable<IAppToolProvider> toolProviders, AppToolApprovalService approvalService)
    {
        _toolProviders = toolProviders;
        _approvalService = approvalService;
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

        if (tools.Count == 0)
        {
            return null;
        }

        if (context.ToolApprovalMode == MoAI.AI.AppToolApprovalContract.ModeApproval)
        {
            // 审批策略随生效配置（发布快照或草稿）解析：白名单插件与沙箱在审批模式下自动放行
            var gate = new AppToolApprovalGate
            {
                Mode = context.ToolApprovalMode,
                AppId = context.AppId,
                SessionId = context.SessionId,
                UserId = context.UserId,
                Policy = MoAI.AI.AppToolApprovalPolicy.Parse(context.Config?.ExecutionSettings),
            };
            return new AppToolContextProvider(tools, _approvalService, gate);
        }

        return new AppToolContextProvider(tools);
    }
}
