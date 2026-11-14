using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using System.Text.Json.Serialization;

namespace MoAI.Memory.Commands;

/// <summary>
/// 清空平台全部记忆：所有应用记忆 + 全部全局记忆（仅超级管理员；解除统一向量化模型/维度锁定）.
/// </summary>
public class ClearAllMemoryCommand : IRequest<EmptyCommandResponse>, IModelValidator<ClearAllMemoryCommand>, IUserIdContext
{
    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<ClearAllMemoryCommand> validate)
    {
    }
}
