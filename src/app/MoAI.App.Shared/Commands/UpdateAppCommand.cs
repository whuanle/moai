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
    /// 是否外部应用：false=内部应用，true=外部应用；应用创建后类型不可更改，此处按创建时类型回传.
    /// </summary>
    public bool IsExternal { get; init; }

    /// <summary>
    /// 是否需要授权访问；仅外部应用有效，内部应用必须为 false.
    /// </summary>
    public bool IsAuth { get; init; }

    /// <summary>
    /// 分类 id，0=未分类；分类类型必须为 app.
    /// </summary>
    public int ClassifyId { get; init; }

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
        validate.RuleFor(x => x.ClassifyId).GreaterThanOrEqualTo(0).WithMessage("分类 id 不正确.");
        validate.RuleFor(x => x.IsAuth).Must((cmd, isAuth) => !isAuth || cmd.IsExternal)
            .WithMessage("只有外部应用可以设置需要授权访问.");
    }
}
