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
    private readonly IPluginTemplateMetaProvider _templateMetaProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTeamDynamicTemplatesCommandHandler"/> class.
    /// </summary>
    /// <param name="registry">插件注册表.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="templateMetaProvider">模板展示补充信息提供者（图标/版本/分类）.</param>
    public QueryTeamDynamicTemplatesCommandHandler(IPluginRegistry registry, ITeamService teamService, IPluginTemplateMetaProvider templateMetaProvider)
    {
        _registry = registry;
        _teamService = teamService;
        _templateMetaProvider = templateMetaProvider;
    }

    /// <inheritdoc/>
    public async Task<QueryPluginListCommandResponse> Handle(QueryTeamDynamicTemplatesCommand request, CancellationToken cancellationToken)
    {
        var myRole = await _teamService.GetMyRoleAsync(request.TeamId, request.ContextUserId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        var dynamicPlugins = _registry.GetAll()
            .Where(x => x.IsDynamic)
            .ToList();
        var metas = await _templateMetaProvider
            .GetAsync(dynamicPlugins.Select(x => x.Key).ToList(), cancellationToken)
            .ConfigureAwait(false);

        var items = dynamicPlugins
            .Select(x =>
            {
                metas.TryGetValue(x.Key, out var meta);
                return new QueryPluginListCommandResponseItem
                {
                    Key = x.Key,
                    Name = x.Name,
                    Description = string.IsNullOrWhiteSpace(x.HumanDescription) ? x.Description : x.HumanDescription,
                    IsDynamic = x.IsDynamic,
                    // 多工具插件（text_toolkit 等）无插件级 Request/Response，类型在 Tools 各项上
                    RequestType = x.Request?.FullName,
                    ResponseType = x.Response?.FullName,
                    ConfigType = x.ConfigType?.FullName,
                    ParamsExample = PluginTypeHelper.GetStaticExample(x.PluginType, "GetParamsExampleValue"),
                    ConfigExample = PluginTypeHelper.GetStaticExample(x.PluginType, "GetConfigExampleValue"),
                    ToolCount = x.IsMultiTool ? x.Tools.Count : 1,
                    Tools = PluginTypeHelper.BuildToolItems(x),
                    AvatarPath = meta?.AvatarPath ?? string.Empty,
                    Version = meta?.Version ?? string.Empty,
                    CategoryName = meta?.CategoryName ?? string.Empty,
                };
            })
            .ToList();

        return new QueryPluginListCommandResponse { Items = items };
    }
}
