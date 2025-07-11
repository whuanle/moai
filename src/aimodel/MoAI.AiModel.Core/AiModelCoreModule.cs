// <copyright file="AiModelCoreModule.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Maomi;
using MoAI.AiModel.Api;

namespace MoAI.AiModel.Core;

/// <summary>
/// AiModelCoreModule.
/// </summary>
[InjectModule<AiModelApiModule>]
public class AiModelCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
    }
}