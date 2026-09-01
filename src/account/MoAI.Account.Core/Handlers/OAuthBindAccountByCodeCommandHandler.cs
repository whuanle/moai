using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Account.Commands;
using MoAI.Auth.Models;
using MoAI.Auth.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.Account.Handlers;

/// <summary>
/// <inheritdoc cref="OAuthBindAccountByCodeCommand"/>
/// </summary>
public class OAuthBindAccountByCodeCommandHandler : IRequestHandler<OAuthBindAccountByCodeCommand, EmptyCommandResponse>
{
    private readonly IOAuthUserProfileService _profileService;
    private readonly DatabaseContext _databaseContext;
    private readonly ILogger<OAuthBindAccountByCodeCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuthBindAccountByCodeCommandHandler"/> class.
    /// </summary>
    /// <param name="profileService"></param>
    /// <param name="databaseContext"></param>
    /// <param name="logger"></param>
    public OAuthBindAccountByCodeCommandHandler(IOAuthUserProfileService profileService, DatabaseContext databaseContext, ILogger<OAuthBindAccountByCodeCommandHandler> logger)
    {
        _profileService = profileService;
        _databaseContext = databaseContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(OAuthBindAccountByCodeCommand request, CancellationToken cancellationToken)
    {
        // 解析第三方授权 code，得到统一用户标识
        var oauthBindUserProfile = default(OAuthBindUserProfile);
        try
        {
            oauthBindUserProfile = await _profileService.GetProfileAsync(request.OAuthId, request.Code, cancellationToken);
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get openid error，oauth privider id: {}，code: {}", request.OAuthId, request.Code);
            throw new BusinessException("第三方接口错误，请联系管理员") { StatusCode = 500 };
        }

        // 检查该第三方账号是否已被其它账号绑定
        var boundUserIds = await _databaseContext.UserOauthConnections
            .Where(a => a.ProviderId == request.OAuthId && a.Sub == oauthBindUserProfile.Profile.Sub)
            .Select(a => a.UserId)
            .ToListAsync(cancellationToken);

        if (boundUserIds.Count > 0 && boundUserIds.All(x => x != request.ContextUserId))
        {
            throw new BusinessException("第三方账号已被其它账号绑定") { StatusCode = 400 };
        }

        // 已经绑定到当前账号，幂等返回
        if (boundUserIds.Contains(request.ContextUserId))
        {
            return EmptyCommandResponse.Default;
        }

        // 用户在同一供应商下不能有多个绑定记录
        var existProvider = await _databaseContext.UserOauthConnections
            .FirstOrDefaultAsync(x => x.UserId == request.ContextUserId && x.ProviderId == request.OAuthId, cancellationToken);
        if (existProvider != null && existProvider.Sub != oauthBindUserProfile.Profile.Sub)
        {
            throw new BusinessException("用户已绑定过其它账号");
        }

        // 绑定账号
        var oauthEntity = new UserOauthConnectionEntity
        {
            UserId = request.ContextUserId,
            ProviderId = request.OAuthId,
            Sub = oauthBindUserProfile.Profile.Sub,
        };

        await _databaseContext.UserOauthConnections.AddAsync(oauthEntity, cancellationToken);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
