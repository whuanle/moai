using FluentValidation;
using MediatR;
using MoAI.AIPlugin.Queries.Responses;

namespace MoAI.AIPlugin.Queries;

/// <summary>
/// 查询插件的工具清单（多工具动态插件返回全部工具；单工具插件固定返回一个 key=run 的项）.
/// </summary>
public class QueryPluginToolsCommand : IRequest<QueryPluginToolsCommandResponse>, IModelValidator<QueryPluginToolsCommand>
{
    /// <summary>
    /// 插件 key（模板 key 或动态插件实例 key）.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryPluginToolsCommand> validate)
    {
        validate.RuleFor(x => x.Key).NotEmpty().WithMessage("插件 Key 不能为空");
    }
}
