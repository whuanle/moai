// <copyright file="PluginCoreModule.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Maomi;

namespace MoAI.Plugin;

/// <summary>
/// PluginCoreModule.
/// </summary>
[InjectModule<PluginApiModule>]
public class PluginCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
    }
}
