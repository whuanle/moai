using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Database.Enums;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Commands;

/// <summary>
/// 创建团队应用，需要团队 Admin 及以上角色.
/// </summary>
public class CreateAppCommand : IRequest<SimpleGuid>, IUserIdContext, IModelValidator<CreateAppCommand>
{
    /// <summary>
    /// 所属团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 应用名称，团队内唯一.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 应用描述，可为空.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 应用类型：Agent 应用=0，流程应用=1.
    /// </summary>
    public AppType AppType { get; init; }

    /// <summary>
    /// 应用头像 objectKey，可为空；为空表示创建时不设置头像.
    /// <para>必须是由存储直传管线完成上传并登记的文件（与设置头像接口同规则）。</para>
    /// </summary>
    public string? Avatar { get; init; }

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
    public static void Validate(AbstractValidator<CreateAppCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("应用名称不能为空.").MaximumLength(20).WithMessage("应用名称最长 20 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("应用描述最长 255 个字符.");
        validate.RuleFor(x => x.AppType).IsInEnum().WithMessage("应用类型不正确.");
        validate.RuleFor(x => x.Avatar).MaximumLength(255).WithMessage("头像 objectKey 最长 255 个字符.");
    }
}
