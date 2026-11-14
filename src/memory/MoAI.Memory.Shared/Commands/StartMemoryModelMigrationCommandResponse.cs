using System;

namespace MoAI.Memory.Commands;

/// <summary>
/// 发起长期记忆向量化模型迁移响应.
/// </summary>
public class StartMemoryModelMigrationCommandResponse
{
    /// <summary>
    /// 迁移任务 id（轮询 <c>GET /memory/model-migration</c> 查询进度）.
    /// </summary>
    public Guid TaskId { get; init; }
}
