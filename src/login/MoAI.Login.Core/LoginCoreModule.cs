// <copyright file="LoginCoreModule.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Maomi;
using MoAI.Login.Http;
using Refit;
using System.Text.Json;

namespace MoAI.Login;

/// <summary>
/// LoginCoreModule.
/// </summary>
[InjectModule<LoginApiModule>]
public class LoginCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        var settings = new RefitSettings(new SystemTextJsonContentSerializer(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
        }))
        {
            Buffered = true
        };

        // todo: 统一 http 拦截器

        context.Services.AddRefitClient<IOAuthClient>(settings);
        context.Services.AddRefitClient<IOAuthClientAccessToken>(settings);
    }
}
