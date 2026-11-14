using FluentValidation;
using MediatR;
using MoAI.AIPlugin.Queries.Responses;

namespace MoAI.AIPlugin.Queries;

/// <summary>
/// 查询单个工具的参数运行示例 JSON.
/// </summary>
public class QueryPluginToolExampleCommand : IRequest<QueryPluginToolExampleCommandResponse>, IModelValidator<QueryPluginToolExampleCommand>
{
    /// <summary>
    /// 插件 key（模板 key 或动态插件实例 key）.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// 工具 key（单工具插件传 run）.
    /// </summary>
    public string ToolKey { get; init; } = string.Empty;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryPluginToolExampleCommand> validate)
    {
        validate.RuleFor(x => x.Key).NotEmpty().WithMessage("插件 Key 不能为空");
        validate.RuleFor(x => x.ToolKey).NotEmpty().WithMessage("工具 Key 不能为空");
    }
}
