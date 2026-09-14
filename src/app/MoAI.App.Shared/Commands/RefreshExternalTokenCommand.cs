using FluentValidation;
using MediatR;
using MoAI.App.Queries.Responses;
using MoAI.Infra.Services;

namespace MoAI.App.Commands;

/// <summary>
/// 刷新外部 token：使用 refresh_token 换取新的 access_token 与 refresh_token（旋转），授权范围以数据库当前配置为准.
/// </summary>
public class RefreshExternalTokenCommand : IRequest<ExternalTokenCommandResponse>, IModelValidator<RefreshExternalTokenCommand>
{
    /// <summary>
    /// 换取 token 时返回的 refresh_token.
    /// </summary>
    public string RefreshToken { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<RefreshExternalTokenCommand> validate)
    {
        validate.RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("refreshToken 不能为空.");
    }
}
