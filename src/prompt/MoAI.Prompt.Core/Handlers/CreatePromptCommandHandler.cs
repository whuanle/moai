using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Classify;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Prompt.Commands;
using MoAI.Team.Services;

namespace MoAI.Prompt.Handlers;

/// <summary>
/// <inheritdoc cref="CreatePromptCommand"/>
/// </summary>
public class CreatePromptCommandHandler : IRequestHandler<CreatePromptCommand, SimpleInt>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreatePromptCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public CreatePromptCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<SimpleInt> Handle(CreatePromptCommand request, CancellationToken cancellationToken)
    {
        if (request.PromptClassId > 0)
        {
            var classifyExist = await _databaseContext.Classifies
                .AnyAsync(x => x.Id == request.PromptClassId && x.Type == ClassifyTypes.Prompt, cancellationToken);

            if (!classifyExist)
            {
                throw new BusinessException("提示词分类不存在.") { StatusCode = 404 };
            }
        }

        if (request.TeamId > 0)
        {
            var myRole = await _teamService.GetMyRoleAsync(request.TeamId, request.ContextUserId, cancellationToken);

            if (myRole == null)
            {
                throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
            }

            if (myRole == TeamRole.Member)
            {
                throw new BusinessException("只有团队管理员可以创建团队提示词.") { StatusCode = 403 };
            }
        }

        var prompt = new PromptEntity
        {
            TeamId = request.TeamId,
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            Content = request.Content,
            PromptClassId = request.PromptClassId,
            AvatarPath = request.AvatarPath ?? string.Empty,
            IsPublic = false,
            Counter = 0,
        };

        _databaseContext.Prompts.Add(prompt);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new SimpleInt { Value = prompt.Id };
    }
}
