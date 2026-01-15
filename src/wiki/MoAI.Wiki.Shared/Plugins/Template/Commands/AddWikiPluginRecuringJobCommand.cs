using FluentValidation;
using MediatR;
using MoAI.Infra;
using MoAI.Infra.Models;
using MoAI.Wiki.Batch.Models;

namespace MoAI.Wiki.Plugins.Template.Commands;

/// <summary>
/// 新建插件定时任务.
/// </summary>
public class AddWikiPluginRecuringJobCommand : IRequest<EmptyCommandResponse>, IModelValidator<AddWikiPluginRecuringJobCommand>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 知识库插件实例 id.
    /// </summary>
    public int ConfigId { get; init; }

    /// <summary>
    /// true 是启动定时任务任务，false 是取消定时任务.
    /// </summary>
    public bool IsStart { get; init; }

    /// <summary>
    /// Cron 表达式.
    /// </summary>
    public string Cron { get; init; } = string.Empty;

    /// <summary>
    /// 是否自动处理文档.
    /// </summary>
    public bool IsAutoProcess { get; init; }

    /// <summary>
    /// 自动处理配置.
    /// </summary>
    public WikiPluginAutoProcessConfig? AutoProcessConfig { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<AddWikiPluginRecuringJobCommand> validate)
    {
        validate.RuleFor(x => x.Cron).NotEmpty().WithMessage("Cron表达式不能为空");
        validate.RuleFor(x => x.WikiId).GreaterThan(0).WithMessage("知识库ID错误");
        validate.RuleFor(x => x.ConfigId).GreaterThan(0).WithMessage("配置ID错误");
        validate.When(x => x.IsStart && x.IsAutoProcess, () =>
        {
            validate.RuleFor(x => x.AutoProcessConfig).NotNull().WithMessage("自动处理配置不能为空");
            validate.RuleFor(x => x.AutoProcessConfig!).SetValidator(new AutoValidator<WikiPluginAutoProcessConfig>());
        });
    }
}
