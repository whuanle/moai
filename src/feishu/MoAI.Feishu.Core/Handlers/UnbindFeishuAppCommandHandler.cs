using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Feishu.Commands;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;

namespace MoAI.Feishu.Handlers;

/// <summary>
/// <inheritdoc cref="UnbindFeishuAppCommand"/>
/// </summary>
public class UnbindFeishuAppCommandHandler : IRequestHandler<UnbindFeishuAppCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnbindFeishuAppCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public UnbindFeishuAppCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UnbindFeishuAppCommand request, CancellationToken cancellationToken)
    {
        var feishuApp = await _databaseContext.FeishuApps
            .FirstOrDefaultAsync(x => x.Id == request.FeishuAppId, cancellationToken);

        if (feishuApp == null)
        {
            throw new BusinessException("飞书应用连接不存在.") { StatusCode = 404 };
        }

        var myRole = await _teamService.GetMyRoleAsync(feishuApp.TeamId, request.ContextUserId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以管理飞书应用绑定.") { StatusCode = 403 };
        }

        var query = _databaseContext.FeishuAppBindings.Where(x => x.FeishuAppId == feishuApp.Id);

        // 订阅型渠道（知识库外部源等）可一对多，按渠道类型/id 精确解除，避免误伤同一飞书应用的其它绑定
        if (request.ChannelType.HasValue)
        {
            var channelType = (int)request.ChannelType.Value;
            query = query.Where(x => x.ChannelType == channelType);
        }

        if (!string.IsNullOrWhiteSpace(request.ChannelId))
        {
            var channelId = request.ChannelId;
            query = query.Where(x => x.ChannelId == channelId);
        }

        var bindingIds = await query.Select(x => x.Id).ToListAsync(cancellationToken);

        if (bindingIds.Count == 0)
        {
            throw new BusinessException("该飞书应用未绑定渠道.") { StatusCode = 404 };
        }

        await _databaseContext.SoftDeleteAsync(_databaseContext.FeishuAppBindings.Where(x => bindingIds.Contains(x.Id)));

        return EmptyCommandResponse.Default;
    }
}
