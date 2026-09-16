using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.App.Queries.Responses;

namespace MoAI.App.Queries;

/// <summary>
/// 查询当前用户在某应用下的个性化配置（专家提示词/自选技能）与应用锁定技能.
/// </summary>
public class QueryAppUserConfigCommand : IRequest<QueryAppUserConfigCommandResponse>, IModelValidator<QueryAppUserConfigCommand>, IUserIdContext
{
    /// <summary>
    /// 应用 id.
    /// </summary>
    public Guid AppId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryAppUserConfigCommand> validate)
    {
        validate.RuleFor(x => x.AppId).NotEmpty().WithMessage("应用 id 不正确.");
    }
}
