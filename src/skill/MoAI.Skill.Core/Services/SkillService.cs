using System.Reflection;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Skill.Models;
using MoAI.Skill.Queries;
using MoAI.Skill.Queries.Responses;
using MoAI.Storage.Services;

namespace MoAI.Skill.Services;

/// <summary>
/// 技能领域服务：面向 Agent 运行时提供技能加载能力.
/// </summary>
public class SkillService : ISkillService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly DatabaseContext _databaseContext;
    private readonly IStorageService _storageService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkillService"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="storageService">存储领域服务.</param>
    public SkillService(DatabaseContext databaseContext, IStorageService storageService)
    {
        _databaseContext = databaseContext;
        _storageService = storageService;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SkillRuntimeInfo>> GetRuntimeSkillsAsync(IReadOnlyCollection<Guid> skillIds, CancellationToken cancellationToken = default)
    {
        if (skillIds.Count == 0)
        {
            return Array.Empty<SkillRuntimeInfo>();
        }

        var ids = skillIds.ToArray();
        var skills = await _databaseContext.Skills
            .Where(x => ids.Contains(x.Id) && !x.IsDisable)
            .ToListAsync(cancellationToken);

        return skills.Select(ToRuntimeInfo).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Guid>> FilterVisibleSkillIdsAsync(IReadOnlyCollection<Guid> skillIds, long userId, int teamId, CancellationToken cancellationToken = default)
    {
        if (skillIds.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        var ids = skillIds.ToArray();
        var visible = await _databaseContext.Skills.AsNoTracking()
            .Where(x => ids.Contains(x.Id) && !x.IsDisable)
            .Where(x => x.IsSystem
                || x.IsPublic
                || x.TeamId == teamId
                || (x.TeamId == 0 && x.CreateUserId == userId))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        return visible;
    }

    /// <inheritdoc/>
    public async Task<string> ReadSkillFileAsync(SkillRuntimeFile file, CancellationToken cancellationToken = default)
    {
        if (file.IsEmbedded)
        {
            var assembly = typeof(SkillService).Assembly;
            using var stream = assembly.GetManifestResourceStream(file.ResourceName!)
                ?? throw new BusinessException($"内置技能文件不存在: {file.Path}") { StatusCode = 404 };
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync(cancellationToken);
        }

        var fileEntity = await _databaseContext.Files.FirstOrDefaultAsync(x => x.Id == file.FileId, cancellationToken)
            ?? throw new BusinessException($"技能文件不存在: {file.Path}") { StatusCode = 404 };

        var result = await _storageService.ReadAsync(fileEntity.ObjectKey, cancellationToken);
        using var streamReader = new StreamReader(result.FileStream);
        return await streamReader.ReadToEndAsync(cancellationToken);
    }

    /// <summary>
    /// 解析技能包文件清单 JSON（skill.files 列），非法 JSON 视为空清单.
    /// </summary>
    /// <param name="filesJson">文件清单 JSON.</param>
    /// <returns>文件项列表.</returns>
    internal static List<SkillFileItem> ParseFiles(string filesJson)
    {
        if (string.IsNullOrWhiteSpace(filesJson))
        {
            return new List<SkillFileItem>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<SkillFileItem>>(filesJson, JsonOptions) ?? new List<SkillFileItem>();
        }
        catch (JsonException)
        {
            return new List<SkillFileItem>();
        }
    }

    private static SkillRuntimeInfo ToRuntimeInfo(SkillEntity entity)
    {
        var files = new List<SkillRuntimeFile>();
        if (entity.IsSystem)
        {
            foreach (var path in BuiltinSkills.GetFiles(entity.Key))
            {
                files.Add(new SkillRuntimeFile
                {
                    Path = path,
                    FileId = 0,
                    ResourceName = BuiltinSkills.BuildResourceName(entity.Key, path),
                });
            }
        }
        else
        {
            foreach (var item in ParseFiles(entity.Files))
            {
                files.Add(new SkillRuntimeFile
                {
                    Path = item.Path,
                    FileId = item.FileId,
                    ResourceName = null,
                });
            }
        }

        return new SkillRuntimeInfo
        {
            Id = entity.Id,
            Key = entity.Key,
            Name = entity.Name,
            Description = entity.Description,
            Instructions = entity.Instructions,
            IsSystem = entity.IsSystem,
            Files = files,
        };
    }
}
