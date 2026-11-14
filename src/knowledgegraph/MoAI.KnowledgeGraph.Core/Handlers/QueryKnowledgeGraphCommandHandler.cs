using System.Text.Json;
using MediatR;
using MoAI.KnowledgeGraph.Models;
using MoAI.KnowledgeGraph.Queries;
using MoAI.KnowledgeGraph.Queries.Responses;
using MoAI.KnowledgeGraph.Services;
using MoAI.Settings.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="QueryKnowledgeGraphCommand"/>
/// </summary>
public class QueryKnowledgeGraphCommandHandler : IRequestHandler<QueryKnowledgeGraphCommand, QueryKnowledgeGraphCommandResponse>
{
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphSettingsService _settingsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryKnowledgeGraphCommandHandler"/> class.
    /// </summary>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="settingsService">知识图谱设置.</param>
    public QueryKnowledgeGraphCommandHandler(IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphSettingsService settingsService)
    {
        _authorizer = authorizer;
        _settingsService = settingsService;
    }

    /// <inheritdoc/>
    public async Task<QueryKnowledgeGraphCommandResponse> Handle(QueryKnowledgeGraphCommand request, CancellationToken cancellationToken)
    {
        var (graph, role) = await _authorizer.AuthorizeAsync(request.KnowledgeGraphId, adminOnly: false, cancellationToken);
        var settings = await _settingsService.GetAsync(cancellationToken);

        return new QueryKnowledgeGraphCommandResponse
        {
            KgId = graph.Id,
            TeamId = graph.TeamId,
            Name = graph.Name,
            Description = graph.Description,
            EmbeddingModelId = graph.EmbeddingModelId,
            EmbeddingDimensions = graph.EmbeddingDimensions,
            ExtractModelId = graph.ExtractModelId,
            CanvasStyle = ParseCanvasStyle(graph.CanvasStyleJson),
            MyRole = (int)role,
            Enabled = settings.Enabled,
            Mode = graph.Mode,
            Database = graph.Database,
            ReadOnly = graph.Mode == KnowledgeGraphModes.Connected,
            AvatarPath = graph.AvatarPath,
            CreateTime = graph.CreateTime,
        };
    }

    private static Dictionary<string, string>? ParseCanvasStyle(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("captions", out var captions) && captions.ValueKind == JsonValueKind.Object)
            {
                var result = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var pair in captions.EnumerateObject())
                {
                    result[pair.Name] = pair.Value.ValueKind == JsonValueKind.String ? pair.Value.GetString() ?? string.Empty : string.Empty;
                }

                return result;
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
