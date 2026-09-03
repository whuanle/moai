using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MoAI.AIPlugin.Commands;
using MoAI.AIPlugin.Models;
using MoAI.AIPlugin.Services;
using MoAI.Infra.Exceptions;

namespace MoAI.AIPlugin.Handlers;

/// <summary>
/// <inheritdoc cref="RunPluginCommand"/>
/// </summary>
public class RunPluginCommandHandler : IRequestHandler<RunPluginCommand, PluginRunResult>
{
    private readonly IPluginRegistry _registry;
    private readonly IPluginExecutor _executor;
    private readonly IDynamicInstanceResolver _dynamicResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="RunPluginCommandHandler"/> class.
    /// </summary>
    /// <param name="registry">插件注册表.</param>
    /// <param name="executor">插件执行引擎.</param>
    /// <param name="dynamicResolver">动态插件实例解析器.</param>
    public RunPluginCommandHandler(IPluginRegistry registry, IPluginExecutor executor, IDynamicInstanceResolver dynamicResolver)
    {
        _registry = registry;
        _executor = executor;
        _dynamicResolver = dynamicResolver;
    }

    /// <inheritdoc/>
    public async Task<PluginRunResult> Handle(RunPluginCommand request, CancellationToken cancellationToken)
    {
        var plugin = _registry.Get(request.Key);
        if (plugin == null)
        {
            var dynamic = _dynamicResolver.Resolve(request.Key);
            if (dynamic != null)
            {
                return await _executor.ExecuteAsync(dynamic.Template, request.RequestJson, dynamic.ConfigJson, cancellationToken).ConfigureAwait(false);
            }

            throw new BusinessException("插件不存在") { StatusCode = 404 };
        }

        return await _executor.ExecuteAsync(plugin, request.RequestJson, request.ConfigJson, cancellationToken).ConfigureAwait(false);
    }
}
