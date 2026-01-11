using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.Plugins.Paddleocr.Models;

namespace MoAI.Wiki.Plugins.Paddleocr.Commands;

/// <summary>
/// 预览飞桨 OCR 文档解析结果.
/// </summary>
public class PreviewPaddleocrDocumentCommand : IRequest<PaddleocrPreviewResult>, IModelValidator<PreviewPaddleocrDocumentCommand>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 插件 id.
    /// </summary>
    public int PluginId { get; init; }

    /// <summary>
    /// 文件id.
    /// </summary>
    public int FileId { get; init; } = default!;

    /// <summary>
    /// 配置，Json 格式.
    /// </summary>
    public string Config { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<PreviewPaddleocrDocumentCommand> validate)
    {
        validate.RuleFor(x => x.WikiId)
            .GreaterThan(0).WithMessage("知识库 id 不正确");

        validate.RuleFor(x => x.PluginId)
            .GreaterThan(0).WithMessage("插件 id 不正确");

        validate.RuleFor(x => x.FileId)
            .NotEmpty().WithMessage("文件不能为空");

        validate.RuleFor(x => x.Config)
            .NotEmpty().WithMessage("文件类型不正确");
    }
}
