using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Common.Queries.Response;
using MoAI.Database;
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

        // 网站名称仅影响前端展示，设置项为空时回退配置文件中的默认名称
        var systemName = (await _databaseContext.Settings
            .Where(s => s.Key == SettingDefinitions.SystemNameKey)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty).Trim();

        return new QueryServerInfoCommandResponse
        {
            PublicStoreUrl = endpoint.ToString(),
            ServiceUrl = _systemOptions.Server,
            RsaPublic = _rsaProvider.GetPublicKey(),
            MaxUploadFileSize = _systemOptions.MaxUploadFileSize,
            Name = systemName.Length > 0 ? systemName : _systemOptions.Name,
            LogoPath = logoPath.Trim()
        };
    }
}
