using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using MoAI.AI.Models;

namespace MoAI.AI.Services;

/// <summary>
/// 沙箱工具来源：当应用开启沙箱时，暴露代码执行 / shell / 文件读写等工具（经 list_tools 渐进披露）.
/// <para>沙箱按会话惰性创建，同会话内代码、命令、文件持续存在.</para>
/// </summary>
[InjectOnScoped]
public sealed class SandboxAppToolProvider : IAppToolProvider
{
    private readonly IAppSandboxService _sandboxService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SandboxAppToolProvider"/> class.
    /// </summary>
    /// <param name="sandboxService">会话沙箱服务.</param>
    public SandboxAppToolProvider(IAppSandboxService sandboxService)
    {
        _sandboxService = sandboxService;
    }

    /// <inheritdoc/>
    public int Order => 15;

    /// <inheritdoc/>
    public Task<IReadOnlyList<AppTool>> GetToolsAsync(AppAgentBuildContext context, CancellationToken cancellationToken)
    {
        var settings = AppAgentExecutionSettings.Parse(context.Config?.ExecutionSettings);
        if (settings.Sandbox is not { Enabled: true })
        {
            return Task.FromResult<IReadOnlyList<AppTool>>([]);
        }

        var session = new SandboxSessionContext
        {
            SessionId = context.SessionId,
            AppId = context.AppId,
            TeamId = context.TeamId,
            Settings = settings.Sandbox,
        };

        IReadOnlyList<AppTool> tools =
        [
            new AppTool
            {
                Name = "sandbox_run_code",
                Title = "运行代码",
                Description = "在本会话专属的 Linux 沙箱中运行代码（Jupyter），支持 python/java/go/typescript/javascript/bash；变量与文件在同会话内持续存在。返回 stdout/stderr/结果/退出码。",
                Kind = "sandbox",
                ParametersExample = "{\"language\":\"python\",\"code\":\"print(1+1)\"}",
                InvokeAsync = async (args, ct) =>
                {
                    if (!TryRead(args, "code", out var code) || string.IsNullOrWhiteSpace(code))
                    {
                        return AppToolResult.Fail("缺少参数 code.");
                    }

                    TryRead(args, "language", out var language);
                    return AppToolResult.Ok(await _sandboxService.RunCodeAsync(session, language, code!, ct).ConfigureAwait(false));
                },
            },
            new AppTool
            {
                Name = "sandbox_run_shell",
                Title = "运行 Shell 命令",
                Description = "在本会话专属沙箱中执行 shell 命令（bash 语义），工作目录与文件在同会话内持续存在。返回 stdout/stderr/退出码。",
                Kind = "sandbox",
                ParametersExample = "{\"command\":\"ls -la\",\"cwd\":\"/workspace\"}",
                InvokeAsync = async (args, ct) =>
                {
                    if (!TryRead(args, "command", out var command) || string.IsNullOrWhiteSpace(command))
                    {
                        return AppToolResult.Fail("缺少参数 command.");
                    }

                    TryRead(args, "cwd", out var cwd);
                    return AppToolResult.Ok(await _sandboxService.RunShellAsync(session, command!, cwd, ct).ConfigureAwait(false));
                },
            },
            new AppTool
            {
                Name = "sandbox_write_file",
                Title = "写入文件",
                Description = "向沙箱写入文件（覆盖），用于准备代码/数据文件。",
                Kind = "sandbox",
                ParametersExample = "{\"path\":\"/workspace/data.txt\",\"content\":\"hello\"}",
                InvokeAsync = async (args, ct) =>
                {
                    if (!TryRead(args, "path", out var path) || string.IsNullOrWhiteSpace(path))
                    {
                        return AppToolResult.Fail("缺少参数 path.");
                    }

                    TryRead(args, "content", out var content);
                    return AppToolResult.Ok(await _sandboxService.WriteFileAsync(session, path!, content ?? string.Empty, ct).ConfigureAwait(false));
                },
            },
            new AppTool
            {
                Name = "sandbox_read_file",
                Title = "读取文件",
                Description = "读取沙箱中的文本文件。",
                Kind = "sandbox",
                ParametersExample = "{\"path\":\"/workspace/data.txt\"}",
                InvokeAsync = async (args, ct) =>
                {
                    if (!TryRead(args, "path", out var path) || string.IsNullOrWhiteSpace(path))
                    {
                        return AppToolResult.Fail("缺少参数 path.");
                    }

                    return AppToolResult.Ok(await _sandboxService.ReadFileAsync(session, path!, ct).ConfigureAwait(false));
                },
            },
            new AppTool
            {
                Name = "sandbox_list_dir",
                Title = "列出目录",
                Description = "列出沙箱目录内容。path 省略时列出根目录；depth 可递归。",
                Kind = "sandbox",
                ParametersExample = "{\"path\":\"/workspace\",\"depth\":2}",
                InvokeAsync = async (args, ct) =>
                {
                    TryRead(args, "path", out var path);
                    var depth = TryRead(args, "depth", out var depthText) && int.TryParse(depthText, out var d) ? d : (int?)null;
                    return AppToolResult.Ok(await _sandboxService.ListDirectoryAsync(session, path, depth, ct).ConfigureAwait(false));
                },
            },
            new AppTool
            {
                Name = "sandbox_delete_file",
                Title = "删除文件",
                Description = "删除沙箱中的文件。",
                Kind = "sandbox",
                ParametersExample = "{\"path\":\"/workspace/tmp.txt\"}",
                InvokeAsync = async (args, ct) =>
                {
                    if (!TryRead(args, "path", out var path) || string.IsNullOrWhiteSpace(path))
                    {
                        return AppToolResult.Fail("缺少参数 path.");
                    }

                    return AppToolResult.Ok(await _sandboxService.DeleteFileAsync(session, path!, ct).ConfigureAwait(false));
                },
            },
            new AppTool
            {
                Name = "sandbox_search_files",
                Title = "搜索文件",
                Description = "在沙箱指定目录下按模式搜索文件。",
                Kind = "sandbox",
                ParametersExample = "{\"path\":\"/workspace\",\"pattern\":\"*.py\"}",
                InvokeAsync = async (args, ct) =>
                {
                    if (!TryRead(args, "path", out var path) || string.IsNullOrWhiteSpace(path))
                    {
                        return AppToolResult.Fail("缺少参数 path.");
                    }

                    if (!TryRead(args, "pattern", out var pattern) || string.IsNullOrWhiteSpace(pattern))
                    {
                        return AppToolResult.Fail("缺少参数 pattern.");
                    }

                    return AppToolResult.Ok(await _sandboxService.SearchFilesAsync(session, path!, pattern!, ct).ConfigureAwait(false));
                },
            },
        ];

        return Task.FromResult(tools);
    }

    private static bool TryRead(string? argsJson, string name, out string? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(argsJson) || argsJson!.Trim() == "{}")
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(argsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty(name, out var element))
            {
                return false;
            }

            value = element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _ => element.GetRawText(),
            };
            return value != null;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
