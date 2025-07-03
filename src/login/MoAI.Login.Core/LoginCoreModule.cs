// <copyright file="LoginCoreModule.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Maomi;

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
    }
}
