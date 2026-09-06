using FluentValidation;
using MediatR;
using MoAI.Gateway.Queries.Responses;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.Gateway.Commands;

/// <summary>
/// 团队管理员创建网关 API Key，密钥原文仅在创建响应中返回一次.
/// </summary>
public class CreateTeamApiKeyCommand : IRequest<CreateTeamApiKeyCommandResponse>, IUserIdContext, IModelValidator<CreateTeamApiKeyCommand>
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 密钥名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 过期时间，null=永不过期.
    /// </summary>
    public DateTimeOffset? ExpireTime { get; init; }

    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateTeamApiKeyCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).NotEmpty().WithMessage("团队id不能为空.");
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("密钥名称不能为空.").MaximumLength(100).WithMessage("密钥名称不能超过100个字符.");
    }
}
