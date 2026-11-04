using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIPlugin.Queries;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Team.Services;
using MoAI.TeamPlugin.Queries;

namespace MoAI.TeamPlugin.Handlers;

/// <summary>
/// <inheritdoc cref="QueryTeamPluginSemanticCommand"/> 校验团队成员与团队自有归属后，转发 aiplugin 语义配置查询.
/// </summary>
public class QueryTeamPluginSemanticCommandHandler : IRequestHandler<QueryTeamPluginSemanticCommand, AIPlugin.Queries.Responses.QueryPluginSemanticCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTeamPluginSemanticCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="mediator">消息中介器，转发 aiplugin 语义配置查询.</param>
    public QueryTeamPluginSemanticCommandHandler(DatabaseContext databaseContext, ITeamService teamService, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<AIPlugin.Queries.Responses.QueryPluginSemanticCommandResponse> Handle(QueryTeamPluginSemanticCommand request, CancellationToken cancellationToken)
    {
        var myRole = await _teamService.GetMyRoleAsync(request.TeamId, request.ContextUserId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        await EnsureTeamOwnedPluginAsync(request.TeamId, request.PluginId, cancellationToken);

        return await _mediator.Send(new AIPlugin.Queries.QueryPluginSemanticCommand { PluginId = request.PluginId }, cancellationToken);
    }

    /// <summary>
    /// 语义配置仅限团队自有插件：系统授权插件与其它团队插件的语义属全局/他团队数据，一律拒绝.
    /// </summary>
    internal async Task EnsureTeamOwnedPluginAsync(long teamId, Guid pluginId, CancellationToken cancellationToken)
    {
        var plugin = await _databaseContext.Plugins
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == pluginId, cancellationToken)
            ?? throw new BusinessException("插件不存在") { StatusCode = 404 };

        if (plugin.TeamId != teamId)
        {
            throw new BusinessException("只能配置本团队自有插件的语义配置") { StatusCode = 403 };
        }
    }
}
