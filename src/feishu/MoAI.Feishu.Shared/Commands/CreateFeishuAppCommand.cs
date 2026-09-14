using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.Feishu.Commands;

/// <summary>
/// 创建飞书应用连接，需要团队 Admin 及以上角色；AppID 全局唯一，创建后立即建立长连接.
/// </summary>
public class CreateFeishuAppCommand : IRequest<SimpleGuid>, IUserIdContext, IModelValidator<CreateFeishuAppCommand>
{
    /// <summary>
    /// 所属团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 连接名称，团队内唯一.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 描述，可为空.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 飞书开放平台 AppID，形如 cli_xxx.
    /// </summary>
    public string AppId { get; init; } = default!;

    /// <summary>
    /// 飞书开放平台 AppSecret.
    /// </summary>
    public string AppSecret { get; init; } = default!;

    /// <summary>
    /// 接入域名，可为空；为空表示飞书默认 https://open.feishu.cn，Lark 填 https://open.larksuite.com.
    /// </summary>
    public string? Domain { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateFeishuAppCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("连接名称不能为空.").MaximumLength(50).WithMessage("连接名称最长 50 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("描述最长 255 个字符.");
        validate.RuleFor(x => x.AppId).NotEmpty().WithMessage("飞书 AppID 不能为空.").MaximumLength(64).WithMessage("飞书 AppID 最长 64 个字符.");
        validate.RuleFor(x => x.AppSecret).NotEmpty().WithMessage("飞书 AppSecret 不能为空.").MaximumLength(128).WithMessage("飞书 AppSecret 最长 128 个字符.");
        validate.RuleFor(x => x.Domain).MaximumLength(100).WithMessage("接入域名最长 100 个字符.");
    }
}
