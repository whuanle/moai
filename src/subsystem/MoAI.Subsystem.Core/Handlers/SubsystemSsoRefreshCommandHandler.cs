using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Subsystem.Commands;
using MoAI.Subsystem.Core.Services;
using MoAI.Subsystem.Queries.Responses;
using MoAI.Subsystem.Services;

namespace MoAI.Subsystem.Handlers;

/// <summary>
/// <inheritdoc cref="SubsystemSsoRefreshCommand"/>
/// </summary>
public class SubsystemSsoRefreshCommandHandler : IRequestHandler<SubsystemSsoRefreshCommand, SubsystemSsoExchangeCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ISubsystemTokenProvider _tokenProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubsystemSsoRefreshCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="tokenProvider">子系统 token 服务.</param>
    public SubsystemSsoRefreshCommandHandler(DatabaseContext databaseContext, ISubsystemTokenProvider tokenProvider)
    {
        _databaseContext = databaseContext;
        _tokenProvider = tokenProvider;
    }

    /// <inheritdoc/>
    public async Task<SubsystemSsoExchangeCommandResponse> Handle(SubsystemSsoRefreshCommand request, CancellationToken cancellationToken)
    {
        if (!_tokenProvider.TryValidateUserRefreshToken(request.RefreshToken, out var subsystemId, out var userId)
            || subsystemId != request.SubsystemId)
        {
            throw new BusinessException("refresh token 无效或已过期.") { StatusCode = 401 };
        }

        // 刷新时按数据库当前状态重验：用户被删/禁用立即失效
        var user = await _databaseContext.Users
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user == null || user.IsDisable)
        {
            throw new BusinessException("用户不存在或已禁用.") { StatusCode = 403 };
        }

        var subsystem = await _databaseContext.Subsystems
            .FirstAsync(x => x.Id == request.SubsystemId, cancellationToken);
        return SubsystemSsoExchangeCommandHandler.BuildResponse(_tokenProvider.GenerateUserTokens(subsystem, user.Id), user);
    }
}
