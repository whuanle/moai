// <copyright file="AzureChatCompletion.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

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
