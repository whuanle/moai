using FluentValidation;
using MediatR;
using MoAI.AIChannel.Models;
using MoAI.Infra.Models;

namespace MoAI.AIChannel.Commands;

/// <summary>
/// 更新 AI 渠道.
/// </summary>
public class UpdateAIChannelCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateAIChannelCommand>
{
    /// <summary>
    /// 渠道 id，由 Controller 从路由参数回填.
    /// </summary>
    public Guid ChannelId { get; set; }

    /// <summary>
    /// 渠道标识，对应 models.json 中的 provider id.
    /// </summary>
    public string ProviderKey { get; init; } = default!;

    /// <summary>
    /// 渠道名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 协议族.
    /// </summary>
    public AIProtocolFamily ProtocolFamily { get; init; } = default!;

    /// <summary>
    /// 接入端点.
    /// </summary>
    public string? BaseUrl { get; init; }

    /// <summary>
    /// 密钥，为空时保持不变.
    /// </summary>
    public string? ApiKey { get; init; }

    /// <summary>
    /// 是否启用.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// 描述.
    /// </summary>
    public string? Description { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateAIChannelCommand> validate)
    {
        validate.RuleFor(x => x.ProviderKey).NotEmpty().WithMessage("渠道标识不能为空.").MaximumLength(50).WithMessage("渠道标识最长 50 个字符.");
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("渠道名称不能为空.").MaximumLength(100).WithMessage("渠道名称最长 100 个字符.");
        validate.RuleFor(x => x.ProtocolFamily).IsInEnum().WithMessage("协议族不合法.");
        validate.RuleFor(x => x.BaseUrl).MaximumLength(1000).When(x => x.BaseUrl != null).WithMessage("接入端点最长 1000 个字符.");
        validate.RuleFor(x => x.ApiKey).MaximumLength(500).When(x => x.ApiKey != null).WithMessage("密钥最长 500 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(1000).When(x => x.Description != null).WithMessage("描述最长 1000 个字符.");
    }
}
