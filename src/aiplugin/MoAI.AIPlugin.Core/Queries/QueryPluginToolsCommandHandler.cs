using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MoAI.AIPlugin.Models;
using MoAI.AIPlugin.Queries.Responses;
using MoAI.AIPlugin.Services;
using MoAI.Infra.Exceptions;

namespace MoAI.AIPlugin.Queries;

/// <summary>
/// <inheritdoc cref="QueryPluginToolsCommand"/>
/// </summary>
public class QueryPluginToolsCommandHandler : IRequestHandler<QueryPluginToolsCommand, QueryPluginToolsCommandResponse>
{
    private const string RunToolKey = "run";

    private readonly IPluginRegistry _registry;
    private readonly IDynamicInstanceResolver _dynamicResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPluginToolsCommandHandler"/> class.
    /// </summary>
    /// <param name="registry">插件注册表.</param>
    /// <param name="dynamicResolver">动态插件实例解析器.</param>
    public QueryPluginToolsCommandHandler(IPluginRegistry registry, IDynamicInstanceResolver dynamicResolver)
    {
        _registry = registry;
        _dynamicResolver = dynamicResolver;
    }

    /// <inheritdoc/>
    public Task<QueryPluginToolsCommandResponse> Handle(QueryPluginToolsCommand request, CancellationToken cancellationToken)
    {
        var plugin = ResolveTemplate(request.Key)
            ?? throw new BusinessException("插件不存在") { StatusCode = 404 };

        var items = plugin.IsMultiTool
            ? plugin.Tools.Select(x => new QueryPluginToolItem
            {
                Key = x.Key,
                Title = x.Title,
                Description = x.Description,
                ParamsExample = x.ParamsExample,
            }).ToList()
            : BuildSingleToolItem(plugin);

        return Task.FromResult(new QueryPluginToolsCommandResponse
        {
            Key = plugin.Key,
            Name = plugin.Name,
            Description = plugin.Description,
            IsDynamic = plugin.IsDynamic,
            Items = items,
        });
    }

    /// <summary>
    /// 按模板 key 或动态插件实例 key 解析模板元数据.
    /// </summary>
    internal static PluginInfo? ResolveTemplate(IPluginRegistry registry, IDynamicInstanceResolver dynamicResolver, string key)
    {
        return registry.Get(key) ?? dynamicResolver.Resolve(key)?.Template;
    }

    private PluginInfo? ResolveTemplate(string key)
    {
        return ResolveTemplate(_registry, _dynamicResolver, key);
    }

    private static List<QueryPluginToolItem> BuildSingleToolItem(PluginInfo plugin)
    {
        return
        [
            new QueryPluginToolItem
            {
                Key = RunToolKey,
                Title = plugin.Name,
                Description = plugin.Description,
                ParamsExample = PluginTypeHelper.GetStaticExample(plugin.PluginType, "GetParamsExampleValue"),
            },
        ];
    }
}
