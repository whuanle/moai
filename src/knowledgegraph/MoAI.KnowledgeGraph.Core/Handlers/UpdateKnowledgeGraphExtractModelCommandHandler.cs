using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.AIChannel.Services;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Models;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateKnowledgeGraphExtractModelCommand"/>
/// </summary>
public class UpdateKnowledgeGraphExtractModelCommandHandler : IRequestHandler<UpdateKnowledgeGraphExtractModelCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateKnowledgeGraphExtractModelCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="authorizer">权限判定.</param>
    public UpdateKnowledgeGraphExtractModelCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateKnowledgeGraphExtractModelCommand request, CancellationToken cancellationToken)
    {
        var (graph, _) = await _authorizer.AuthorizeAsync(request.KnowledgeGraphId, adminOnly: true, cancellationToken);
        if (!string.Equals(graph.Mode, KnowledgeGraphModes.Managed, StringComparison.Ordinal))
        {
            throw new BusinessException("外部接入图谱不支持抽取模型配置.") { StatusCode = 409 };
        }

        if (request.ExtractModelId == null)
        {
            // 清除默认：导入时必须显式指定模型
            graph.ExtractModelId = null;
            await _databaseContext.SaveChangesAsync(cancellationToken);
            return EmptyCommandResponse.Default;
        }

        // 与 AI 导入 ResolveChatModelAsync 同口径：启用 + 团队可用 + 对话类型
        var usableModelIds = await _databaseContext.GetTeamUsableModelIdsAsync(graph.TeamId, cancellationToken);
        var model = await (from m in _databaseContext.AiModels
                           join c in _databaseContext.AiChannels on m.ChannelId equals c.Id
                           where m.Id == request.ExtractModelId && m.Enabled && c.Enabled && m.IsDeleted == 0 && c.IsDeleted == 0
                               && usableModelIds.Contains(m.Id)
                           select new { m.Id, m.ModelKind })
                           .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BusinessException("抽取模型不存在、未启用或团队不可用.") { StatusCode = 400 };

        if (!string.Equals(model.ModelKind, "conversation", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("抽取模型必须是对话模型.") { StatusCode = 400 };
        }

        graph.ExtractModelId = model.Id;
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
