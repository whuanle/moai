// <copyright file="DeletePromptCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Models;
using MoAI.Prompt.Commands;

namespace MaomiAI.Prompt.Core.Handlers;

/// <summary>
/// <inheritdoc cref="DeletePromptCommand"/>
/// </summary>
public class DeletePromptCommandHandler : IRequestHandler<DeletePromptCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeletePromptCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public DeletePromptCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeletePromptCommand request, CancellationToken cancellationToken)
    {
        var prompt = await _databaseContext.Prompts.FirstOrDefaultAsync(x => x.Id == request.PromptId);
        if (prompt != null)
        {
            _databaseContext.Prompts.Remove(prompt);
            await _databaseContext.SaveChangesAsync(cancellationToken);
        }

        return EmptyCommandResponse.Default;
    }
}
