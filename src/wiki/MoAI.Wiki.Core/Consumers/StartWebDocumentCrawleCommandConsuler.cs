// <copyright file="StartWebDocumentCrawleCommandConsuler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Maomi.MQ;
using MediatR;
using MoAI.Wiki.Consumers.Events;
using MoAI.Wiki.WebDocuments.Commands;

namespace MoAI.Wiki.Consumers;

[Consumer("crawle_document", Qos = 10)]
public class StartWebDocumentCrawleCommandConsuler : IConsumer<StartWebDocumentCrawleMessage>
{
    private readonly IMediator _mediator;
    public StartWebDocumentCrawleCommandConsuler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task ExecuteAsync(MessageHeader messageHeader, StartWebDocumentCrawleMessage message)
    {
        await _mediator.Send(message);
    }

    public Task FaildAsync(MessageHeader messageHeader, Exception ex, int retryCount, StartWebDocumentCrawleMessage message)
    {
        throw new NotImplementedException();
    }

    public Task<ConsumerState> FallbackAsync(MessageHeader messageHeader, StartWebDocumentCrawleMessage? message, Exception? ex)
    {
        throw new NotImplementedException();
    }
}