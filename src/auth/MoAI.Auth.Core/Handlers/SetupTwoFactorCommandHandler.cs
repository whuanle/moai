using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Auth.Commands;
using MoAI.Auth.Commands.Responses;
using MoAI.Auth.Services;
using MoAI.Database;
using MoAI.Database.Seed;
using MoAI.Infra;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Service;
using MoAI.Infra.Services;

namespace MoAI.Auth.Handlers;

/// <summary>
/// <inheritdoc cref="SetupTwoFactorCommand"/>.
/// </summary>
public class SetupTwoFactorCommandHandler : IRequestHandler<SetupTwoFactorCommand, SetupTwoFactorCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITotpService _totpService;
    private readonly IAESProvider _aesProvider;
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetupTwoFactorCommandHandler"/> class.
    /// </summary>
    public SetupTwoFactorCommandHandler(DatabaseContext databaseContext, ITotpService totpService, IAESProvider aesProvider, SystemOptions systemOptions)
    {
        _databaseContext = databaseContext;
        _totpService = totpService;
        _aesProvider = aesProvider;
        _systemOptions = systemOptions;
    }

    /// <inheritdoc/>
    public async Task<SetupTwoFactorCommandResponse> Handle(SetupTwoFactorCommand request, CancellationToken cancellationToken)
    {
        var user = await _databaseContext.Users
            .FirstOrDefaultAsync(u => u.Id == request.ContextUserId && u.IsDeleted == 0, cancellationToken);
        if (user == null)
        {
            throw new BusinessException("用户不存在.") { StatusCode = 404 };
        }

        if (user.IsTotpEnabled)
        {
            throw new BusinessException("已开启两步验证，无需重复绑定.") { StatusCode = 400 };
        }

        var secret = _totpService.GenerateSecret();
        user.TotpSecret = _aesProvider.Encrypt(secret);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        var siteName = await ResolveSiteNameAsync(cancellationToken);
        return new SetupTwoFactorCommandResponse
        {
            Secret = secret,
            OtpauthUri = _totpService.BuildOtpauthUri(secret, user.Email, siteName)
        };
    }

    /// <summary>
    /// 网站名称取自系统设置，为空回退配置文件默认名称（otpauth issuer）.
    /// </summary>
    private async Task<string> ResolveSiteNameAsync(CancellationToken cancellationToken)
    {
        var name = (await _databaseContext.Settings
            .Where(s => s.Key == SettingDefinitions.SystemNameKey)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty).Trim();

        return name.Length > 0 ? name : _systemOptions.Name;
    }
}
