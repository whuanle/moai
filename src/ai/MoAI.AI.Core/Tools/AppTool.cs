using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MoAI.AI.Services;

/// <summary>
/// 工具调用结果.
/// </summary>
public sealed class AppToolResult
{
    /// <summary>
    /// 是否成功.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 成功时的结果数据（通常是 JSON 文本）.
    /// </summary>
    public string? Data { get; init; }

    /// <summary>
    /// 失败时的错误信息.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// 构造成功结果.
    /// </summary>
    /// <param name="data">结果数据.</param>
    /// <returns>结果.</returns>
    public static AppToolResult Ok(string data) => new() { Success = true, Data = data };

    /// <summary>
    /// 构造失败结果.
    /// </summary>
    /// <param name="error">错误信息.</param>
    /// <returns>结果.</returns>
    public static AppToolResult Fail(string error) => new() { Success = false, Error = error };
}

/// <summary>
/// 应用可用工具（插件/知识库抽象）：元数据 + 调用委托，供 Agent 动态加载与调用.
/// </summary>
public sealed class AppTool
{
    /// <summary>
    /// 供模型使用的工具名称（应用内唯一）.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 展示标题.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// 工具描述.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// 来源类型：static|dynamic|mcp|openapi|wiki.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// 工具来源资源 id（插件/流程应用 id），审批策略按其判定白名单自动放行；无来源（沙箱/知识库）为 null.
    /// </summary>
    public Guid? SourceId { get; init; }

    /// <summary>
    /// 参数示例（JSON 文本），可为空.
    /// </summary>
    public string? ParametersExample { get; init; }

    /// <summary>
    /// 惰性解析参数示例（如 MCP 需连接服务器拉取 Schema），可为空.
    /// </summary>
    public Func<CancellationToken, Task<string?>>? ResolveParametersExampleAsync { get; init; }

    /// <summary>
    /// 调用工具.
    /// </summary>
    public required Func<string?, CancellationToken, Task<AppToolResult>> InvokeAsync { get; init; }
}

/// <summary>
/// 工具来源提供者：把某一类资源（插件/知识库）转换为 <see cref="AppTool"/> 列表；新增来源实现本接口即可.
/// </summary>
public interface IAppToolProvider
{
    /// <summary>
    /// 执行顺序，越小越先执行.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// 构建该来源在当前应用下可用的工具.
    /// </summary>
    /// <param name="context">应用构建上下文.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>工具列表.</returns>
    Task<IReadOnlyList<AppTool>> GetToolsAsync(AppAgentBuildContext context, CancellationToken cancellationToken);
}
