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
/// <inheritdoc cref="SubsystemSsoExchangeCommand"/>
/// </summary>
public class SubsystemSsoExchangeCommandHandler : IRequestHandler<SubsystemSsoExchangeCommand, SubsystemSsoExchangeCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ISubsystemSsoCodeStore _codeStore;
    private readonly ISubsystemTokenProvider _tokenProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubsystemSsoExchangeCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="codeStore">委托码存储.</param>
    /// <param name="tokenProvider">子系统 token 服务.</param>
    public SubsystemSsoExchangeCommandHandler(DatabaseContext databaseContext, ISubsystemSsoCodeStore codeStore, ISubsystemTokenProvider tokenProvider)
    {
        _databaseContext = databaseContext;
        _codeStore = codeStore;
        _tokenProvider = tokenProvider;
    }

    /// <inheritdoc/>
    public async Task<SubsystemSsoExchangeCommandResponse> Handle(SubsystemSsoExchangeCommand request, CancellationToken cancellationToken)
    {
        // 一次性原子消费：不存在/已使用/过期一律 401，且不区分原因避免探测
        var payload = await _codeStore.ConsumeAsync(request.Code);
        if (payload == null)
        {
            throw new BusinessException("委托码无效或已过期.") { StatusCode = 401 };
        }

        // 委托码绑定子系统：仅签发对象可兑换
        if (payload.SubsystemId != request.SubsystemId)
        {
            throw new BusinessException("委托码无效或已过期.") { StatusCode = 401 };
        }

        var user = await _databaseContext.Users
            .FirstOrDefaultAsync(x => x.Id == payload.UserId, cancellationToken);
        if (user == null || user.IsDisable)
        {
            throw new BusinessException("用户不存在或已禁用.") { StatusCode = 403 };
        }

        var subsystem = await _databaseContext.Subsystems
            .FirstAsync(x => x.Id == request.SubsystemId, cancellationToken);
        return BuildResponse(_tokenProvider.GenerateUserTokens(subsystem, user.Id), user);
    }

    /// <summary>
    /// 组装兑换响应（access/refresh + 用户基本资料）.
    /// </summary>
    internal static SubsystemSsoExchangeCommandResponse BuildResponse((string AccessToken, string RefreshToken, int ExpiresIn) tokens, Database.Entities.UserEntity user)
        => new()
        {
            AccessToken = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken,
            ExpiresIn = tokens.ExpiresIn,
            TokenType = "Bearer",
            UserId = user.Id,
            UserName = user.UserName,
            NickName = user.NickName,
            AvatarPath = user.AvatarPath,
        };
}
