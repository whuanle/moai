using FastEndpoints;
using FluentValidation;
using MoAI.Login.Commands;

namespace MoAI.Admin.Validators;

public class UpdateOAuthConnectionCommandValidator : Validator<UpdateOAuthConnectionCommand>
{
    public UpdateOAuthConnectionCommandValidator()
    {
        RuleFor(x => x.OAuthConnectionId).NotEmpty().WithMessage("ID不能为空.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("认证名称不能为空.")
            .MinimumLength(2)
            .MaximumLength(20)
            .Matches(@"^[\u4e00-\u9fa5a-zA-Z0-9]+$").WithMessage("认证名称只能包含中文、英文和数字.");
        RuleFor(x => x.Provider).NotEmpty().WithMessage("提供商不能为空.");
        RuleFor(x => x.Key).NotEmpty().WithMessage("应用key不能为空.");
        RuleFor(x => x.IconUrl).NotEmpty().WithMessage("图标地址不能为空.");
        RuleFor(x => x.WellKnown).NotEmpty().WithMessage("发现端口不能为空.");
    }
}
