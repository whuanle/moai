using System;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using System.Text.Json.Serialization;

namespace MoAI.Memory.Commands;

/// <summary>
/// 发起长期记忆向量化模型迁移：用新的向量化模型与维度重建平台全部记忆向量（应用记忆 + 全局记忆），
/// RabbitMQ 异步执行，期间记忆写入阻断、检索不受影响；仅超级管理员.
/// </summary>
public class StartMemoryModelMigrationCommand : IRequest<StartMemoryModelMigrationCommandResponse>, IModelValidator<StartMemoryModelMigrationCommand>, IUserIdContext
{
    /// <summary>
    /// 目标向量化模型 id（须为公开启用中的 embedding 模型）.
    /// </summary>
    public Guid ModelId { get; init; }

    /// <summary>
    /// 目标向量维度（1~2000）.
    /// </summary>
    public int Dimensions { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<StartMemoryModelMigrationCommand> validate)
    {
        validate.RuleFor(x => x.ModelId).NotEmpty().WithMessage("目标向量化模型不能为空.");
        validate.RuleFor(x => x.Dimensions).InclusiveBetween(1, 2000).WithMessage("向量维度必须是 1~2000 之间的整数.");
    }
}
