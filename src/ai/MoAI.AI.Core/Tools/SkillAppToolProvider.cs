using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using MoAI.AI.Models;
using MoAI.Skill.Services;

namespace MoAI.AI.Services;

/// <summary>
/// 技能工具来源：把应用挂载的技能转换为"加载"工具；调用时把技能包文件写入会话沙箱并返回使用说明，
/// Agent 随后按说明使用沙箱工具（run_code 等）执行技能脚本.
/// <para>沙箱未启用时降级为仅返回使用说明（脚本无法执行）.</para>
/// </summary>
[InjectOnScoped]
public sealed class SkillAppToolProvider : IAppToolProvider
{
    /// <summary>
    /// 技能脚本写入沙箱的根目录（沙箱工作区）.
    /// </summary>
    public const string SandboxSkillRoot = "/workspace/skills";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ISkillService _skillService;
    private readonly IAppSandboxService _sandboxService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkillAppToolProvider"/> class.
    /// </summary>
    /// <param name="skillService">技能领域服务.</param>
    /// <param name="sandboxService">会话沙箱服务.</param>
    public SkillAppToolProvider(ISkillService skillService, IAppSandboxService sandboxService)
    {
        _skillService = skillService;
        _sandboxService = sandboxService;
    }

    /// <inheritdoc/>
    public int Order => 14;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AppTool>> GetToolsAsync(AppAgentBuildContext context, CancellationToken cancellationToken)
    {
        if (context.SkillIds.Count == 0)
        {
            return [];
        }

        var skills = await _skillService.GetRuntimeSkillsAsync(context.SkillIds, cancellationToken).ConfigureAwait(false);
        if (skills.Count == 0)
        {
            return [];
        }

        var sandboxEnabled = AppAgentExecutionSettings.Parse(context.Config?.ExecutionSettings).Sandbox is { Enabled: true };

        var tools = new List<AppTool>(skills.Count);
        foreach (var skill in skills)
        {
            var session = new SandboxSessionContext
            {
                SessionId = context.SessionId,
                AppId = context.AppId,
                TeamId = context.TeamId,
                Settings = AppAgentExecutionSettings.Parse(context.Config?.ExecutionSettings).Sandbox ?? new SandboxSettings(),
            };

            tools.Add(new AppTool
            {
                Name = $"skill_{skill.Key}",
                Title = skill.Name,
                Description = $"{skill.Description} 调用本工具可加载该技能的脚本与使用说明。",
                Kind = "skill",
                ParametersExample = "{}",
                InvokeAsync = (args, ct) => LoadSkillAsync(session, skill, sandboxEnabled, ct),
            });
        }

        return tools;
    }

    private async Task<AppToolResult> LoadSkillAsync(SandboxSessionContext session, SkillRuntimeInfo skill, bool sandboxEnabled, CancellationToken cancellationToken)
    {
        var loadedFiles = new List<string>();
        if (sandboxEnabled)
        {
            foreach (var file in skill.Files)
            {
                try
                {
                    var content = await _skillService.ReadSkillFileAsync(file, cancellationToken).ConfigureAwait(false);
                    var targetPath = $"{SandboxSkillRoot}/{skill.Key}/{file.Path.TrimStart('/')}";
                    await _sandboxService.WriteFileAsync(session, targetPath, content, cancellationToken).ConfigureAwait(false);
                    loadedFiles.Add(targetPath);
                }
#pragma warning disable CA1031 // 单个技能文件加载失败记录后继续
                catch (Exception ex)
                {
                    return AppToolResult.Fail($"加载技能文件 {file.Path} 失败: {ex.Message}");
                }
#pragma warning restore CA1031
            }
        }

        return AppToolResult.Ok(JsonSerializer.Serialize(new
        {
            success = true,
            skill = skill.Name,
            key = skill.Key,
            sandboxEnabled,
            files = loadedFiles,
            message = sandboxEnabled
                ? $"技能已加载：脚本位于 {SandboxSkillRoot}/{skill.Key}/，请按以下使用说明执行。"
                : "当前应用未启用沙箱，技能脚本无法写入执行，以下仅为使用说明。",
            instructions = skill.Instructions,
        }, JsonOptions));
    }
}
