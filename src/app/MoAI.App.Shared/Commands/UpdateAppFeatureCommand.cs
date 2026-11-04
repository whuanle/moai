using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.App.Queries.Responses;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Commands;

/// <summary>
/// 更新应用对外功能开关（ACP/A2A）：开启时自动生成/复用该应用唯一的团队资源 key（ACP/A2A 共用一把），关闭不影响已有 key（端点按开关拒绝）；需要团队 Admin 及以上角色.
/// </summary>
public class UpdateAppFeatureCommand : IRequest<AppFeatureResponse>, IUserIdContext, IModelValidator<UpdateAppFeatureCommand>
{
    /// <summary>
    /// 应用 id，由 Controller 从路由参数回填.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 是否开启 ACP 接入.
    /// </summary>
    public bool IsAcpEnabled { get; init; }

    /// <summary>
    /// 是否开启 A2A 接入.
    /// </summary>
    public bool IsA2aEnabled { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateAppFeatureCommand> validate)
    {
        // AppId 由 Controller 从路由参数回填，不做校验.
    }
}
