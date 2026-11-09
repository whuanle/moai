using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Models;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateKnowledgeGraphCanvasStyleCommand"/>
/// </summary>
public class UpdateKnowledgeGraphCanvasStyleCommandHandler : IRequestHandler<UpdateKnowledgeGraphCanvasStyleCommand, EmptyCommandResponse>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateKnowledgeGraphCanvasStyleCommandHandler"/> class.
    /// </summary>
    public UpdateKnowledgeGraphCanvasStyleCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateKnowledgeGraphCanvasStyleCommand request, CancellationToken cancellationToken)
    {
        var (graph, _) = await _authorizer.AuthorizeAsync(request.KnowledgeGraphId, adminOnly: true, cancellationToken);
        if (!string.Equals(graph.Mode, KnowledgeGraphModes.Managed, StringComparison.Ordinal))
        {
            throw new BusinessException("外部接入图谱不支持画布样式配置.") { StatusCode = 409 };
        }

        // 类型归属校验：键必须为该图谱已存在的实体类型 id
        if (request.Captions != null && request.Captions.Count > 0)
        {
            var typeIds = request.Captions.Keys.Select(long.Parse).ToList();
            var existing = await _databaseContext.KnowledgeGraphEntityTypes
                .Where(x => x.KnowledgeGraphId == request.KnowledgeGraphId && typeIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
            var unknown = typeIds.Except(existing).ToList();
            if (unknown.Count > 0)
            {
                throw new BusinessException($"caption 规则包含不存在的实体类型：{string.Join(",", unknown)}.") { StatusCode = 400 };
            }
        }

        // 存储：null 或空字典清空规则；序列化 {captions:{...}}（null 值规整为空串）
        if (request.Captions == null || request.Captions.Count == 0)
        {
            graph.CanvasStyleJson = string.Empty;
        }
        else
        {
            var payload = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in request.Captions)
            {
                payload[pair.Key] = pair.Value ?? string.Empty;
            }

            graph.CanvasStyleJson = JsonSerializer.Serialize(new { captions = payload }, JsonOptions);
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
