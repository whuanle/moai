using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Commands;

/// <summary>
/// 更新应用基础信息（名称、描述），需要团队 Admin 及以上角色；应用类型不可修改.
/// </summary>
public class UpdateAppCommand : IRequest<EmptyCommandResponse>, IUserIdContext, IModelValidator<UpdateAppCommand>
{
    /// <summary>
    /// 应用 id，由 Controller 从路由参数回填.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 应用名称，团队内唯一.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 应用描述，可为空.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 允许外部使用；开启后团队外用户可通过「外部用户」能力使用该应用（能力本身待后续交付）.
    /// </summary>
    public bool EnableForeign { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateAppCommand> validate)
    {
        // AppId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段.
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("应用名称不能为空.").MaximumLength(20).WithMessage("应用名称最长 20 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("应用描述最长 255 个字符.");
    }
}
