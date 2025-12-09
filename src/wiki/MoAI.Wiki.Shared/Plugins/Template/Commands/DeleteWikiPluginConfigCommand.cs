using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Plugins.Template.Commands;

/// <summary>
/// 删除配置.
/// </summary>
public class DeleteWikiPluginConfigCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteWikiPluginConfigCommand>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 配置.
    /// </summary>
    public int ConfigId { get; init; }

    /// <summary>
    /// 是否删除该配置下的所有网页.
    /// </summary>
    public bool IsDeleteDocuments { get; set; }

    /// <inheritdoc/>
    public void Validate(AbstractValidator<DeleteWikiPluginConfigCommand> validate)
    {
        validate.RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        validate.RuleFor(x => x.ConfigId)
            .NotEmpty().WithMessage("爬虫配置id不正确")
            .GreaterThan(0).WithMessage("爬虫配置id不正确");
    }
}