using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoAI.Memory.Models;

namespace MoAI.Memory.Services;

/// <summary>
/// Agent 应用长期记忆领域服务：按用户隔离的向量化记忆（语义/情景/程序），
/// 支撑应用内与跨应用检索、选择性与合并式写入、清空与列举.
/// </summary>
public interface IMemoryService
{
    /// <summary>
    /// 解析用户在某应用的记忆配置与生效模式；应用未开启记忆返回 null.
    /// </summary>
    /// <param name="appId">应用 id.</param>
    /// <param name="userId">用户 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>记忆解析结果或 null.</returns>
    Task<MemoryResolution?> ResolveAsync(Guid appId, long userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 向量检索记忆：本应用 + （允许时）跨应用候选，按相似度归并.
    /// </summary>
    /// <param name="query">检索查询.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>命中的记忆条目.</returns>
    Task<IReadOnlyList<MemoryItem>> SearchAsync(MemorySearchQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存一条记忆：与既有高相似记忆合并（更新），否则新增；受类型开关与单用户容量约束.
    /// </summary>
    /// <param name="request">保存请求.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>保存后的记忆条目.</returns>
    Task<MemoryItem> SaveAsync(MemorySaveRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除本人的一条记忆.
    /// </summary>
    /// <param name="appId">应用 id.</param>
    /// <param name="memoryId">记忆 id.</param>
    /// <param name="userId">用户 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>是否删除成功.</returns>
    Task<bool> DeleteAsync(Guid appId, Guid memoryId, long userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 清空本应用内本人的全部记忆.
    /// </summary>
    /// <param name="appId">应用 id.</param>
    /// <param name="userId">用户 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>清除条数.</returns>
    Task<int> ClearAppUserAsync(Guid appId, long userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 清空应用内全部用户的记忆（团队管理员操作）.
    /// </summary>
    /// <param name="appId">应用 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>清除条数.</returns>
    Task<int> ClearAppAsync(Guid appId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 分页列举本应用内本人的记忆（按创建时间倒序）.
    /// </summary>
    /// <param name="appId">应用 id.</param>
    /// <param name="userId">用户 id.</param>
    /// <param name="page">页码（1 起）.</param>
    /// <param name="pageSize">页大小（1~100）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>记忆条目与总数.</returns>
    Task<(IReadOnlyList<MemoryItem> Items, int Total)> ListMineAsync(Guid appId, long userId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// 应用记忆统计：全部用户的记忆总条数与已有记忆的用户数（shard 聚合）.
    /// </summary>
    /// <param name="appId">应用 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回 (总条数, 用户数).</returns>
    Task<(int TotalMemories, int UserCount)> GetAppStatsAsync(Guid appId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除应用的记忆集合（应用删除时清理；表不存在时静默跳过）.
    /// </summary>
    /// <param name="appId">应用 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task DeleteAppCollectionAsync(Guid appId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 对一个会话执行 mem0 式两段记忆提取（事实抽取 → 合并决策），幂等可重复投递.
    /// </summary>
    /// <param name="sessionId">会话 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task ExtractAndConsolidateAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 读取本人的一条应用内记忆（不存在或非本人返回 null）.
    /// </summary>
    /// <param name="appId">应用 id.</param>
    /// <param name="userId">用户 id.</param>
    /// <param name="memoryId">记忆 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>记忆条目或 null.</returns>
    Task<MemoryItem?> GetAppMemoryAsync(Guid appId, long userId, Guid memoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 解析平台记忆设置（系统设置 MEMORY_*：总开关与统一向量化模型/维度）；未配置返回 Enabled=false.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>平台记忆设置.</returns>
    Task<MemoryPlatformSettings> GetPlatformSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 编辑本人的一条应用内记忆：保留原 id 与创建时间，内容重嵌向量（同应用配置模型）.
    /// </summary>
    /// <param name="appId">应用 id.</param>
    /// <param name="userId">用户 id.</param>
    /// <param name="memoryId">记忆 id.</param>
    /// <param name="content">新内容.</param>
    /// <param name="memoryType">记忆类型.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>是否更新成功（记忆不存在或非本人返回 false）.</returns>
    Task<bool> UpdateAppMemoryAsync(Guid appId, long userId, Guid memoryId, string content, int memoryType, CancellationToken cancellationToken = default);

    /// <summary>
    /// 分页列举本人的全局记忆（按创建时间倒序）.
    /// </summary>
    /// <param name="userId">用户 id.</param>
    /// <param name="page">页码（1 起）.</param>
    /// <param name="pageSize">页大小.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>记忆条目与总数.</returns>
    Task<(IReadOnlyList<MemoryItem> Items, int Total)> ListGlobalMineAsync(long userId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增本人的一条全局记忆：与既有高相似记忆合并，受单用户容量约束；平台未开启时抛 BusinessException.
    /// </summary>
    /// <param name="userId">用户 id.</param>
    /// <param name="content">记忆内容.</param>
    /// <param name="memoryType">记忆类型.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>保存后的记忆条目.</returns>
    Task<MemoryItem> SaveGlobalAsync(long userId, string content, int memoryType, CancellationToken cancellationToken = default);

    /// <summary>
    /// 编辑本人的一条全局记忆：保留原 id 与创建时间，内容按全局模型重嵌向量.
    /// </summary>
    /// <param name="userId">用户 id.</param>
    /// <param name="memoryId">记忆 id.</param>
    /// <param name="content">新内容.</param>
    /// <param name="memoryType">记忆类型.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>是否更新成功.</returns>
    Task<bool> UpdateGlobalAsync(long userId, Guid memoryId, string content, int memoryType, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除本人的一条全局记忆.
    /// </summary>
    /// <param name="userId">用户 id.</param>
    /// <param name="memoryId">记忆 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>是否删除成功.</returns>
    Task<bool> DeleteGlobalAsync(long userId, Guid memoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 清空本人的全部全局记忆.
    /// </summary>
    /// <param name="userId">用户 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>清除条数.</returns>
    Task<int> ClearGlobalUserAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 全局记忆统计：全部用户的记忆总条数与已有记忆的用户数（shard 聚合）.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回 (总条数, 用户数).</returns>
    Task<(int TotalMemories, int UserCount)> GetGlobalStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 清空全部用户的全局记忆并删除全局集合（root 操作）.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task ClearGlobalAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 清空平台全部记忆：所有应用记忆 + 全部全局记忆（root 操作，解除统一向量化模型/维度锁定）.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>清除的应用记忆总条数（不含全局记忆）.</returns>
    Task<int> ClearAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 全局记忆向量检索（个人跨应用；平台未开启返回空）.
    /// </summary>
    /// <param name="userId">用户 id.</param>
    /// <param name="queryText">查询文本.</param>
    /// <param name="topK">召回条数上限.</param>
    /// <param name="minScore">相似度阈值.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>命中的全局记忆条目.</returns>
    Task<IReadOnlyList<MemoryItem>> SearchGlobalAsync(long userId, string queryText, int topK, double minScore, CancellationToken cancellationToken = default);
}

/// <summary>
/// 记忆提取触发端口：会话落库后按需投递异步提取任务（AI 模块调用，实现方在 Memory 模块）.
/// </summary>
public interface IMemoryExtractionTrigger
{
    /// <summary>
    /// 判断会话是否满足自动提取条件并投递提取任务；不满足或投递失败时静默跳过（记忆为尽力而为）.
    /// </summary>
    /// <param name="sessionId">会话 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>是否投递了任务.</returns>
    Task<bool> TriggerAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
