using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.WeixinWork.Commands;

/// <summary>
/// 更新企业微信应用连接，需要团队 Admin 及以上角色；CorpSecret/Token/EncodingAESKey 为空表示保持不变.
/// </summary>
public class UpdateWeixinWorkAppCommand : IRequest<EmptyCommandResponse>, IUserIdContext, IModelValidator<UpdateWeixinWorkAppCommand>
{
    /// <summary>
    /// 企业微信应用记录 id（来自路由）.
    /// </summary>
    public Guid WeixinWorkAppId { get; init; }

    /// <summary>
    /// 连接名称，团队内唯一.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 描述，可为空.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 企业微信企业 ID.
    /// </summary>
    public string CorpId { get; init; } = default!;

    /// <summary>
    /// 企业微信自建应用 AgentId.
    /// </summary>
    public int AgentId { get; init; }

    /// <summary>
    /// 企业微信自建应用 Secret，为空表示保持不变.
    /// </summary>
    public string? CorpSecret { get; init; }

    /// <summary>
    /// 接收消息回调配置 Token，为空表示保持不变.
    /// </summary>
    public string? Token { get; init; }

    /// <summary>
    /// 接收消息回调配置 EncodingAESKey（43 字符），为空表示保持不变.
    /// </summary>
    public string? EncodingAesKey { get; init; }

    /// <summary>
    /// API 地址覆盖，为空表示使用官方地址.
    /// </summary>
    public string? ApiEndpoint { get; init; }

    /// <summary>
    /// 是否禁用，禁用后回调推送不再处理.
    /// </summary>
    public bool IsDisable { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateWeixinWorkAppCommand> validate)
    {
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("连接名称不能为空.").MaximumLength(50).WithMessage("连接名称最长 50 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("描述最长 255 个字符.");
        validate.RuleFor(x => x.CorpId).NotEmpty().WithMessage("企业 ID 不能为空.").MaximumLength(64).WithMessage("企业 ID 最长 64 个字符.");
        validate.RuleFor(x => x.AgentId).GreaterThan(0).WithMessage("AgentId 不正确.");
        validate.RuleFor(x => x.CorpSecret).MaximumLength(128).WithMessage("应用 Secret 最长 128 个字符.");
        validate.RuleFor(x => x.Token).MaximumLength(128).WithMessage("回调 Token 最长 128 个字符.");
        validate.RuleFor(x => x.EncodingAesKey).Length(43).WithMessage("EncodingAESKey 须为 43 位字符串.").When(x => !string.IsNullOrWhiteSpace(x.EncodingAesKey));
        validate.RuleFor(x => x.ApiEndpoint).MaximumLength(200).WithMessage("API 地址最长 200 个字符.").Must(x => string.IsNullOrWhiteSpace(x) || x.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || x.StartsWith("https://", StringComparison.OrdinalIgnoreCase)).WithMessage("API 地址须以 http(s):// 开头.").When(x => !string.IsNullOrWhiteSpace(x.ApiEndpoint));
    }
}
