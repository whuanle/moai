// <copyright file="UpdateAiModelCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AiModel.Commands;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Service;
using MoAI.Infra.Services;

namespace MoAI.AiModel.Handlers;

/// <summary>
/// 更新模型.
/// </summary>
public class UpdateAiModelCommandHandler : IRequestHandler<UpdateAiModelCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _dbContext;
    private readonly IRsaProvider _rsaProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateAiModelCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    /// <param name="rsaProvider"></param>
    public UpdateAiModelCommandHandler(DatabaseContext dbContext, IRsaProvider rsaProvider)
    {
        _dbContext = dbContext;
        _rsaProvider = rsaProvider;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateAiModelCommand request, CancellationToken cancellationToken)
    {
        var aiModel = await _dbContext.AiModels
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.AiModelId, cancellationToken);

        if (aiModel == null)
        {
            throw new BusinessException("模型不存在") { StatusCode = 404 };
        }

        if (aiModel.Name != request.Name)
        {
            var existModel = await _dbContext.AiModels
                .AnyAsync(x => x.Name == request.Name, cancellationToken);

            if (existModel)
            {
                throw new BusinessException("已存在同名模型") { StatusCode = 409 };
            }
        }

        if (!string.IsNullOrEmpty(request.Key) && request.Key.Distinct().Count() > 1)
        {
            try
            {
                string skKey = _rsaProvider.Decrypt(request.Key);
            }
            catch (Exception ex)
            {
                _ = ex;
                throw new BusinessException("key未正确加密") { StatusCode = 400 };
            }
        }

        aiModel.Name = request.Name;
        aiModel.DeploymentName = request.DeploymentName;
        aiModel.Title = request.Title;
        aiModel.AiModelType = request.AiModelType.ToString();
        aiModel.AiProvider = request.Provider.ToString();
        aiModel.ContextWindowTokens = request.ContextWindowTokens;
        aiModel.Endpoint = request.Endpoint;
        aiModel.MaxDimension = request.MaxDimension;
        aiModel.TextOutput = request.TextOutput;
        aiModel.FunctionCall = request.Abilities?.FunctionCall ?? false;
        aiModel.Files = request.Abilities?.Files ?? false;
        aiModel.ImageOutput = request.Abilities?.ImageOutput ?? false;
        aiModel.IsVision = request.Abilities?.Vision ?? false;

        _dbContext.AiModels.Update(aiModel);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}