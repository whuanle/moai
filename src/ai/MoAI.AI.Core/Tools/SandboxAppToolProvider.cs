using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using MoAI.AI.Models;
using MoAI.Settings.Services;
using MoAI.Storage.Commands;
using MoAI.Storage.Helpers;
using MoAI.Storage.Services;

namespace MoAI.AI.Services;

/// <summary>
/// 沙箱工具来源：当应用开启沙箱时，暴露代码执行 / shell / 文件读写等工具（经 list_tools 渐进披露）.
/// <para>沙箱按会话惰性创建，同会话内代码、命令、文件持续存在；run_shell / run_code 执行前经命令过滤
/// （平台级禁用清单 ∪ 应用级 commandFilter，命中即拦截并返回说明，模型可调整方案）.</para>
/// </summary>
[InjectOnScoped]
public sealed class SandboxAppToolProvider : IAppToolProvider
{
    private static readonly TimeSpan ArtifactDownloadExpiry = TimeSpan.FromHours(1);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAppSandboxService _sandboxService;
    private readonly IStorageService _storageService;
    private readonly ISandboxSettingsService _sandboxSettingsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SandboxAppToolProvider"/> class.
    /// </summary>
    /// <param name="sandboxService">会话沙箱服务.</param>
    /// <param name="storageService">存储领域服务.</param>
    /// <param name="sandboxSettingsService">沙箱系统设置（平台级禁用命令清单）.</param>
    public SandboxAppToolProvider(IAppSandboxService sandboxService, IStorageService storageService, ISandboxSettingsService sandboxSettingsService)
    {
        _sandboxService = sandboxService;
        _storageService = storageService;
        _sandboxSettingsService = sandboxSettingsService;
    }

    /// <inheritdoc/>
    public int Order => 15;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AppTool>> GetToolsAsync(AppAgentBuildContext context, CancellationToken cancellationToken)
    {
        var settings = AppAgentExecutionSettings.Parse(context.Config?.ExecutionSettings);
        if (settings.Sandbox is not { Enabled: true })
        {
            return [];
        }

        var session = new SandboxSessionContext
        {
            SessionId = context.SessionId,
            AppId = context.AppId,
            TeamId = context.TeamId,
            Settings = settings.Sandbox,
        };

        // 命令过滤清单快照（每轮对话装配读取一次；清单更新对已装配轮次不回溯，下一轮生效）
        var limits = await _sandboxSettingsService.GetLimitsAsync(cancellationToken).ConfigureAwait(false);
        var commandDeny = MergeCommandDeny(settings.Sandbox.CommandFilter, limits.DeniedCommands);

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

                    var blocked = SandboxCommandGuard.Evaluate(code, isShell: false, commandDeny);
                    if (blocked != null)
                    {
                        return AppToolResult.Fail(FormatBlocked(blocked));
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

                    var blocked = SandboxCommandGuard.Evaluate(command, isShell: true, commandDeny);
                    if (blocked != null)
                    {
                        return AppToolResult.Fail(FormatBlocked(blocked));
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
            new AppTool
            {
                Name = "sandbox_save_artifact",
                Title = "保存产物文件",
                Description = "把沙箱中生成的文件（如 docx/pptx/xlsx 等二进制产物）保存为可下载文件，返回下载链接。生成文件后如需交付给用户，必须先调用本工具再把链接发给用户。",
                Kind = "sandbox",
                ParametersExample = "{\"path\":\"/workspace/output.docx\",\"fileName\":\"报告.docx\"}",
                InvokeAsync = async (args, ct) =>
                {
                    if (!TryRead(args, "path", out var path) || string.IsNullOrWhiteSpace(path))
                    {
                        return AppToolResult.Fail("缺少参数 path.");
                    }

                    var fileName = TryRead(args, "fileName", out var name) && !string.IsNullOrWhiteSpace(name)
                        ? Path.GetFileName(name!)
                        : Path.GetFileName(path!);
                    if (string.IsNullOrWhiteSpace(fileName))
                    {
                        return AppToolResult.Fail("无法从路径推导文件名，请传 fileName.");
                    }

                    return await SaveArtifactAsync(session, path!, fileName, ct).ConfigureAwait(false);
                },
            },
        ];

        return tools;
    }

    /// <summary>
    /// 从沙箱读取产物字节并上传 MinIO，返回下载链接.
    /// </summary>
    private async Task<AppToolResult> SaveArtifactAsync(SandboxSessionContext session, string path, string fileName, CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await _sandboxService.ReadFileBytesAsync(session, path, cancellationToken).ConfigureAwait(false);
            if (bytes.Length == 0)
            {
                return AppToolResult.Fail("文件为空或不存在.");
            }

            var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
            var objectKey = FileStoreHelper.GetObjectKey(sha256: sha256, fileName: fileName, prefix: $"appagent/{session.SessionId:N}");

            using var stream = new MemoryStream(bytes);
            var upload = await _storageService.UploadStreamAsync(new UploadStreamFileCommand
            {
                FileStream = stream,
                ContentType = FileStoreHelper.GetMimeType(fileName, "application/octet-stream"),
                FileSize = bytes.Length,
                SHA256 = sha256,
                ObjectKey = objectKey,
            }, cancellationToken).ConfigureAwait(false);

            var downloadUrl = await _storageService.GetDownloadUrlAsync(objectKey, fileName, ArtifactDownloadExpiry, cancellationToken).ConfigureAwait(false);

            return AppToolResult.Ok(JsonSerializer.Serialize(new
            {
                success = true,
                fileId = upload.FileId,
                fileName,
                path,
                downloadUrl = downloadUrl.ToString(),
                expiresInMinutes = (int)ArtifactDownloadExpiry.TotalMinutes,
            }, JsonOptions));
        }
#pragma warning disable CA1031 // 产物保存失败以工具错误返回，不中断对话
        catch (Exception ex)
        {
            return AppToolResult.Fail($"保存产物失败: {ex.Message}");
        }
#pragma warning restore CA1031
    }

    private static bool TryRead(string? argsJson, string name, out string? value)
    {        value = null;
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

    /// <summary>
    /// 合并命令过滤清单：平台级全站强制，应用级仅在 enabled 时追加（运行时取并集）.
    /// </summary>
    /// <param name="filter">应用级命令过滤配置.</param>
    /// <param name="platformDeny">平台级禁用清单.</param>
    /// <returns>合并后的禁用清单.</returns>
    private static IReadOnlyList<string> MergeCommandDeny(SandboxCommandFilterSettings? filter, IReadOnlyList<string> platformDeny)
    {
        if (filter is { Enabled: true } && filter.Deny.Count > 0)
        {
            return filter.Deny.Concat(platformDeny).ToList();
        }

        return platformDeny;
    }

    /// <summary>
    /// 命令过滤拦截反馈文案（返回给模型，模型可调整方案；不中断对话）.
    /// </summary>
    /// <param name="blocked">命中信息.</param>
    /// <returns>工具错误文案.</returns>
    private static string FormatBlocked(SandboxCommandBlock blocked)
        => $"该请求已被沙箱命令过滤拦截：命中平台/应用配置的禁用模式「{blocked.Pattern}」。请改用其他方式完成任务，不要尝试绕过过滤。";
}
