using System;
using System.Threading;
using System.Threading.Tasks;

namespace MoAI.AI.Services;

/// <summary>
/// 流程应用对话调用请求.
/// </summary>
public class WorkflowAppChatRequest
{
    /// <summary>
    /// 应用 id.
    /// </summary>
    public Guid AppId { get; set; }

    /// <summary>
    /// 所属团队 id.
    /// </summary>
    public long TeamId { get; set; }

    /// <summary>
    /// 发起对话的用户 id.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 会话 id（对话轮次归属的会话）.
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// 本轮用户消息文本.
    /// </summary>
    public string Query { get; set; } = string.Empty;
}

/// <summary>
/// 流程应用对话调用结果.
/// </summary>
public class WorkflowAppChatResult
{
    /// <summary>
    /// 是否执行完成（实例到达 Completed 态）.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 本轮流程实例 id.
    /// </summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// AI 回复文本（结束节点输出中提取）.
    /// </summary>
    public string Reply { get; set; } = string.Empty;

    /// <summary>
    /// 失败/挂起原因.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 流程应用对话执行端口：AI 会话层把一轮用户消息交给流程引擎执行并取回回复文本.
/// 实现在 MoAI.App.Workflow.Core（能访问流程引擎与会话消息），本层仅依赖接口.
/// </summary>
public interface IWorkflowAppChatInvoker
{
    /// <summary>
    /// 执行一轮流程应用对话：用户消息作为启动参数驱动已发布流程，结束后提取回复.
    /// </summary>
    /// <param name="request">调用请求.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>执行结果.</returns>
    Task<WorkflowAppChatResult> InvokeAsync(WorkflowAppChatRequest request, CancellationToken cancellationToken = default);
}
