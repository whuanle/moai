// <copyright file="QueryServerInfoCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Common.Queries.Response;
using MoAI.Database;
using MoAI.Database.Models;
using MoAI.Infra;
using MoAI.Infra.Extensions;
using MoAI.Infra.Services;

namespace MoAI.Common.Queries;

/// <summary>
/// <inheritdoc cref="QueryServerInfoCommand"/>
/// </summary>
public class QueryServerInfoCommandHandler : IRequestHandler<QueryServerInfoCommand, QueryServerInfoCommandResponse>
{
    private readonly SystemOptions _systemOptions;
    private readonly IRsaProvider _rsaProvider;
    private readonly DatabaseContext _databaseContext;
    private readonly ISystemSettingProvider _systemSettingProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryServerInfoCommandHandler"/> class.
    /// </summary>
    /// <param name="systemOptions"></param>
    /// <param name="rsaProvider"></param>
    /// <param name="databaseContext"></param>
    /// <param name="systemSettingProvider"></param>
    public QueryServerInfoCommandHandler(SystemOptions systemOptions, IRsaProvider rsaProvider, DatabaseContext databaseContext, ISystemSettingProvider systemSettingProvider)
    {
        _systemOptions = systemOptions;
        _rsaProvider = rsaProvider;
        _databaseContext = databaseContext;
        _systemSettingProvider = systemSettingProvider;
    }

    /// <inheritdoc/>
    public async Task<QueryServerInfoCommandResponse> Handle(QueryServerInfoCommand request, CancellationToken cancellationToken)
    {
        var maxUploadFileSize = await _systemSettingProvider.GetValueAsync(ISystemSettingProvider.MaxUploadFileSize.Key);
        var disableRegister = await _systemSettingProvider.GetValueAsync(ISystemSettingProvider.DisableRegister.Key);

        var endpoint = new Uri(new Uri(_systemOptions.Server), "statics");

        return new QueryServerInfoCommandResponse
        {
            PublicStoreUrl = endpoint.ToString(),
            ServiceUrl = _systemOptions.Server,
            RsaPublic = _rsaProvider.GetPublicKey(),
            MaxUploadFileSize = maxUploadFileSize.JsonToObject<int>(),
            DisableRegister = disableRegister.JsonToObject<bool>()
        };
    }
}
