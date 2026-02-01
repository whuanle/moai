using Maomi;
using MediatR;
using MoAI.Wiki.Documents.Queries;
using MoAI.Workflow.Enums;
using MoAI.Workflow.Models;

namespace MoAI.Workflow.Runtime;

/// <summary>
/// Wiki 节点运行时实现.
/// Wiki 节点负责查询知识库，使用语义搜索返回相关文档.
/// </summary>
[InjectOnTransient]
public class WikiNodeRuntime : INodeRuntime
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiNodeRuntime"/> class.
    /// </summary>
    public WikiNodeRuntime(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public NodeType SupportedNodeType => NodeType.Wiki;

    /// <inheritdoc/>
    public async Task<NodeExecutionResult> ExecuteAsync(
        Dictionary<string, object> inputs,
        INodePipeline pipeline,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!inputs.TryGetValue("wikiId", out var wikiIdObj))
            {
                return NodeExecutionResult.Failure("缺少必需的输入字段: wikiId");
            }

            if (!inputs.TryGetValue("query", out var queryObj))
            {
                return NodeExecutionResult.Failure("缺少必需的输入字段: query");
            }

            int wikiId = Convert.ToInt32(wikiIdObj);
            string query = queryObj?.ToString() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(query))
            {
                return NodeExecutionResult.Failure("查询字符串不能为空");
            }

            int? documentId = null;
            if (inputs.TryGetValue("documentId", out var documentIdObj) && documentIdObj != null)
            {
                documentId = Convert.ToInt32(documentIdObj);
            }

            double minRelevance = 0.0;
            if (inputs.TryGetValue("minRelevance", out var minRelevanceObj) && minRelevanceObj != null)
            {
                minRelevance = Convert.ToDouble(minRelevanceObj);
            }

            int limit = 30;
            if (inputs.TryGetValue("limit", out var limitObj) && limitObj != null)
            {
                limit = Convert.ToInt32(limitObj);
            }

            int aiModelId = 0;
            if (inputs.TryGetValue("aiModelId", out var aiModelIdObj) && aiModelIdObj != null)
            {
                aiModelId = Convert.ToInt32(aiModelIdObj);
            }

            bool isOptimizeQuery = false;
            if (inputs.TryGetValue("isOptimizeQuery", out var isOptimizeQueryObj) && isOptimizeQueryObj != null)
            {
                isOptimizeQuery = Convert.ToBoolean(isOptimizeQueryObj);
            }

            bool isAnswer = false;
            if (inputs.TryGetValue("isAnswer", out var isAnswerObj) && isAnswerObj != null)
            {
                isAnswer = Convert.ToBoolean(isAnswerObj);
            }

            var searchCommand = new SearchWikiDocumentTextCommand
            {
                WikiId = wikiId,
                DocumentId = documentId,
                Query = query,
                MinRelevance = minRelevance,
                Limit = limit,
                AiModelId = aiModelId,
                IsOptimizeQuery = isOptimizeQuery,
                IsAnswer = isAnswer,
                ContextUserId = 0,
                ContextUserType = Infra.Models.UserType.Normal
            };

            var searchResult = await _mediator.Send(searchCommand, cancellationToken);

            var output = new Dictionary<string, object>
            {
                ["query"] = searchResult.Query,
                ["answer"] = searchResult.Answer ?? string.Empty,
                ["resultCount"] = searchResult.SearchResult.Count,
                ["results"] = searchResult.SearchResult.Select(item => new Dictionary<string, object>
                {
                    ["chunkId"] = item.ChunkId,
                    ["sourceChunkId"] = item.SourceChunkId,
                    ["text"] = item.Text,
                    ["chunkText"] = item.ChunkText,
                    ["relevance"] = item.RecordRelevance,
                    ["documentId"] = item.DocumentId,
                    ["fileName"] = item.FileName,
                    ["fileType"] = item.FileType
                }).ToList()
            };

            return NodeExecutionResult.Success(output);
        }
        catch (Exception ex)
        {
            return NodeExecutionResult.Failure(ex);
        }
    }
}
