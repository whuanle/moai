// <copyright file="PromptCoreModule.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Maomi;
using MoAI.Prompt;

namespace MaomiAI.Prompt.Core;

/// <summary>
/// PromptCoreModule.
/// </summary>
[InjectModule<PromptApiModule>]
public class PromptCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
    }
}
