using System;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.TeamPlugin.Commands;

/// <summary>
/// 导入/更新团队 OpenAPI 插件.
/// </summary>
public class SaveTeamOpenApiPluginCommand : IUserIdContext, IRequest<SimpleGuid>, IModelValidator<SaveTeamOpenApiPluginCommand>
{
    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 插件记录 id；更新时传入，新建为空.
    /// </summary>
    public Guid? PluginId { get; init; }

    /// <summary>
    /// 分类 id，0 表示未分类.
    /// </summary>
    public int ClassifyId { get; init; }

    /// <summary>
    /// 上传的 id.
    /// </summary>
    public long FileId { get; init; } = default!;

    /// <summary>
    /// 文件名称.
    /// </summary>
    public string FileName { get; init; } = default!;

    /// <summary>
    /// 插件名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 插件标题，可中文.
    /// </summary>
    public string Title { get; init; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = default!;

    /// <summary>
    /// Header 头部信息，值支持 <c>{变量名}</c> 引用团队变量，运行时按插件所属团队插值.
    /// </summary>
    public IReadOnlyCollection<KeyValueString> Header { get; init; } = Array.Empty<KeyValueString>();

    /// <summary>
    /// Query 参数，值支持 <c>{变量名}</c> 引用团队变量，运行时按插件所属团队插值.
    /// </summary>
    public IReadOnlyCollection<KeyValueString> Query { get; init; } = Array.Empty<KeyValueString>();

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SaveTeamOpenApiPluginCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
        validate.RuleFor(x => x.Name)
            .NotEmpty().WithMessage("插件名称长度在 2-30 之间.")
            .Length(2, 30).WithMessage("插件名称长度在 2-30 之间.")
            .Matches("^[a-zA-Z_]+$").WithMessage("插件名称只能包含字母下划线.");
        validate.RuleFor(x => x.Title)
            .NotEmpty().WithMessage("插件名称不能为空.")
            .Length(2, 20).WithMessage("插件名称长度在 2-20 之间.");
        validate.RuleFor(x => x.Description)
            .NotEmpty().WithMessage("插件描述长度在 2-255 之间.")
            .Length(2, 255).WithMessage("插件描述长度在 2-255 之间.");
        validate.RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("文件名称长度在 2-100 之间.")
            .Length(2, 100).WithMessage("文件名称长度在 2-100 之间.");
        validate.RuleFor(x => x.FileId)
            .GreaterThan(0).WithMessage("文件 ID 必须大于 0.");
        validate.RuleFor(x => x.Header)
            .Must(headers => headers.All(x => !string.IsNullOrWhiteSpace(x.Key)))
            .WithMessage("Header 的 key 不能为空.");
        validate.RuleFor(x => x.Query)
            .Must(queries => queries.All(x => !string.IsNullOrWhiteSpace(x.Key)))
            .WithMessage("Query 的 key 不能为空.");
    }
}
