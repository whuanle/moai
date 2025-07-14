// <copyright file="OpenAiChatCompletion.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Maomi;
using MoAI.AiModel.Shared.Models;
using MoAI.Infra.Exceptions;
using MediatR;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI;
using StackExchange.Redis.Extensions.Core.Abstractions;
using System.ClientModel;
using MoAI.AiModel.Models;
using MoAI.AI.Abstract;

namespace MoAI.AI.ChatCompletion;

[InjectOnScoped(ServiceKey = AiProvider.OpenAI)]
public class OpenAiChatCompletion : IChatCompletionConfigurator
{
    public IKernelBuilder Configure(IKernelBuilder kernelBuilder, AiEndpoint endpoint)
    {
        return kernelBuilder.AddOpenAIChatCompletion(
            apiKey: endpoint.Key,
            endpoint: new Uri(endpoint.Endpoint),
            modelId: endpoint.Name,
            serviceId: "MaomiAI");
    }
}
