using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MoAI.AiPlugin.Queries.Responses;
using MoAI.AiPlugin.Services;

namespace MoAI.AiPlugin.Queries;

/// <summary>
/// <inheritdoc cref="QueryPluginListCommand"/>
/// </summary>
public class QueryPluginListCommandHandler : IRequestHandler<QueryPluginListCommand, QueryPluginListCommandResponse>
{
    private readonly IPluginRegistry _registry;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPluginListCommandHandler"/> class.
    /// </summary>
    /// <param name="registry">插件注册表.</param>
    public QueryPluginListCommandHandler(IPluginRegistry registry)
    {
        _registry = registry;
    }

    /// <inheritdoc/>
    public Task<QueryPluginListCommandResponse> Handle(QueryPluginListCommand request, CancellationToken cancellationToken)
    {
        var items = _registry.GetAll()
            .Select(x => new QueryPluginListCommandResponseItem
            {
                Key = x.Key,
                Name = x.Name,
                Description = x.Description,
                IsDynamic = x.IsDynamic,
                RequestType = x.Request.FullName,
                ResponseType = x.Response.FullName,
                ConfigType = x.ConfigType?.FullName,
                ParamsExample = PluginTypeHelper.GetStaticExample(x.PluginType, "GetParamsExampleValue"),
                ConfigExample = x.IsDynamic ? PluginTypeHelper.GetStaticExample(x.PluginType, "GetConfigExampleValue") : null,
            })
            .ToList();

        return Task.FromResult(new QueryPluginListCommandResponse { Items = items });
    }
}
