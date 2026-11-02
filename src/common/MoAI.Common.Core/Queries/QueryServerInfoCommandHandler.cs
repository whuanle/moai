using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Common.Queries.Response;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Database.Seed;
using MoAI.Infra;
using MoAI.Infra.Services;
using MoAI.Storage.Services;

namespace MoAI.Common.Queries;

/// <summary>
/// <inheritdoc cref="QueryServerInfoCommand"/>
/// </summary>
public class QueryServerInfoCommandHandler : IRequestHandler<QueryServerInfoCommand, QueryServerInfoCommandResponse>
{
    private readonly SystemOptions _systemOptions;
    private readonly IRsaProvider _rsaProvider;
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryServerInfoCommandHandler"/> class.
    /// </summary>
    /// <param name="systemOptions"></param>
    /// <param name="rsaProvider"></param>
    /// <param name="databaseContext"></param>
    public QueryServerInfoCommandHandler(SystemOptions systemOptions, IRsaProvider rsaProvider, DatabaseContext databaseContext)
    {
        _systemOptions = systemOptions;
        _rsaProvider = rsaProvider;
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryServerInfoCommandResponse> Handle(QueryServerInfoCommand request, CancellationToken cancellationToken)
    {
        var endpoint = new Uri(new Uri(_systemOptions.Server), IStorageService.StaticRoutePrefix.TrimStart('/'));

        // 网站 Logo 为公开信息（登录/注册页也要展示），缺失时回退内置默认空串
        var logoPath = await _databaseContext.Settings
            .Where(s => s.Key == SettingDefinitions.SystemLogoKey)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        // 登录页背景图同为公开信息（登录页在认证前渲染），缺失时回退空串（前端用默认背景）
        var loginBackgroundPath = await _databaseContext.Settings
            .Where(s => s.Key == SettingDefinitions.LoginBackgroundKey)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        // 网站名称仅影响前端展示，设置项为空时回退配置文件中的默认名称
        var systemName = (await _databaseContext.Settings
            .Where(s => s.Key == SettingDefinitions.SystemNameKey)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty).Trim();

        // 注册验证码开关与邮箱验证启用状态是登录/注册页在认证前渲染的公开开关信息：
        // 注册验证码要求两者同时可用，找回密码仅要求邮箱验证可用（未启用时由管理员重置）；
        // SMTP 连接配置统一在通知中心渠道维护，可用 = 总开关开启且绑定的 SMTP 渠道存在并启用
        var settingValues = await _databaseContext.Settings
            .Where(s => s.Key == SettingDefinitions.SmtpEnabledKey
                || s.Key == SettingDefinitions.SmtpChannelIdKey
                || s.Key == SettingDefinitions.RegisterEmailCodeEnabledKey)
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

        bool ResolveFlag(string key)
        {
            if (settingValues.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return string.Equals(value.Trim(), "true", StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(SettingDefinitions.Find(key)?.DefaultValue, "true", StringComparison.OrdinalIgnoreCase);
        }

        var smtpEnabled = ResolveFlag(SettingDefinitions.SmtpEnabledKey);
        var passwordResetEnabled = false;
        if (smtpEnabled
            && settingValues.TryGetValue(SettingDefinitions.SmtpChannelIdKey, out var channelIdValue)
            && long.TryParse(channelIdValue.Trim(), out var channelId)
            && channelId > 0)
        {
            // 绑定的渠道必须存在且启用（软删除由全局过滤器剔除）
            passwordResetEnabled = await _databaseContext.NotificationChannels
                .Where(c => c.Id == channelId && c.Type == (int)NotificationChannelType.Smtp)
                .Select(c => (bool?)c.Enabled)
                .FirstOrDefaultAsync(cancellationToken) == true;
        }

        return new QueryServerInfoCommandResponse
        {
            PublicStoreUrl = endpoint.ToString(),
            ServiceUrl = _systemOptions.Server,
            RsaPublic = _rsaProvider.GetPublicKey(),
            MaxUploadFileSize = _systemOptions.MaxUploadFileSize,
            Name = systemName.Length > 0 ? systemName : _systemOptions.Name,
            LogoPath = logoPath.Trim(),
            LoginBackgroundPath = loginBackgroundPath.Trim(),
            RegisterEmailCodeRequired = passwordResetEnabled && ResolveFlag(SettingDefinitions.RegisterEmailCodeEnabledKey),
            PasswordResetEnabled = passwordResetEnabled
        };
    }
}
