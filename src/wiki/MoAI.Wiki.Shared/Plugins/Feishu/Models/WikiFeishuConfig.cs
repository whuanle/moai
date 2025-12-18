using FluentValidation;
using MediatR;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Plugins.Feishu.Models;

public class WikiFeishuConfig : IWikiPluginKey, IModelValidator<WikiFeishuConfig>
{
    /// <summary>
    /// 飞书应用 id.
    /// </summary>
    public string AppId { get; init; } = string.Empty;

    /// <summary>
    /// 飞书应用密钥.
    /// </summary>
    public string AppSecret { get; init; } = string.Empty;

    /// <summary>
    /// 飞书知识库 id.
    /// </summary>
    public string SpaceId { get; init; } = string.Empty;

    /// <summary>
    /// 顶部文档 token.
    /// </summary>
    public string ParentNodeToken { get; init; } = string.Empty;

    /// <summary>
    /// 是否覆盖已存在的页面.
    /// </summary>
    public bool IsOverExistPage { get; init; }

    /// <summary>
    /// 插件名称.
    /// </summary>
    public string PluginKey => "feishu";

    /// <inheritdoc/>
    public void Validate(AbstractValidator<WikiFeishuConfig> validate)
    {
        validate.RuleFor(x => x.AppId)
            .NotEmpty().WithMessage("不能为空");

        validate.RuleFor(x => x.AppSecret)
            .NotEmpty().WithMessage("不能为空");

        validate.RuleFor(x => x.SpaceId)
            .NotEmpty().WithMessage("不能为空");

        validate.RuleFor(x => x.ParentNodeToken)
            .NotEmpty().WithMessage("不能为空");
    }
}
