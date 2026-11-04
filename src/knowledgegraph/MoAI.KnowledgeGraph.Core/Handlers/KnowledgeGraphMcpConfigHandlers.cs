using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Services;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Models;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// 知识图谱 MCP 配置响应组装：开关 + 该图的团队资源 key（开启过 MCP 才有）.
/// </summary>
internal static class KnowledgeGraphMcpConfigResponseMapper
{
    /// <summary>
    /// 组装响应.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="graph">图谱（含开关字段）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回响应模型.</returns>
    public static async Task<KnowledgeGraphMcpConfigResponse> BuildAsync(DatabaseContext databaseContext, Database.Entities.KnowledgeGraphEntity graph, CancellationToken cancellationToken)
    {
        var response = new KnowledgeGraphMcpConfigResponse
        {
            IsMcpEnabled = graph.IsMcpEnabled,
        };

        if (graph.IsMcpEnabled)
        {
            var key = await databaseContext.TeamResourceKeys
                .Where(x => x.TeamId == graph.TeamId && x.ResourceType == TeamResourceType.KnowledgeGraph && x.ResourceId == graph.Id.ToString())
                .Select(x => new { x.Id, x.Key, x.KeyPrefix, x.Status })
                .FirstOrDefaultAsync(cancellationToken);
            if (key != null)
            {
                response.TeamResourceKeyId = key.Id;
                response.Key = key.Key;
                response.KeyPrefix = key.KeyPrefix;
                response.Status = key.Status;
            }
        }

        return response;
    }
}

/// <summary>
/// <inheritdoc cref="UpdateKnowledgeGraphMcpConfigCommand"/>
/// </summary>
public class UpdateKnowledgeGraphMcpConfigCommandHandler : IRequestHandler<UpdateKnowledgeGraphMcpConfigCommand, KnowledgeGraphMcpConfigResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateKnowledgeGraphMcpConfigCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="authorizer">权限判定.</param>
    public UpdateKnowledgeGraphMcpConfigCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
    }

    /// <inheritdoc/>
    public async Task<KnowledgeGraphMcpConfigResponse> Handle(UpdateKnowledgeGraphMcpConfigCommand request, CancellationToken cancellationToken)
    {
        var (graph, _) = await _authorizer.AuthorizeAsync(request.KnowledgeGraphId, adminOnly: true, cancellationToken);

        // MCP 接入仅托管图可用：接入图（connected）不对外暴露
        if (graph.Mode != KnowledgeGraphModes.Managed)
        {
            throw new BusinessException("仅托管图支持 MCP 接入.") { StatusCode = 400 };
        }

        graph.IsMcpEnabled = request.IsMcpEnabled;
        await _databaseContext.SaveChangesAsync(cancellationToken);

        // 开启 MCP 即自动生成/复用该图的团队资源 key（查库无则写、有则复用；一个图谱一把）
        if (graph.IsMcpEnabled)
        {
            await TeamResourceKeyEnsurer.EnsureAsync(_databaseContext, graph.TeamId, TeamResourceType.KnowledgeGraph, graph.Id.ToString(), cancellationToken);
        }

        return await KnowledgeGraphMcpConfigResponseMapper.BuildAsync(_databaseContext, graph, cancellationToken);
    }
}

/// <summary>
/// <inheritdoc cref="QueryKnowledgeGraphMcpConfigCommand"/>
/// </summary>
public class QueryKnowledgeGraphMcpConfigCommandHandler : IRequestHandler<QueryKnowledgeGraphMcpConfigCommand, KnowledgeGraphMcpConfigResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryKnowledgeGraphMcpConfigCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="authorizer">权限判定.</param>
    public QueryKnowledgeGraphMcpConfigCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
    }

    /// <inheritdoc/>
    public async Task<KnowledgeGraphMcpConfigResponse> Handle(QueryKnowledgeGraphMcpConfigCommand request, CancellationToken cancellationToken)
    {
        var (graph, _) = await _authorizer.AuthorizeAsync(request.KnowledgeGraphId, adminOnly: true, cancellationToken);
        return await KnowledgeGraphMcpConfigResponseMapper.BuildAsync(_databaseContext, graph, cancellationToken);
    }
}
