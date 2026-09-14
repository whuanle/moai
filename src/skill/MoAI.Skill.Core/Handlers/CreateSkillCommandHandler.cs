using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Skill.Commands;
using MoAI.Skill.Models;

namespace MoAI.Skill.Handlers;

/// <summary>
/// <inheritdoc cref="CreateSkillCommand"/>
/// </summary>
public class CreateSkillCommandHandler : IRequestHandler<CreateSkillCommand, SimpleGuid>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateSkillCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public CreateSkillCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<SimpleGuid> Handle(CreateSkillCommand request, CancellationToken cancellationToken)
    {
        var keyExist = await _databaseContext.Skills.AnyAsync(x => x.Key == request.Key, cancellationToken);
        if (keyExist)
        {
            throw new BusinessException("技能标识已存在，请更换后重试.") { StatusCode = 409 };
        }

        await SkillFilesGuard.EnsureFilesValidAsync(_databaseContext, request.Files, cancellationToken);

        var skill = new SkillEntity
        {
            Key = request.Key,
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            Instructions = request.Instructions ?? string.Empty,
            Files = JsonSerializer.Serialize(request.Files, JsonOptions),
            IsSystem = false,
            TeamId = 0,
            IsDisable = false,
        };

        _databaseContext.Skills.Add(skill);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new SimpleGuid { Value = skill.Id };
    }
}

/// <summary>
/// 技能包文件校验：文件必须已上传完成（file 表存在且 IsUploaded）.
/// </summary>
internal static class SkillFilesGuard
{
    public static async Task EnsureFilesValidAsync(DatabaseContext databaseContext, IReadOnlyList<SkillFileItem> files, CancellationToken cancellationToken)
    {
        if (files.Count == 0)
        {
            return;
        }

        var fileIds = files.Select(f => f.FileId).Distinct().ToArray();
        var uploadedCount = await databaseContext.Files
            .CountAsync(x => fileIds.Contains(x.Id) && x.IsUploaded, cancellationToken);

        if (uploadedCount != fileIds.Length)
        {
            throw new BusinessException("存在未上传完成的技能包文件.") { StatusCode = 400 };
        }
    }
}
