// <copyright file="MainModule.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Maomi;
using Maomi.I18n;
using MoAI.Admin;
using MoAI.Database;
using MoAI.Filters;
using MoAI.Infra;
using MoAI.Login;
using MoAI.Modules;
using MoAI.Common;
using MoAI.Storage;
using MoAI.User;

namespace MoAI;

/// <summary>
/// MainModule.
/// </summary>
[InjectModule<InfraCoreModule>]
[InjectModule<DatabaseCoreModule>]
[InjectModule<StorageCoreModule>]
[InjectModule<PublicCoreModule>]
[InjectModule<LoginCoreModule>]
[InjectModule<AdminCoreModule>]
[InjectModule<UserCoreModule>]
[InjectModule<ApiModule>]
public partial class MainModule : IModule
{
    private readonly IConfiguration _configuration;
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainModule"/> class.
    /// </summary>
    /// <param name="configuration"></param>
    public MainModule(IConfiguration configuration)
    {
        _configuration = configuration;
        _systemOptions = configuration.Get<SystemOptions>()!;
    }

    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        // 添加HTTP上下文访问器
        context.Services.AddHttpContextAccessor();
        context.Services.AddExceptionHandler<MaomiExceptionHandler>();
        context.Services.AddI18nAspNetCore();
    }
}