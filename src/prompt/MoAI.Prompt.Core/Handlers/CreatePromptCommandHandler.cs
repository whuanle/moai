// <copyright file="CreatePromptCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Models;
using MoAI.Prompt.Commands;

namespace MoAIPrompt.Core.Handlers;

/// <summary>
/// <inheritdoc cref="CreatePromptCommand"/>
/// </summary>
public class CreatePromptCommandHandler : IRequestHandler<CreatePromptCommand, SimpleInt>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreatePromptCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public CreatePromptCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<SimpleInt> Handle(CreatePromptCommand request, CancellationToken cancellationToken)
    {
        var prompt = new PromptEntity
        {
            Name = request.Name,
            Description = request.Description,
            Content = request.Content,
            PromptClassId = request.PromptClassId,
            IsPublic = request.IsPublic
        };

        await _databaseContext.Prompts.AddAsync(prompt, cancellationToken);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return prompt.Id;
    }
}
