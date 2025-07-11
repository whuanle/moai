// <copyright file="DeletePromptClassCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Prompt.Commands;

namespace MoAI.Prompt.Handlers;

/// <summary>
/// <inheritdoc cref="DeletePromptClassCommand"/>
/// </summary>
public class DeletePromptClassCommandHandler : IRequestHandler<DeletePromptClassCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeletePromptClassCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public DeletePromptClassCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeletePromptClassCommand request, CancellationToken cancellationToken)
    {
        var promptClass = await _databaseContext.PromptClasses.FirstOrDefaultAsync(x => x.Id == request.ClassId, cancellationToken);

        if (promptClass == null)
        {
            throw new BusinessException("提示词分类不存在.");
        }

        _databaseContext.PromptClasses.Remove(promptClass);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
