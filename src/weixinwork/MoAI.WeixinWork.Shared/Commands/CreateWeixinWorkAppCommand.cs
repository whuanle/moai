using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Database.Enums;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.WeixinWork.Commands;

/// <summary>
/// 创建企业微信应用连接，需要团队 Admin 及以上角色；CorpId + AgentId 全局唯一，创建后回调地址即可配置到企业微信管理端；
/// 可携带渠道信息创建即绑定（当前仅应用渠道）.
/// </summary>
public class CreateWeixinWorkAppCommand : IRequest<SimpleGuid>, IUserIdContext, IModelValidator<CreateWeixinWorkAppCommand>
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
    /// 企业微信企业 ID（管理后台 我的企业 → 企业信息）.
    /// </summary>
    public string CorpId { get; init; } = default!;

    /// <summary>
    /// 企业微信自建应用 Secret（应用管理 → 自建应用 → Secret）.
    /// </summary>
    public string CorpSecret { get; init; } = default!;

    /// <summary>
    /// 企业微信自建应用 AgentId.
    /// </summary>
    public int AgentId { get; init; }

    /// <summary>
    /// 接收消息回调配置 Token（管理后台 接收消息 → 设置 API 接收）.
    /// </summary>
    public string Token { get; init; } = default!;

    /// <summary>
    /// 接收消息回调配置 EncodingAESKey（43 字符）.
    /// </summary>
    public string EncodingAesKey { get; init; } = default!;

    /// <summary>
    /// API 地址覆盖，可为空（默认官方地址）；供本地桩联调或代理使用.
    /// </summary>
    public string? ApiEndpoint { get; init; }

    /// <summary>
    /// 创建即绑定的渠道类型，可为空（不绑定）；当前仅支持应用渠道 app.
    /// </summary>
    public WeixinWorkChannelType? ChannelType { get; init; }

    /// <summary>
    /// 创建即绑定的渠道记录 id 字符串，应用渠道为 app.id（uuid）；与 <see cref="ChannelType"/> 必须同时提供或同时缺省.
    /// </summary>
    public string? ChannelId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateWeixinWorkAppCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("连接名称不能为空.").MaximumLength(50).WithMessage("连接名称最长 50 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("描述最长 255 个字符.");
        validate.RuleFor(x => x.CorpId).NotEmpty().WithMessage("企业 ID 不能为空.").MaximumLength(64).WithMessage("企业 ID 最长 64 个字符.");
        validate.RuleFor(x => x.CorpSecret).NotEmpty().WithMessage("应用 Secret 不能为空.").MaximumLength(128).WithMessage("应用 Secret 最长 128 个字符.");
        validate.RuleFor(x => x.AgentId).GreaterThan(0).WithMessage("AgentId 不正确.");
        validate.RuleFor(x => x.Token).NotEmpty().WithMessage("回调 Token 不能为空.").MaximumLength(128).WithMessage("回调 Token 最长 128 个字符.");
        validate.RuleFor(x => x.EncodingAesKey).NotEmpty().WithMessage("EncodingAESKey 不能为空.").Length(43).WithMessage("EncodingAESKey 须为 43 位字符串.");
        validate.RuleFor(x => x.ApiEndpoint).MaximumLength(200).WithMessage("API 地址最长 200 个字符.").Must(x => string.IsNullOrWhiteSpace(x) || x.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || x.StartsWith("https://", StringComparison.OrdinalIgnoreCase)).WithMessage("API 地址须以 http(s):// 开头.").When(x => !string.IsNullOrWhiteSpace(x.ApiEndpoint));
        validate.RuleFor(x => x.ChannelType).IsInEnum().WithMessage("渠道类型不正确.").When(x => x.ChannelType.HasValue);
        validate.RuleFor(x => x.ChannelId).NotEmpty().WithMessage("渠道 id 不能为空.").MaximumLength(64).WithMessage("渠道 id 最长 64 个字符.").When(x => x.ChannelType.HasValue);
        validate.RuleFor(x => x.ChannelType).NotNull().WithMessage("渠道类型与渠道 id 必须同时提供.").When(x => !string.IsNullOrWhiteSpace(x.ChannelId));
    }
}
