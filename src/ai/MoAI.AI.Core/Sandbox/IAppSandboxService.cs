using System;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AI.Models;

namespace MoAI.AI.Services;

/// <summary>
/// 会话沙箱上下文：定位会话所属沙箱所需的全部信息.
/// </summary>
public sealed class SandboxSessionContext
{
    /// <summary>
    /// 会话 id（threadId）.
    /// </summary>
    public required Guid SessionId { get; init; }

    /// <summary>
    /// 应用 id.
    /// </summary>
    public required Guid AppId { get; init; }

    /// <summary>
    /// 团队 id.
    /// </summary>
    public required int TeamId { get; init; }

    /// <summary>
    /// 沙箱配置.
    /// </summary>
    public required SandboxSettings Settings { get; init; }
}

/// <summary>
/// 应用会话沙箱服务：按会话惰性创建/复用/续期/销毁 OpenSandbox 沙箱，并在其中执行代码、命令与文件操作.
/// <para>实现负责隐藏 OpenSandbox SDK，向上层暴露稳定能力.</para>
/// </summary>
public interface IAppSandboxService
{
    /// <summary>
    /// 在沙箱中执行代码（Jupyter，默认 python）.
    /// </summary>
    /// <param name="context">会话沙箱上下文.</param>
    /// <param name="language">语言；空则 python.</param>
    /// <param name="code">代码.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>执行结果 JSON 文本.</returns>
    Task<string> RunCodeAsync(SandboxSessionContext context, string? language, string code, CancellationToken cancellationToken);

    /// <summary>
    /// 在沙箱中执行 shell 命令.
    /// </summary>
    /// <param name="context">会话沙箱上下文.</param>
    /// <param name="command">命令.</param>
    /// <param name="workingDirectory">工作目录；空则使用默认.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>执行结果 JSON 文本.</returns>
    Task<string> RunShellAsync(SandboxSessionContext context, string command, string? workingDirectory, CancellationToken cancellationToken);

    /// <summary>
    /// 写入文件（覆盖）.
    /// </summary>
    /// <param name="context">会话沙箱上下文.</param>
    /// <param name="path">文件路径.</param>
    /// <param name="content">文件内容.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>结果消息.</returns>
    Task<string> WriteFileAsync(SandboxSessionContext context, string path, string content, CancellationToken cancellationToken);

    /// <summary>
    /// 读取文件文本.
    /// </summary>
    /// <param name="context">会话沙箱上下文.</param>
    /// <param name="path">文件路径.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>文件内容.</returns>
    Task<string> ReadFileAsync(SandboxSessionContext context, string path, CancellationToken cancellationToken);

    /// <summary>
    /// 列出目录.
    /// </summary>
    /// <param name="context">会话沙箱上下文.</param>
    /// <param name="path">目录路径；空则根目录.</param>
    /// <param name="depth">递归深度.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>目录项 JSON 文本.</returns>
    Task<string> ListDirectoryAsync(SandboxSessionContext context, string? path, int? depth, CancellationToken cancellationToken);

    /// <summary>
    /// 删除文件.
    /// </summary>
    /// <param name="context">会话沙箱上下文.</param>
    /// <param name="path">文件路径.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>结果消息.</returns>
    Task<string> DeleteFileAsync(SandboxSessionContext context, string path, CancellationToken cancellationToken);

    /// <summary>
    /// 搜索文件.
    /// </summary>
    /// <param name="context">会话沙箱上下文.</param>
    /// <param name="path">搜索根目录.</param>
    /// <param name="pattern">匹配模式.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>匹配文件 JSON 文本.</returns>
    Task<string> SearchFilesAsync(SandboxSessionContext context, string path, string pattern, CancellationToken cancellationToken);

    /// <summary>
    /// 销毁会话对应的远端沙箱（会话删除等场景）.
    /// </summary>
    /// <param name="sessionId">会话 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task KillSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 回收孤儿沙箱：清理本系统创建但会话已不存在的沙箱.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>清理数量.</returns>
    Task<int> ReapOrphansAsync(CancellationToken cancellationToken = default);
}
