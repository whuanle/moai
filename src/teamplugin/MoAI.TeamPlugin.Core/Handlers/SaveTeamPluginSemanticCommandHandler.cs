using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;
using MoAI.TeamPlugin.Commands;

namespace MoAI.TeamPlugin.Handlers;

/// <summary>
/// <inheritdoc cref="SaveTeamPluginSemanticCommand"/> 校验团队管理员与团队自有归属后，转发 aiplugin 语义配置保存.
/// </summary>
public class SaveTeamPluginSemanticCommandHandler : IRequestHandler<SaveTeamPluginSemanticCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="SaveTeamPluginSemanticCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="mediator">消息中介器，转发 aiplugin 语义配置保存.</param>
    public SaveTeamPluginSemanticCommandHandler(DatabaseContext databaseContext, ITeamService teamService, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(SaveTeamPluginSemanticCommand request, CancellationToken cancellationToken)
    {
        var myRole = await _teamService.GetMyRoleAsync(request.TeamId, request.ContextUserId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以管理插件.") { StatusCode = 403 };
        }

        // 语义配置仅限团队自有插件：系统授权插件与其它团队插件的语义属全局/他团队数据，一律拒绝
        var plugin = await _databaseContext.Plugins
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.PluginId, cancellationToken)
            ?? throw new BusinessException("插件不存在") { StatusCode = 404 };

        if (plugin.TeamId != request.TeamId)
        {
            throw new BusinessException("只能配置本团队自有插件的语义配置") { StatusCode = 403 };
        }

        return await _mediator.Send(new AIPlugin.Commands.SavePluginSemanticCommand
        {
            PluginId = request.PluginId,
            UsageNotes = request.UsageNotes,
            Notes = request.Notes,
            ToolMetas = request.ToolMetas,
        }, cancellationToken);
    }
}
