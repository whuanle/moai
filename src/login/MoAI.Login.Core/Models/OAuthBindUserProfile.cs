// <copyright file="OAuthBindUserProfile.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MoAI.Infra.OAuth.Models;

namespace MoAI.Login.Models;

public class OAuthBindUserProfile
{
    public int OAuthId { get; set; } = default!;

    public string Name { get; set; } = default!;

    public OpenIdUserProfile Profile { get; set; } = default!;

    public string AccessToken { get; set; } = default!;
}
