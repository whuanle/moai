using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using MoAI.Database.Entities;

namespace MoAI.AI.Services;

/// <summary>
/// 工作流 AI 节点对话请求（aiChat / 问题分类节点的统一模型接入入参）.
/// </summary>
public class WorkflowNodeChatRequest
{
    /// <summary>
    /// 模型 id（ai_model.id）.
    /// </summary>
    public Guid ModelId { get; init; }

    /// <summary>
    /// 所属团队 id（模型可用性校验维度）.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 工作流所属应用 id（技能/沙箱工具链装配用）.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 触发用户 id.
    /// </summary>
    public long UserId { get; init; }

    /// <summary>
    /// 系统提示词（可为空）.
    /// </summary>
    public string? SystemPrompt { get; init; }

    /// <summary>
    /// 用户消息.
    /// </summary>
    public required string Prompt { get; init; }

    /// <summary>
    /// 历史消息（可选，仅本次调用内有效，不落库）.
    /// </summary>
    public IReadOnlyList<ChatMessage>? History { get; init; }

    /// <summary>
    /// 采样温度 0-2（可为空，用渠道/模型默认）.
    /// </summary>
    public float? Temperature { get; init; }

    /// <summary>
    /// 引入的技能 id（挂载为模型可调用工具，为空时不带工具）.
    /// </summary>
    public IReadOnlyList<Guid> SkillIds { get; init; } = [];

    /// <summary>
    /// 是否开启沙箱（暴露代码执行等沙箱工具）.
    /// </summary>
    public bool SandboxEnabled { get; init; }
}

/// <summary>
/// 工作流 Agent 应用节点调用请求：已通过归属/发布校验的 Agent 应用，按生效配置执行一轮对话.
/// </summary>
public class WorkflowNodeAgentAppRequest
{
    /// <summary>
    /// 目标 Agent 应用（调用方已校验：存在/未禁用/Agent 类型/同团队/已发布）.
    /// </summary>
    public required AppEntity App { get; init; }

    /// <summary>
    /// 生效配置（调用方按发布快照解析后的应用配置）.
    /// </summary>
    public required AppAgentConfigEntity Config { get; init; }

    /// <summary>
    /// 触发用户 id.
    /// </summary>
    public long UserId { get; init; }

    /// <summary>
    /// 本轮用户消息.
    /// </summary>
    public required string Prompt { get; init; }

    /// <summary>
    /// 历史消息（可选，仅本次调用内有效，不落库）.
    /// </summary>
    public IReadOnlyList<ChatMessage>? History { get; init; }
}

/// <summary>
/// 工作流 AI 节点统一模型接入端口：节点与流程模块不自行装配模型客户端，
/// 模型解析（ai_model/ai_channel、公共/团队授权）、协议客户端构建、消息组装、
/// 技能/沙箱工具链（渐进式披露）与流式输出全部由 AI 模块统一实现.
/// </summary>
public interface IWorkflowNodeAiInvoker
{
    /// <summary>
    /// 发起一次模型对话补全（SkillIds/SandboxEnabled 非空时走带工具的 Agent 执行）.
    /// </summary>
    /// <param name="request">对话请求.</param>
    /// <param name="onProgress">流式输出回调（可为空）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>模型回答文本.</returns>
    Task<string> ChatAsync(WorkflowNodeChatRequest request, Func<string, Task>? onProgress, CancellationToken cancellationToken = default);

    /// <summary>
    /// 驱动一次已校验的 Agent 应用对话（一轮，按生效配置装配工具链）.
    /// </summary>
    /// <param name="request">调用请求.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>回复文本.</returns>
    Task<string> AgentAppAsync(WorkflowNodeAgentAppRequest request, CancellationToken cancellationToken = default);
}
