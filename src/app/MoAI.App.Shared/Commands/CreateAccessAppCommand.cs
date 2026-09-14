using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.App.Queries.Responses;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Commands;

/// <summary>
/// 创建应用接入（团队下的 key，授权可访问哪些外部应用），需要团队 Admin 及以上角色.
/// </summary>
public class CreateAccessAppCommand : IRequest<CreateAccessAppCommandResponse>, IUserIdContext, IModelValidator<CreateAccessAppCommand>
{
    /// <summary>
    /// 所属团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 接入名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 描述，可为空.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 允许访问的外部应用 id 列表，必须属于本团队且为外部应用.
    /// </summary>
    public List<Guid> AppIds { get; init; } = new();

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateAccessAppCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("接入名称不能为空.").MaximumLength(20).WithMessage("接入名称最长 20 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("接入描述最长 255 个字符.");
        validate.RuleFor(x => x.AppIds).NotNull().WithMessage("授权应用列表不能为空.");
    }
}
