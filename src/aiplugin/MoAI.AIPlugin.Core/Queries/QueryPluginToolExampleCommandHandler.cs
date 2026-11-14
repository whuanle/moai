using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MoAI.AIPlugin.Queries.Responses;
using MoAI.AIPlugin.Services;
using MoAI.Infra.Exceptions;

namespace MoAI.AIPlugin.Queries;

/// <summary>
/// <inheritdoc cref="QueryPluginToolExampleCommand"/>
/// </summary>
public class QueryPluginToolExampleCommandHandler : IRequestHandler<QueryPluginToolExampleCommand, QueryPluginToolExampleCommandResponse>
{
    private const string RunToolKey = "run";

    private readonly IPluginRegistry _registry;
    private readonly IDynamicInstanceResolver _dynamicResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPluginToolExampleCommandHandler"/> class.
    /// </summary>
    /// <param name="registry">插件注册表.</param>
    /// <param name="dynamicResolver">动态插件实例解析器.</param>
    public QueryPluginToolExampleCommandHandler(IPluginRegistry registry, IDynamicInstanceResolver dynamicResolver)
    {
        _registry = registry;
        _dynamicResolver = dynamicResolver;
    }

    /// <inheritdoc/>
    public Task<QueryPluginToolExampleCommandResponse> Handle(QueryPluginToolExampleCommand request, CancellationToken cancellationToken)
    {
        var plugin = QueryPluginToolsCommandHandler.ResolveTemplate(_registry, _dynamicResolver, request.Key)
            ?? throw new BusinessException("插件不存在") { StatusCode = 404 };

        var example = plugin.IsMultiTool
            ? plugin.FindTool(request.ToolKey)?.ParamsExample
                ?? throw new BusinessException($"工具不存在：{request.ToolKey}") { StatusCode = 404 }
            : PluginTypeHelper.GetStaticExample(plugin.PluginType, "GetParamsExampleValue");

        return Task.FromResult(new QueryPluginToolExampleCommandResponse
        {
            ToolKey = plugin.IsMultiTool ? plugin.FindTool(request.ToolKey)!.Key : RunToolKey,
            ParamsExample = example,
        });
    }
}
