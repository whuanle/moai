using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Commands;
using MoAI.App.Services;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Prompt.Services;
using MoAI.Team.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateAppSessionPromptCommand"/>
/// </summary>
public class UpdateAppSessionPromptCommandHandler : IRequestHandler<UpdateAppSessionPromptCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IPromptUsageCounter _promptUsageCounter;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateAppSessionPromptCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="promptUsageCounter">提示词使用次数计数器.</param>
    public UpdateAppSessionPromptCommandHandler(DatabaseContext databaseContext, ITeamService teamService, IPromptUsageCounter promptUsageCounter)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _promptUsageCounter = promptUsageCounter;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateAppSessionPromptCommand request, CancellationToken cancellationToken)
    {
        var session = await _databaseContext.AppAgentSessions
            .FirstOrDefaultAsync(x => x.Id == request.SessionId, cancellationToken);

        if (session == null || session.CreateUserId != request.ContextUserId)
        {
            throw new BusinessException("会话不存在.") { StatusCode = 404 };
        }

        if (request.PromptId != 0)
        {
            await SessionPromptHelper.EnsureUsableAsync(_databaseContext, _teamService, request.PromptId, session.TeamId, request.ContextUserId, cancellationToken);

            // 选用专家 +1 使用次数（Redis 计数，统计模块批量落库；top_used 推荐依据）；可用性已在上面校验
            await _promptUsageCounter.IncrementAsync(request.PromptId, cancellationToken);
        }

        session.PromptId = request.PromptId;
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
