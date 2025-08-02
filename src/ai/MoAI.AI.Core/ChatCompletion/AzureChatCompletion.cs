// <copyright file="AzureChatCompletion.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

#pragma warning disable SKEXP0070 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。

using Maomi;
using Microsoft.SemanticKernel;
using MoAI.AI.Abstract;
using MoAI.AiModel.Models;

namespace MoAI.AI.ChatCompletion;

[InjectOnScoped(ServiceKey = AiProvider.Azure)]
public class AzureChatCompletion : IChatCompletionConfigurator
{
    public IKernelBuilder Configure(IKernelBuilder kernelBuilder, AiEndpoint endpoint)
    {
        return kernelBuilder.AddAzureOpenAIChatCompletion(
                deploymentName: endpoint.DeploymentName,
                apiKey: endpoint.Key,
                endpoint: endpoint.Endpoint,
                modelId: endpoint.Name,
                serviceId: "MoAI");
    }
}
