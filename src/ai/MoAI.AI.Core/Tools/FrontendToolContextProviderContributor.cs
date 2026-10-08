using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.Agents.AI;

namespace MoAI.AI.Services;

/// <summary>
/// 前端展示工具贡献者：对话请求头 X-Moai-Ui-Tools=1（前端已适配侧边栏渲染）时注册 ui_ 前缀工具，
/// 其余渠道（嵌入组件/外部接口/流程内 Agent 节点）不注册，行为不变.
/// </summary>
[InjectOnScoped]
public sealed class FrontendToolContextProviderContributor : IAppContextProviderContributor
{
    /// <inheritdoc/>
    public int Order => 19;

    /// <inheritdoc/>
    public Task<AIContextProvider?> CreateAsync(AppAgentBuildContext context, CancellationToken cancellationToken)
    {
        // 仅 Agent 应用对话且前端显式启用时注册；Workflow 应用在工厂分支中不走上下文贡献者
        return context.EnableUiTools
            ? Task.FromResult<AIContextProvider?>(new FrontendToolContextProvider())
            : Task.FromResult<AIContextProvider?>(null);
    }
}
