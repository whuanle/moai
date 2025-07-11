// <copyright file="CreatePromptClassCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Prompt.Commands;

namespace MoAI.Prompt.Handlers;

/// <summary>
/// <inheritdoc cref="CreatePromptClassCommand"/>
/// </summary>
public class CreatePromptClassCommandHandler : IRequestHandler<CreatePromptClassCommand, SimpleInt>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreatePromptClassCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public CreatePromptClassCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<SimpleInt> Handle(CreatePromptClassCommand request, CancellationToken cancellationToken)
    {
        var exist = await _databaseContext.PromptClasses
            .AnyAsync(x => x.Name == request.Name, cancellationToken);

        if (exist)
        {
            throw new BusinessException("分类已存在");
        }

        var promptClass = new PromptClassEntity
        {
            Name = request.Name,
            Description = request.Description
        };

        await _databaseContext.PromptClasses.AddAsync(promptClass);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return promptClass.Id;
    }
}
