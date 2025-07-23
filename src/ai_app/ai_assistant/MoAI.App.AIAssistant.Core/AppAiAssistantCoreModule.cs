// <copyright file="AppAiAssistantCoreModule.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Maomi;

namespace MoAI.App.AIAssistant;

[InjectModule<AppAiAssistantApiModule>]
public class AppAiAssistantCoreModule : IModule
{
    public void ConfigureServices(ServiceContext context)
    {
    }
}
