using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.App.Models;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Commands;

/// <summary>
/// 保存外部应用访问点配置（整体替换），需要团队 Admin 及以上角色，仅外部应用.
/// </summary>
public class SaveAppAccessPointCommand : IRequest<EmptyCommandResponse>, IUserIdContext, IModelValidator<SaveAppAccessPointCommand>
{
    /// <summary>
    /// 应用 id（路由参数）.
    /// </summary>
    [JsonIgnore]
    public Guid AppId { get; init; }

    /// <summary>
    /// 面板标题，空则用应用名.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// 欢迎语/副标题.
    /// </summary>
    public string? Subtitle { get; init; }

    /// <summary>
    /// 输入框占位文案.
    /// </summary>
    public string? Placeholder { get; init; }

    /// <summary>
    /// 主题色，#RRGGBB，可为空.
    /// </summary>
    public string? PrimaryColor { get; init; }

    /// <summary>
    /// 悬浮位置.
    /// </summary>
    public AccessPointPosition Position { get; init; }

    /// <summary>
    /// 悬浮按钮文案，空则用图标.
    /// </summary>
    public string? LauncherText { get; init; }

    /// <summary>
    /// 头像 objectKey，可为空；必须是由存储直传管线完成上传并登记的文件.
    /// </summary>
    public string? Avatar { get; init; }

    /// <summary>
    /// 面板宽度 px（280~640）.
    /// </summary>
    public int PanelWidth { get; init; }

    /// <summary>
    /// 面板高度 px（360~900）.
    /// </summary>
    public int PanelHeight { get; init; }

    /// <summary>
    /// 是否默认展开.
    /// </summary>
    public bool DefaultOpen { get; init; }

    /// <summary>
    /// 是否启用访问点.
    /// </summary>
    public bool Enabled { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SaveAppAccessPointCommand> validate)
    {
        validate.RuleFor(x => x.Title).MaximumLength(100).WithMessage("面板标题最长 100 个字符.");
        validate.RuleFor(x => x.Subtitle).MaximumLength(255).WithMessage("欢迎语最长 255 个字符.");
        validate.RuleFor(x => x.Placeholder).MaximumLength(100).WithMessage("输入框占位最长 100 个字符.");
        validate.RuleFor(x => x.PrimaryColor)
            .Matches("^#[0-9a-fA-F]{6}$")
            .When(x => !string.IsNullOrEmpty(x.PrimaryColor))
            .WithMessage("主题色必须为 #RRGGBB 格式.");
        validate.RuleFor(x => x.LauncherText).MaximumLength(50).WithMessage("悬浮按钮文案最长 50 个字符.");
        validate.RuleFor(x => x.Avatar).MaximumLength(255).WithMessage("头像 objectKey 最长 255 个字符.");
        validate.RuleFor(x => x.PanelWidth).InclusiveBetween(280, 640).WithMessage("面板宽度必须在 280~640 之间.");
        validate.RuleFor(x => x.PanelHeight).InclusiveBetween(360, 900).WithMessage("面板高度必须在 360~900 之间.");
    }
}
