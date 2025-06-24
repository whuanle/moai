// <copyright file="EncryptionEndpoint.cs" company="MaomiAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.Public.Endpoints;

/// <summary>
/// 加密接口，通过.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/encryption")]
[AllowAnonymous]
public class EncryptionEndpoint : Endpoint<SimpleString, SimpleString>
{
    private readonly IRsaProvider _rsaProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="EncryptionEndpoint"/> class.
    /// </summary>
    /// <param name="rsaProvider"></param>
    public EncryptionEndpoint(IRsaProvider rsaProvider)
    {
        _rsaProvider = rsaProvider;
    }

    /// <inheritdoc/>
    public override Task<SimpleString> ExecuteAsync(SimpleString request, CancellationToken ct)
    {
        return Task.FromResult(new SimpleString
        {
            Data = _rsaProvider.Encrypt(request.Data)
        });
    }
}
