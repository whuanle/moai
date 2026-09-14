using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Commands;

/// <summary>
/// 更新应用接入（名称、描述、授权外部应用；key 不可改），需要团队 Admin 及以上角色.
/// </summary>
public class UpdateAccessAppCommand : IRequest<EmptyCommandResponse>, IUserIdContext, IModelValidator<UpdateAccessAppCommand>
{
    /// <summary>
    /// 接入 id，由 Controller 从路由参数回填.
    /// </summary>
    public Guid AccessAppId { get; init; }

    /// <summary>
    /// 接入名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 描述，可为空.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 允许访问的外部应用 id 列表.
    /// </summary>
    public List<Guid> AppIds { get; init; } = new();

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateAccessAppCommand> validate)
    {
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("接入名称不能为空.").MaximumLength(20).WithMessage("接入名称最长 20 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("接入描述最长 255 个字符.");
        validate.RuleFor(x => x.AppIds).NotNull().WithMessage("授权应用列表不能为空.");
    }
}
