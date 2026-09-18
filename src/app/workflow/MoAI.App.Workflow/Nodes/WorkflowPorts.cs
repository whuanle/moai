using System.Text.Json.Nodes;

namespace MoAI.App.Workflow.Nodes;

/// <summary>
/// AI 对话客户端抽象 - AI 对话节点通过此接口调用模型服务.
/// 引擎不绑定具体模型 SDK（SemanticKernel 等），迁移到正式项目时注入真实实现即可.
/// </summary>
public interface IAiChatClient
{
    /// <summary>
    /// 发起一次对话补全.
    /// </summary>
    /// <param name="systemPrompt">系统提示词（可为空）.</param>
    /// <param name="prompt">用户消息.</param>
    /// <param name="history">历史消息（可选，[{role, content}]）.</param>
    /// <param name="model">模型标识（可为空，由实现方决定默认模型）.</param>
    /// <param name="onProgress">流式输出回调（可为空）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>模型回答文本.</returns>
    Task<string> CompleteAsync(
        string? systemPrompt,
        string prompt,
        JsonArray? history,
        string? model,
        Func<string, Task>? onProgress,
        CancellationToken cancellationToken);
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
