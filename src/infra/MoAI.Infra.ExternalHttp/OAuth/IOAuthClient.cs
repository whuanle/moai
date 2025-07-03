// <copyright file="IOAuthClient.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MoAI.Infra.OAuth.Models;
using Refit;

namespace MoAI.Infra.OAuth;

public interface IOAuthClient
{
    public HttpClient Client { get; }

    [Get("/{**path}")]
    [QueryUriFormat(UriFormat.Unescaped)]
    Task<OpenIdConfiguration> GetWellKnownAsync(string path);
}
