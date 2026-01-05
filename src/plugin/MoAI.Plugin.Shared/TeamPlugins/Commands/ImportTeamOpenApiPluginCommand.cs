using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Plugin.TeamPlugins.Commands;

/// <summary>
/// 团队导入 OpenAPI 插件.
/// </summary>
public class ImportTeamOpenApiPluginCommand : IRequest<SimpleInt>, IModelValidator<ImportTeamOpenApiPluginCommand>
{
    /// <summary>
    /// 团队 ID.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 上传的文件 ID.
    /// </summary>
    public int FileId { get; init; }

    /// <summary>
    /// 文件名称.
    /// </summary>
    public string FileName { get; init; } = default!;

    /// <summary>
    /// 插件名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 插件标题.
    /// </summary>
    public string Title { get; init; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = default!;

    /// <summary>
    /// 分类 ID.
    /// </summary>
    public int ClassifyId { get; init; }

    /// <summary>
    /// 是否公开.
    /// </summary>
    public bool IsPublic { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<ImportTeamOpenApiPluginCommand> validate)
    {
        validate.RuleFor(x => x.TeamId)
            .GreaterThan(0).WithMessage("团队 ID 必须大于 0.");

        validate.RuleFor(x => x.Name)
            .NotEmpty().WithMessage("插件名称长度在 2-30 之间.")
            .Length(2, 30).WithMessage("插件名称长度在 2-30 之间.")
            .Matches("^[a-zA-Z_]+$").WithMessage("插件名称只能包含字母下划线.");

        validate.RuleFor(x => x.Title)
            .NotEmpty().WithMessage("插件标题不能为空.")
            .Length(2, 20).WithMessage("插件标题长度在 2-20 之间.");

        validate.RuleFor(x => x.Description)
            .NotEmpty().WithMessage("插件描述长度在 2-255 之间.")
            .Length(2, 255).WithMessage("插件描述长度在 2-255 之间.");

        validate.RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("文件名称长度在 2-100 之间.")
            .Length(2, 100).WithMessage("文件名称长度在 2-100 之间.");

        validate.RuleFor(x => x.FileId)
            .GreaterThan(0).WithMessage("文件 ID 必须大于 0.");
    }
}
