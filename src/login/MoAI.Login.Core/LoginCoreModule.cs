// <copyright file="LoginCoreModule.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Login.Services;

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
        context.Services.AddScoped<IUserContextProvider, UserContextProvider>();
        context.Services.AddScoped<UserContext>(s =>
        {
            return s.GetRequiredService<IUserContextProvider>().GetUserContext();
        });

        context.Services.AddScoped<CustomAuthorizaMiddleware>();
    }
}
