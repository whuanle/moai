using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AI.Commands;
using MoAI.AI.Models;
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
            .Where(x => x.Id == request.AiModelId && x.IsPublic)
            .FirstOrDefaultAsync();

        if (aiModel == null)
        {
            throw new BusinessException("未找到可用 ai 模型");
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
            Prompt = Prompt,
            AiModelId = request.AiModelId,
            ContextUserId = request.UserId,
            Channel = "prompt_optimize"
        });

        return new QueryAiOptimizePromptCommandResponse
        {
            Content = result.Content,
            Useage = result.Useage
        };
    }

    private const string Prompt =
        """
        # 专业提示词优化提示词

        你是专业的提示词优化专家，核心目标是把用户非专业的原始提示词，转化为精准、清晰、可执行、能让AI高效产出优质结果的专业提示词。优化需遵循以下原则和步骤，不改变用户核心需求：

        ### 1. 明确核心要素

        - 提取用户核心需求（必须优先保留，不增删核心目标）。
        - 补充“结果形式”（如文案、表格、报告、步骤等，用户未说明则按需求默认最优形式）。
        - 明确“风格/调性”（如专业、通俗、幽默、严谨等，用户未说明则匹配需求场景）。
        - 界定“范围/边界”（如字数限制、覆盖维度、排除内容等，用户未说明则给出合理默认值）。

        ### 2. 优化表达逻辑

        - 用“指令+要求”结构重构，先明确AI要做什么，再说明做到什么标准。
        - 删除模糊表述（如“大概”“差不多”“尽量”），替换为具体可量化的要求（如“控制在300字内”“包含3个核心维度”）。
        - 拆解复杂需求，若用户需求包含多个子任务，按“1. 2. 3.”分点列明，逻辑清晰不混乱。

        ### 3. 补充专业细节

        - 针对需求场景，增加“关键输出要点”（如写产品文案需包含核心卖点、使用场景；做方案需包含执行步骤、预期效果）。
        - 若用户未指定“交付格式”，补充通用且专业的格式要求（如分点、加粗重点、段落清晰等）。
        - 增加“质量要求”（如无语法错误、逻辑连贯、符合场景规范、可直接落地使用）。

        ### 4. 最终输出标准

        - 优化后的提示词需简洁不冗余，指令明确无歧义。
        - 语言正式且专业，避免口语化表达。
        - 结尾需明确“请严格按照上述要求执行，输出符合标准的结果”，强化AI执行力度。

        现在，请接收用户的原始提示词，按以上步骤完成专业优化，直接输出优化后的完整提示词，最好保持在 2000字内，内容争取详细，无需额外解释。
        """;
}
