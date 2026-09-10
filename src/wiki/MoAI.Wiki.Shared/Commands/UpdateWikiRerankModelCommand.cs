using System;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Commands;

/// <summary>
/// 更新知识库重排序模型配置，需要团队 Admin 及以上角色.
/// 重排序模型为可选项：传 null 表示不使用重排序（解绑）.
/// 与向量化配置解耦：即使知识库已锁定（IsLock，已有文档被向量化），仍可绑定/更换/解绑.
/// </summary>
public class UpdateWikiRerankModelCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateWikiRerankModelCommand>
{
    /// <summary>
    /// 知识库 id，由 Controller 从路由参数回填.
    /// </summary>
    public long WikiId { get; init; }

    /// <summary>
    /// 重排序模型 id；为空表示不使用重排序.
    /// </summary>
    public Guid? RerankModelId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateWikiRerankModelCommand> validate)
    {
        // 重排序模型可选，因此无必填校验；WikiId 由 Controller 从路由参数回填.
    }
}
