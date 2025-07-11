// <copyright file="AddAiModelCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MaomiAI.AiModel.Shared.Commands;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Helper;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.AiModel.Handlers;

/// <summary>
/// 添加模型.
/// </summary>
public class AddAiModelCommandHandler : IRequestHandler<AddAiModelCommand, SimpleInt>
{
    private readonly DatabaseContext _dbContext;
    private readonly IRsaProvider _rsaProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AddAiModelCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    /// <param name="rsaProvider"></param>
    public AddAiModelCommandHandler(DatabaseContext dbContext, IRsaProvider rsaProvider)
    {
        _dbContext = dbContext;
        _rsaProvider = rsaProvider;
    }

    /// <inheritdoc/>
    public async Task<SimpleInt> Handle(AddAiModelCommand request, CancellationToken cancellationToken)
    {
        var existModel = await _dbContext.AiModels
            .AnyAsync(x => x.Title == request.Title, cancellationToken);

        if (existModel)
        {
            throw new BusinessException("已存在同名模型") { StatusCode = 409 };
        }

        string skKey = string.Empty;
        try
        {
            skKey = _rsaProvider.Decrypt(request.Key);
        }
        catch (Exception ex)
        {
            _ = ex;
            throw new BusinessException("key未正确加密") { StatusCode = 400 };
        }

        var aiModel = new AiModelEntity
        {
            Name = request.Name,
            DeploymentName = request.DeploymentName,
            Title = request.Title,
            AiModelType = request.AiModelType.ToDBString(),
            AiProvider = request.Provider.ToDBString(),
            ContextWindowTokens = request.ContextWindowTokens,
            Endpoint = request.Endpoint,
            Key = skKey,
            MaxDimension = request.MaxDimension,
            TextOutput = request.TextOutput,
            FunctionCall = request.Abilities?.FunctionCall ?? false,
            Files = request.Abilities?.Files ?? false,
            ImageOutput = request.Abilities?.ImageOutput ?? false,
            IsVision = request.Abilities?.Vision ?? false,
        };

        await _dbContext.AddAsync(aiModel, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new SimpleInt
        {
            Value = aiModel.Id,
        };
    }
}
