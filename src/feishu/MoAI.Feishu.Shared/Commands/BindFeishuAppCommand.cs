using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Database.Enums;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.Feishu.Commands;

/// <summary>
/// 绑定飞书应用到渠道（应用/知识库等），需要团队 Admin 及以上角色；同一飞书应用同时只能绑定一个渠道，绑定冲突时返回 409.
/// </summary>
public class BindFeishuAppCommand : IRequest<EmptyCommandResponse>, IUserIdContext, IModelValidator<BindFeishuAppCommand>
{
    /// <summary>
    /// 飞书应用记录 id（来自路由）.
    /// </summary>
    public Guid FeishuAppId { get; init; }

    /// <summary>
    /// 渠道类型.
    /// </summary>
    public FeishuChannelType ChannelType { get; init; }

    /// <summary>
    /// 渠道记录 id 字符串，应用为 app.id（uuid），知识库为 wiki.id（数字）.
    /// </summary>
    public string ChannelId { get; init; } = default!;

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<BindFeishuAppCommand> validate)
    {
        validate.RuleFor(x => x.ChannelType).IsInEnum().WithMessage("渠道类型不正确.");
        validate.RuleFor(x => x.ChannelId).NotEmpty().WithMessage("渠道 id 不能为空.").MaximumLength(64).WithMessage("渠道 id 最长 64 个字符.");
    }
}
