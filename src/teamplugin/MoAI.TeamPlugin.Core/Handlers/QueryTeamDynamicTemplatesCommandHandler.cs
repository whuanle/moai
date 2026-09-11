using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MoAI.AIPlugin.Queries.Responses;
using MoAI.AIPlugin.Services;
using MoAI.Infra.Exceptions;
using MoAI.Team.Services;
using MoAI.TeamPlugin.Queries;

namespace MoAI.TeamPlugin.Handlers;

/// <summary>
/// <inheritdoc cref="QueryTeamDynamicTemplatesCommand"/> 查询动态插件模板列表.
/// </summary>
public class QueryTeamDynamicTemplatesCommandHandler : IRequestHandler<QueryTeamDynamicTemplatesCommand, QueryPluginListCommandResponse>
{
    private readonly IPluginRegistry _registry;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTeamDynamicTemplatesCommandHandler"/> class.
    /// </summary>
    /// <param name="registry">插件注册表.</param>
    /// <param name="teamService">团队领域服务.</param>
    public QueryTeamDynamicTemplatesCommandHandler(IPluginRegistry registry, ITeamService teamService)
    {
        _registry = registry;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<QueryPluginListCommandResponse> Handle(QueryTeamDynamicTemplatesCommand request, CancellationToken cancellationToken)
    {
        var myRole = await _teamService.GetMyRoleAsync(request.TeamId, request.ContextUserId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        var items = _registry.GetAll()
            .Where(x => x.IsDynamic)
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
                ConfigExample = PluginTypeHelper.GetStaticExample(x.PluginType, "GetConfigExampleValue"),
            })
            .ToList();

        return new QueryPluginListCommandResponse { Items = items };
    }
}
