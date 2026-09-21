using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;
using MoAI.Wiki.Commands;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateWikiWorkflowCommand"/>
/// </summary>
public class UpdateWikiWorkflowCommandHandler : IRequestHandler<UpdateWikiWorkflowCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateWikiWorkflowCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public UpdateWikiWorkflowCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateWikiWorkflowCommand request, CancellationToken cancellationToken)
    {
        var wiki = await _databaseContext.Wikis
            .FirstOrDefaultAsync(x => x.Id == request.WikiId && x.IsDeleted == 0, cancellationToken);

        if (wiki == null)
        {
            throw new BusinessException("知识库不存在.") { StatusCode = 404 };
        }

        var myRole = await _teamService.GetMyRoleAsync(wiki.TeamId, request.ContextUserId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以修改工作流配置.") { StatusCode = 403 };
        }

        // 元数据生成模型必须存在、启用且为团队可用对话模型（外部源回退该预设时同样受此约束）
        if (request.Workflow?.Metadata != null)
        {
            await EnsureMetadataModelAsync(request.Workflow.Metadata.MetadataModelId, wiki.TeamId, cancellationToken);
        }

        // 整体覆盖保存：三步全为空表示清除预设
        var workflow = request.Workflow;
        var isEmpty = workflow == null
            || (workflow.Partition == null && workflow.Metadata == null && workflow.Embedding == null);

        wiki.DefaultWorkflowConfig = isEmpty ? string.Empty : WikiWorkflowConfigJson.Serialize(workflow!);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }

    private async Task EnsureMetadataModelAsync(Guid modelId, int teamId, CancellationToken cancellationToken)
    {
        var model = await _databaseContext.AiModels
            .FirstOrDefaultAsync(x => x.Id == modelId && x.Enabled && x.IsDeleted == 0, cancellationToken);
        if (model == null)
        {
            throw new BusinessException("元数据生成模型不存在或未启用.") { StatusCode = 404 };
        }

        if (!string.Equals(model.ModelKind, "conversation", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("所选模型不是对话模型，无法用于元数据生成.") { StatusCode = 400 };
        }

        if (model.IsPublic)
        {
            return;
        }

        var authorized = await _databaseContext.AiModelAuthorizations
            .AnyAsync(x => x.AiModelId == modelId && x.TeamId == teamId, cancellationToken);
        if (!authorized)
        {
            throw new BusinessException("元数据生成模型未授权给你的团队使用.") { StatusCode = 403 };
        }
    }
}
