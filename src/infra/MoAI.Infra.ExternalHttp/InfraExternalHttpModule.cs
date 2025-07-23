// <copyright file="InfraExternalHttpModule.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Infra.DingTalk;
using MoAI.Infra.Feishu;
using MoAI.Infra.OAuth;
using MoAI.Infra.WeixinWork;
using Refit;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MoAI.Infra;

/// <summary>
/// InfraExternalHttpModule.
/// </summary>
public class InfraExternalHttpModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        var settings = new RefitSettings(new SystemTextJsonContentSerializer(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
        }))
        {
            Buffered = true
        };

        context.Services.AddTransient<ExternalHttpMessageHandler>();

        context.Services.AddRefitClient<IOAuthClient>(settings)
            .AddHttpMessageHandler<ExternalHttpMessageHandler>()
            .SetHandlerLifetime(TimeSpan.FromSeconds(30));

        context.Services.AddRefitClient<IOAuthClientAccessToken>(settings).
            AddHttpMessageHandler<ExternalHttpMessageHandler>()
            .SetHandlerLifetime(TimeSpan.FromSeconds(30));

        context.Services.AddRefitClient<IFeishuClient>(settings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://open.feishu.cn"))
            .AddHttpMessageHandler<ExternalHttpMessageHandler>()
            .SetHandlerLifetime(TimeSpan.FromSeconds(30));

        context.Services.AddRefitClient<IWeixinWorkClient>(settings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://qyapi.weixin.qq.com"))
            .AddHttpMessageHandler<ExternalHttpMessageHandler>()
            .SetHandlerLifetime(TimeSpan.FromSeconds(30));

        context.Services.AddRefitClient<IDingTalkClient>(settings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://api.dingtalk.com"))
            .AddHttpMessageHandler<ExternalHttpMessageHandler>()
            .SetHandlerLifetime(TimeSpan.FromSeconds(30));
    }
}
