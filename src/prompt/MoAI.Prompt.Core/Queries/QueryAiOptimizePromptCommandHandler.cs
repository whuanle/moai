using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AI.Commands;
using MoAI.AI.Models;
using MoAI.AiModel.Queries;
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
        var aiEndpoint = await _mediator.Send(new QueryAiModelToAiEndpointCommand { AiModelId = request.AiModelId, IsPublic = true });

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
        用户需要撰写一份高质量的人工智能提示词，但他们不是专业的开发者。请帮助用户优化他们的提示词内容，确保其清晰、结构化且具有可操作性。直接输出优化后的完整提示词，保持在2000-3000字内，无需额外解释，直接输出内容。
        需要注意的地方包括但不限于以下几点：
        ### 1. 明确核心要素
        - **提取用户核心需求**：确保核心目标明确且不增删。
        - **补充结果形式**：如果用户未说明，按需求默认最优形式（如文案、表格、报告、步骤等）。
        - **明确风格/调性**：如果用户未说明，匹配需求场景（如专业、通俗、幽默、严谨等）。
        - **界定范围/边界**：如果用户未说明，给出合理默认值（如字数限制、覆盖维度、排除内容等）。

        ### 2. 优化表达逻辑
        - **指令+要求结构**：先明确AI要做什么，再说明做到什么标准。
        - **删除模糊表述**：替换为具体可量化的要求（如“控制在300字内”“包含3个核心维度”）。
        - **拆解复杂需求**：若用户需求包含多个子任务，按“1. 2. 3.”分点列明，逻辑清晰不混乱。

        ### 3. 补充专业细节
        - **关键输出要点**：针对需求场景，增加关键输出要点（如写产品文案需包含核心卖点、使用场景；做方案需包含执行步骤、预期效果）。
        - **交付格式**：如果用户未指定，补充通用且专业的格式要求（如分点、加粗重点、段落清晰等）。
        - **质量要求**：增加质量要求（如无语法错误、逻辑连贯、符合场景规范、可直接落地使用）。

        ### 4. 最终输出标准
        - **简洁不冗余**：优化后的提示词需简洁不冗余，指令明确无歧义。
        - **语言正式且专业**：避免口语化表达。
        - **结尾明确**：请严格按照上述要求执行，输出符合标准的结果，强化AI执行力度。

        请接收用户的原始提示词，按以上步骤完成专业优化，直接输出优化后的完整提示词，保持在2000字内，内容争取详细，无需额外解释。
        """;
}
