using FluentValidation;
using MediatR;
using MoAI.Infra;
using MoAI.Infra.Models;
using MoAI.Wiki.Batch.Models;

namespace MoAI.Wiki.Plugins.Template.Commands;

/// <summary>
/// 开始启动知识库插件任务.
/// </summary>
public class StartWikiCrawlerPluginTaskCommand : IRequest<EmptyCommandResponse>, IModelValidator<StartWikiCrawlerPluginTaskCommand>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 知识库 web 配置 id.
    /// </summary>
    public int ConfigId { get; init; }

    /// <summary>
    /// true 是启动任务，false 是停止任务.
    /// </summary>
    public bool IsStart { get; init; }

    /// <summary>
    /// 是否自动处理文档.
    /// </summary>
    public bool IsAutoProcess { get; init; }

    /// <summary>
    /// 自动处理配置.
    /// </summary>
    public WikiPluginAutoProcessConfig? AutoProcessConfig { get; init; }

    /// <inheritdoc/>
    public void Validate(AbstractValidator<StartWikiCrawlerPluginTaskCommand> validate)
    {
        validate.RuleFor(x => x.WikiId).GreaterThan(0).WithMessage("知识库ID错误");
        validate.RuleFor(x => x.ConfigId).GreaterThan(0).WithMessage("配置ID错误");

        validate.When(x => x.IsStart && x.IsAutoProcess, () =>
        {
            validate.RuleFor(x => x.AutoProcessConfig).NotNull().WithMessage("自动处理配置不能为空");
            validate.RuleFor(x => x.AutoProcessConfig!).SetValidator(new AutoValidator<WikiPluginAutoProcessConfig>());
        });
    }
}
