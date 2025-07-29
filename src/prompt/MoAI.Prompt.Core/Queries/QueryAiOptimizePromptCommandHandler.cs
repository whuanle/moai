// <copyright file="QueryAiOptimizePromptCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AI.Commands;
using MoAI.AiModel.Models;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Prompt.Queries.Responses;

namespace MoAI.Prompt.Queries;

/// <summary>
/// <inheritdoc cref="AiOptimizePromptCommand"/>
/// </summary>
public class QueryAiOptimizePromptCommandHandler : IRequestHandler<AiOptimizePromptCommand, QueryAiOptimizePromptCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAiOptimizePromptCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryAiOptimizePromptCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryAiOptimizePromptCommandResponse> Handle(AiOptimizePromptCommand request, CancellationToken cancellationToken)
    {
        // 检查用户是否有权使用此 ai 模型

        var aiModel = await _databaseContext.AiModels
            .Where(x => x.Id == request.AiModelId)
            .FirstOrDefaultAsync();

        if (aiModel == null)
        {
            throw new BusinessException("未找到可用 ai 模型");
        }

        if (aiModel.IsSystem)
        {
            if (!aiModel.IsPublic)
            {
                throw new BusinessException("未找到可用 ai 模型");
            }
        }
        else
        {
            if (aiModel.CreateUserId != request.UserId)
            {
                throw new BusinessException("未找到可用 ai 模型");
            }
        }

        var aiEndpoint = new AiEndpoint
        {
            Name = aiModel.Name,
            DeploymentName = aiModel.DeploymentName,
            Title = aiModel.Title,
            AiModelType = Enum.Parse<AiModelType>(aiModel.AiModelType, true),
            Provider = Enum.Parse<AiProvider>(aiModel.AiProvider, true),
            ContextWindowTokens = aiModel.ContextWindowTokens,
            Endpoint = aiModel.Endpoint,
            Key = aiModel.Key,
            Abilities = new ModelAbilities
            {
                Files = aiModel.Files,
                FunctionCall = aiModel.FunctionCall,
                ImageOutput = aiModel.ImageOutput,
                Vision = aiModel.IsVision,
            },
            MaxDimension = aiModel.MaxDimension,
            TextOutput = aiModel.TextOutput
        };

        var result = await _mediator.Send(new OneSimpleChatCommand
        {
            Endpoint = aiEndpoint,
            Question = request.SourcePrompt,
            Prompt = Prompt
        });

        return new QueryAiOptimizePromptCommandResponse
        {
            Content = result.Content,
            Useage = result.Useage
        };
    }

    private const string Prompt =
        """
        提示词生成器

        目标：将用户提供的简单提示词转化为详细专业的提示词，提示词不要超过 2000 字。

        输入指南：

        背景信息 ：请尽可能多地收集与主题相关的信息，包括目标受众、使用场景和期望的输出形式。
        核心主题 ：明确用户输入的核心主题或关键字。
        意图与目标 ：理解用户的意图（如：提高效率、获得灵感、生成内容等）和目标效果。
        输出要求：

        结构化内容 ：确保输出的提示词有清晰的结构，包括引言、主体部分和结论。
        细节与深度 ：根据背景信息和核心主题，补充细节，使提示词具有专业性和说服力。
        明确指示 ：输出提示词应包括明确的指示，指导 AI 完成特定任务。
        相关示例 ：提供适当的示例或用例，以帮助理解和应用提示词。
        示例任务：
        用户输入：“帮我写一个关于环保的重要性的文章。”

        生成专业提示词：
        请写一篇关于环保重要性的文章，目标受众为青少年。文章应包括以下内容：

        引言 ：简要介绍环保的重要性并说明为什么青少年应该关注这个主题。
        当前环境现状 ：描述当前地球环境的状态，包括气候变化、污染等问题。
        环保的重要性 ：详细阐述环保对于地球和人类未来的重要性，并结合具体例子说明（如：动物栖息地的丧失、海洋污染）。
        行动措施 ：提出青少年可以采取的具体环保行动，如减少使用塑料、参加环保活动等。
        结论 ：总结文章内容，并呼吁青少年积极参与环保实践。

        请遵循以上格式生成新的专业提示词。根据用户输入的信息和意图，适当地进行调整和内容扩展。
        """;
}
