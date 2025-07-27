// <copyright file="UpdatePromptClassCommandHandler.cs" company="MoAI">
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
/// <inheritdoc cref="UpdatePromptClassCommand"/>
/// </summary>
public class UpdatePromptClassCommandHandler : IRequestHandler<UpdatePromptClassCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdatePromptClassCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public UpdatePromptClassCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdatePromptClassCommand request, CancellationToken cancellationToken)
    {
        var promptClass = await _databaseContext.PromptClasses.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (promptClass == null)
        {
            throw new BusinessException("提示词分类不存在.");
        }

        promptClass.Name = request.Name;

        _databaseContext.PromptClasses.Update(promptClass);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
