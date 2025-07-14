// <copyright file="DeepSeekChatCompletion.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Maomi;
using Microsoft.SemanticKernel;
using MoAI.AI.Abstract;
using MoAI.AiModel.Models;
using OpenAI;
using System.ClientModel;

namespace MoAI.AI.ChatCompletion;

[InjectOnScoped(ServiceKey = AiProvider.DeepSeek)]
public class DeepSeekChatCompletion : IChatCompletionConfigurator
{
    public IKernelBuilder Configure(IKernelBuilder kernelBuilder, AiEndpoint endpoint)
    {
        var openAIClientCredential = new ApiKeyCredential(endpoint.Key);
        var openAIClientOption = new OpenAIClientOptions
        {
            Endpoint = new Uri(endpoint.Endpoint),
        };

        var openapiClient = new OpenAIClient(openAIClientCredential, openAIClientOption);
        return kernelBuilder
            .AddOpenAIChatCompletion(endpoint.Name, openapiClient, serviceId: "MoAI");
    }
}