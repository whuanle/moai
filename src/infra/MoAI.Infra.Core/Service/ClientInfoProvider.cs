// <copyright file="ClientInfoProvider.cs" company="MaomiAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Maomi;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using Microsoft.AspNetCore.Http;

namespace MoAI.Infra.Defaults;

[InjectOnScoped]
public class ClientInfoProvider : IClientInfoProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly Lazy<ClientInfo> _clientInfo;

    public ClientInfoProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
        _clientInfo = new Lazy<ClientInfo>(() =>
         {
             return ParseClientInfo();
         });
    }

    public ClientInfo GetClientInfo() => _clientInfo.Value;

    private ClientInfo ParseClientInfo()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null)
        {
            throw new BusinessException("HttpContext is not available.");
        }

        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var userAgent = context.Request.Headers["User-Agent"].FirstOrDefault() ?? "Unknown";

        return new ClientInfo
        {
            IP = ip,
            UserAgent = userAgent
        };
    }
}
