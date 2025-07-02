// <copyright file="ApiModule.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Maomi;

namespace MoAI.Modules;

/// <summary>
/// 聚合 API 项目中的各个子模块.
/// </summary>
[InjectModule<ConfigureLoggerModule>]
[InjectModule<ConfigureAuthorizaModule>]
[InjectModule<ConfigureMVCModule>]
[InjectModule<FastEndpointModule>]
[InjectModule<ConfigureMediatRModule>]
[InjectModule<MessageModule>]
public class ApiModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
    }
}
