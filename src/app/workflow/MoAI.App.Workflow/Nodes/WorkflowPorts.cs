using System.Text.Json.Nodes;

namespace MoAI.App.Workflow.Nodes;

/// <summary>
/// AI 对话请求（aiChat 节点执行入参的端口契约）.
/// </summary>
public sealed class AiChatRequest
{
    /// <summary>
    /// 系统提示词（可为空）.
    /// </summary>
    public string? SystemPrompt { get; init; }

    /// <summary>
    /// 用户消息.
    /// </summary>
    public required string Prompt { get; init; }

    /// <summary>
    /// 历史消息（可选，[{role, content}]）.
    /// </summary>
    public JsonArray? History { get; init; }

    /// <summary>
    /// 模型标识（可为空，由实现方决定默认模型）.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// 采样温度 0-2（可为空，用渠道/模型默认）.
    /// </summary>
    public float? Temperature { get; init; }
}

/// <summary>
/// AI 对话客户端抽象 - AI 对话节点通过此接口调用模型服务.
/// 引擎不绑定具体模型 SDK（SemanticKernel 等），迁移到正式项目时注入真实实现即可.
/// </summary>
public interface IAiChatClient
{
    /// <summary>
    /// 发起一次模型对话补全（纯对话，不带工具；复杂 Agent 能力用 agentApp 节点编排）.
    /// </summary>
    /// <param name="request">对话请求.</param>
    /// <param name="onProgress">流式输出回调（可为空）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>模型回答文本.</returns>
    Task<string> CompleteAsync(AiChatRequest request, Func<string, Task>? onProgress, CancellationToken cancellationToken);
}

/// <summary>
/// Agent 应用调用端口 - agentApp 节点通过此接口驱动一次已发布 Agent 应用对话（一轮）.
/// 实现方负责应用归属校验与循环嵌套防护（当前流程 → Agent → 流程工具的回环）.
/// </summary>
public interface IWorkflowAgentAppClient
{
    /// <summary>
    /// 以 prompt 为用户消息驱动一次 Agent 应用对话，返回回复文本.
    /// </summary>
    /// <param name="agentAppId">Agent 应用 id.</param>
    /// <param name="prompt">用户消息.</param>
    /// <param name="history">历史消息（可选，[{role, content}]）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>回复文本.</returns>
    Task<string> InvokeAsync(Guid agentAppId, string prompt, JsonArray? history, CancellationToken cancellationToken);
}

/// <summary>
/// 工作流插件调用器抽象 - 插件节点通过此接口执行注册的插件.
/// 迁移到正式项目时对接 MoAI.Plugin 的 Native/Tool 插件体系.
/// </summary>
public interface IWorkflowPluginInvoker
{
    /// <summary>
    /// 按插件 Key 执行插件.
    /// </summary>
    /// <param name="pluginKey">插件标识.</param>
    /// <param name="parameters">插件参数（节点输入）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>插件输出.</returns>
    Task<JsonObject> InvokeAsync(string pluginKey, JsonObject parameters, CancellationToken cancellationToken);
}

/// <summary>
/// 工作流知识库检索客户端抽象 - 知识库检索节点通过此接口检索知识库.
/// 引擎不绑定知识库实现，由宿主注入（实现方负责团队归属校验与 embedding 检索）.
/// </summary>
public interface IWorkflowWikiSearchClient
{
    /// <summary>
    /// 在指定知识库集合内检索与查询最相似的切片.
    /// </summary>
    /// <param name="wikiIds">知识库 id 集合.</param>
    /// <param name="query">查询文本.</param>
    /// <param name="top">每个知识库返回条数.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>按相似度降序的命中项.</returns>
    Task<IReadOnlyList<WorkflowWikiSearchHit>> SearchAsync(IReadOnlyCollection<long> wikiIds, string query, int top, CancellationToken cancellationToken);
}

/// <summary>
/// 工作流知识图谱检索客户端抽象 - 知识图谱检索节点通过此接口检索知识图谱.
/// 引擎不绑定知识图谱实现，由宿主注入（实现方负责团队归属校验与向量检索）.
/// </summary>
public interface IWorkflowGraphSearchClient
{
    /// <summary>
    /// 在指定知识图谱集合内检索与查询最相似的实体及其一跳邻居.
    /// </summary>
    /// <param name="kgIds">知识图谱 id 集合.</param>
    /// <param name="query">查询文本.</param>
    /// <param name="top">每个知识图谱返回条数.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>按相似度降序的命中项.</returns>
    Task<IReadOnlyList<WorkflowGraphSearchHit>> SearchAsync(IReadOnlyCollection<long> kgIds, string query, int top, CancellationToken cancellationToken);
}
