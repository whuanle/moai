using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.Feishu.Commands;

/// <summary>
/// 更新飞书应用连接，需要团队 Admin 及以上角色；AppSecret 为空表示保持不变，更新后自动重连.
/// </summary>
public class UpdateFeishuAppCommand : IRequest<EmptyCommandResponse>, IUserIdContext, IModelValidator<UpdateFeishuAppCommand>
{
    /// <summary>
    /// 飞书应用记录 id（来自路由）.
    /// </summary>
    public Guid FeishuAppId { get; init; }

    /// <summary>
    /// 连接名称，团队内唯一.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 描述，可为空.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 飞书开放平台 AppSecret，为空表示保持不变.
    /// </summary>
    public string? AppSecret { get; init; }

    /// <summary>
    /// 接入域名，为空表示保持不变.
    /// </summary>
    public string? Domain { get; init; }

    /// <summary>
    /// 是否禁用，禁用后断开长连接且不再接收事件.
    /// </summary>
    public bool IsDisable { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateFeishuAppCommand> validate)
    {
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("连接名称不能为空.").MaximumLength(50).WithMessage("连接名称最长 50 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("描述最长 255 个字符.");
        validate.RuleFor(x => x.AppSecret).MaximumLength(128).WithMessage("飞书 AppSecret 最长 128 个字符.");
        validate.RuleFor(x => x.Domain).MaximumLength(100).WithMessage("接入域名最长 100 个字符.");
    }
}
