using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Skill.Queries.Responses;

namespace MoAI.Skill.Queries;

/// <summary>
/// 查询技能包文件下载地址：对可见技能（内置除外）返回每个文件的预签名下载链接.
/// </summary>
public class QuerySkillFileDownloadCommand : IRequest<QuerySkillFileDownloadResponse>, IModelValidator<QuerySkillFileDownloadCommand>, IUserIdContext
{
    /// <summary>
    /// 技能 id.
    /// </summary>
    public Guid SkillId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QuerySkillFileDownloadCommand> validate)
    {
        validate.RuleFor(x => x.SkillId).NotEmpty().WithMessage("技能 id 不正确.");
    }
}
