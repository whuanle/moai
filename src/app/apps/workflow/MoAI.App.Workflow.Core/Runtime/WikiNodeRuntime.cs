using MediatR;
using MoAI.Wiki.Documents.Queries;
using MoAI.Workflow.Enums;
using MoAI.Workflow.Models;

namespace MoAI.Workflow.Runtime;

/// <summary>
/// Wiki 节点运行时实现.
/// Wiki 节点负责查询知识库，使用语义搜索返回相关文档.
/// </summary>
public class WikiNodeRuntime : INodeRuntime
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiNodeRuntime"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 中介者，用于发送命令和查询.</param>
    public WikiNodeRuntime(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public NodeType SupportedNodeType => NodeType.Wiki;

    /// <summary>
    /// 执行 Wiki 节点逻辑.
    /// 调用知识库搜索服务，使用语义搜索查询相关文档.
    /// </summary>
    /// <param name="nodeDefine">节点定义.</param>
    /// <param name="inputs">节点输入数据，应包含 wikiId、query 等字段.</param>
    /// <param name="context">工作流上下文.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>包含搜索结果的执行结果.</returns>
    public async Task<NodeExecutionResult> ExecuteAsync(
        INodeDefine nodeDefine,
        Dictionary<string, object> inputs,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            // 1. 验证必需的输入字段
            if (!inputs.TryGetValue("wikiId", out var wikiIdObj))
            {
                return NodeExecutionResult.Failure("缺少必需的输入字段: wikiId");
            }

            if (!inputs.TryGetValue("query", out var queryObj))
            {
                return NodeExecutionResult.Failure("缺少必需的输入字段: query");
            }

            // 2. 解析输入参数
            int wikiId = Convert.ToInt32(wikiIdObj);
            string query = queryObj?.ToString() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(query))
            {
                return NodeExecutionResult.Failure("查询字符串不能为空");
            }

            // 3. 解析可选参数
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

            // 4. 构建搜索命令
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
                ContextUserId = 0, // 工作流执行时使用系统用户
                ContextUserType = Infra.Models.UserType.Normal
            };

            // 5. 执行搜索
            var searchResult = await _mediator.Send(searchCommand, cancellationToken);

            // 6. 构建输出
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
