using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.Gateway.Commands;

/// <summary>
/// 团队管理员修改网关 API Key（名称、启用/禁用）.
/// </summary>
public class UpdateTeamApiKeyCommand : IRequest<EmptyCommandResponse>, IUserIdContext, IModelValidator<UpdateTeamApiKeyCommand>
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 密钥 id.
    /// </summary>
    public Guid ApiKeyId { get; init; }

    /// <summary>
    /// 新名称.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 是否禁用.
    /// </summary>
    public bool IsDisable { get; init; }

    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateTeamApiKeyCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).NotEmpty().WithMessage("团队id不能为空.");
        validate.RuleFor(x => x.ApiKeyId).NotEmpty().WithMessage("密钥id不能为空.");
        validate.RuleFor(x => x.Name).MaximumLength(100).When(x => x.Name != null).WithMessage("密钥名称不能超过100个字符.");
    }
}
