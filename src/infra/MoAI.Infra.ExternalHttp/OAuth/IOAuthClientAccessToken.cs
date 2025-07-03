// <copyright file="IOAuthClientAccessToken.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MoAI.Infra.OAuth.Models;
using Refit;

namespace MoAI.Infra.OAuth;

public interface IOAuthClientAccessToken
{
    public HttpClient Client { get; }

    [Post("/{**path}")]
    [QueryUriFormat(UriFormat.Unescaped)]
    Task<OpenIdAuthorizationResponse> GetAccessTokenAsync(string path, [Body] OpenIdAuthorizationRequest request);

    [Get("/{**path}")]
    [QueryUriFormat(UriFormat.Unescaped)]
    Task<OpenIdUserProfile> GetUserInfoAsync(string path, [Query] string accessToken);
}
