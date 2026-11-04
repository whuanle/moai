using System;
using FluentValidation;
using MediatR;
using MoAI.AIPlugin.Queries.Responses;

namespace MoAI.AIPlugin.Queries;

/// <summary>
/// 查询插件的语义配置：插件级使用须知、语义条目、工具元数据（补充说明/调用策略/MCP 注解）与工具清单.
/// </summary>
public class QueryPluginSemanticCommand : IRequest<QueryPluginSemanticCommandResponse>, IModelValidator<QueryPluginSemanticCommand>
{
    /// <summary>
    /// 插件记录 id（plugin.id）.
    /// </summary>
    public Guid PluginId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryPluginSemanticCommand> validate)
    {
        validate.RuleFor(x => x.PluginId).NotEmpty().WithMessage("插件 id 不能为空");
    }
}
