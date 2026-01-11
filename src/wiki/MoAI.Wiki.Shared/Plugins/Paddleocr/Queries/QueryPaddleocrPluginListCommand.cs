using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.Plugins.Paddleocr.Queries.Responses;

namespace MoAI.Wiki.Plugins.Paddleocr.Queries;

/// <summary>
/// 查询可用的飞桨 OCR 插件列表 (公开或被授权的 paddleocr_ocr、paddleocr_vl、paddleocr_structure_v3 插件).
/// </summary>
public class QueryPaddleocrPluginListCommand : IRequest<QueryPaddleocrPluginListCommandResponse>, IModelValidator<QueryPaddleocrPluginListCommand>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryPaddleocrPluginListCommand> validate)
    {
        validate.RuleFor(x => x.WikiId)
            .NotEmpty();
    }
}
