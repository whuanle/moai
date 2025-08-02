// <copyright file="DeleteAiAssistantChatOneRecordCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.App.AIAssistant.Commands;
using MoAI.Database;
using MoAI.Infra.Models;

namespace MoAI.App.AIAssistant.Handlers;

public class DeleteAiAssistantChatOneRecordCommandHandler : IRequestHandler<DeleteAiAssistantChatOneRecordCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteAiAssistantChatOneRecordCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public DeleteAiAssistantChatOneRecordCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteAiAssistantChatOneRecordCommand request, CancellationToken cancellationToken)
    {
        await _databaseContext.SoftDeleteAsync(_databaseContext.AppAssistantChatHistories.Where(x => x.Id == request.RecordId));
        return EmptyCommandResponse.Default;
    }
}
