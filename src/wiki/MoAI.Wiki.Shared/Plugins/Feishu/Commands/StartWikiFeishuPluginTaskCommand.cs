using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Plugins.Template.Commands;

/// <summary>
/// 开始启动知识库插件任务.
/// </summary>
public class StartWikiFeishuPluginTaskCommand : IRequest<EmptyCommandResponse>, IModelValidator<StartWikiFeishuPluginTaskCommand>
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

    /// <inheritdoc/>
    public void Validate(AbstractValidator<StartWikiFeishuPluginTaskCommand> validate)
    {
        validate.RuleFor(x => x.WikiId).GreaterThan(0).WithMessage("知识库ID错误");
        validate.RuleFor(x => x.ConfigId).GreaterThan(0).WithMessage("配置ID错误");
    }
}
