using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MoAI.AIPlugin.Queries.Responses;
using MoAI.AIPlugin.Services;

namespace MoAI.AIPlugin.Queries;

/// <summary>
/// <inheritdoc cref="QueryPluginListCommand"/>
/// </summary>
public class QueryPluginListCommandHandler : IRequestHandler<QueryPluginListCommand, QueryPluginListCommandResponse>
{
    private readonly IPluginRegistry _registry;
    private readonly IPluginTemplateMetaProvider _templateMetaProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPluginListCommandHandler"/> class.
    /// </summary>
    /// <param name="registry">插件注册表.</param>
    /// <param name="templateMetaProvider">模板展示补充信息提供者（图标/版本/分类）.</param>
    public QueryPluginListCommandHandler(IPluginRegistry registry, IPluginTemplateMetaProvider templateMetaProvider)
    {
        _registry = registry;
        _templateMetaProvider = templateMetaProvider;
    }

    /// <inheritdoc/>
    public async Task<QueryPluginListCommandResponse> Handle(QueryPluginListCommand request, CancellationToken cancellationToken)
    {
        var plugins = _registry.GetAll();
        var metas = await _templateMetaProvider
            .GetAsync(plugins.Select(x => x.Key).ToList(), cancellationToken)
            .ConfigureAwait(false);

        var items = plugins
            .Select(x => new QueryPluginListCommandResponseItem
            {
                Key = x.Key,
                Name = x.Name,
                Description = string.IsNullOrWhiteSpace(x.HumanDescription) ? x.Description : x.HumanDescription,
                IsDynamic = x.IsDynamic,
                RequestType = x.Request?.FullName,
                ResponseType = x.Response?.FullName,
                ConfigType = x.ConfigType?.FullName,
                ParamsExample = PluginTypeHelper.GetStaticExample(x.PluginType, "GetParamsExampleValue"),
                ConfigExample = x.IsDynamic ? PluginTypeHelper.GetStaticExample(x.PluginType, "GetConfigExampleValue") : null,
                ToolCount = x.IsMultiTool ? x.Tools.Count : 1,
                Tools = PluginTypeHelper.BuildToolItems(x),
                AvatarPath = metas.TryGetValue(x.Key, out var meta) ? meta.AvatarPath : string.Empty,
                Version = metas.TryGetValue(x.Key, out var versionMeta) ? versionMeta.Version : string.Empty,
                CategoryName = metas.TryGetValue(x.Key, out var categoryMeta) ? categoryMeta.CategoryName : string.Empty,
            })
            .ToList();

        return new QueryPluginListCommandResponse { Items = items };
    }
}
