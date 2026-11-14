using System;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 配置知识图谱的默认抽取模型（管理员，D30）：AI 导入未显式指定模型时使用（请求级 aiModelId &gt; 图谱默认 &gt; 400）.
/// </summary>
public class UpdateKnowledgeGraphExtractModelCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateKnowledgeGraphExtractModelCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KnowledgeGraphId { get; init; }

    /// <summary>
    /// 默认抽取模型 id（需已启用、团队可用的对话模型）；null 表示清除默认（导入时必须显式指定）.
    /// </summary>
    public Guid? ExtractModelId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateKnowledgeGraphExtractModelCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        // ExtractModelId 允许 null（清除默认），非空合法性在 Handler 校验模型存在/启用/团队可用/对话类型.
    }
}
