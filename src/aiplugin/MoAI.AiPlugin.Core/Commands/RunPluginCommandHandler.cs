using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MoAI.AiPlugin.Commands;
using MoAI.AiPlugin.Models;
using MoAI.AiPlugin.Services;
using MoAI.Infra.Exceptions;

namespace MoAI.AiPlugin.Handlers;

/// <summary>
/// <inheritdoc cref="RunPluginCommand"/>
/// </summary>
public class RunPluginCommandHandler : IRequestHandler<RunPluginCommand, PluginRunResult>
{
    private readonly IPluginRegistry _registry;
    private readonly IPluginExecutor _executor;

    /// <summary>
    /// Initializes a new instance of the <see cref="RunPluginCommandHandler"/> class.
    /// </summary>
    /// <param name="registry">插件注册表.</param>
    /// <param name="executor">插件执行引擎.</param>
    public RunPluginCommandHandler(IPluginRegistry registry, IPluginExecutor executor)
    {
        _registry = registry;
        _executor = executor;
    }

    /// <inheritdoc/>
    public async Task<PluginRunResult> Handle(RunPluginCommand request, CancellationToken cancellationToken)
    {
        var plugin = _registry.Get(request.Key);
        if (plugin == null)
        {
            throw new BusinessException("插件不存在") { StatusCode = 404 };
        }

        return await _executor.ExecuteAsync(plugin, request.RequestJson, request.ConfigJson, cancellationToken).ConfigureAwait(false);
    }
}
