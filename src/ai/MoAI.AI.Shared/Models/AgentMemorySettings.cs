using System;
using System.Text.Json;

namespace MoAI.AI.Models;

/// <summary>
/// Agent 应用长期记忆配置（对应 execution_settings.memory；为空表示未开启记忆）.
/// </summary>
public sealed class AgentMemorySettings
{
    /// <summary>
    /// 记忆类型：语义记忆（事实/偏好/结论）.
    /// </summary>
    public const int TypeSemantic = 0;

    /// <summary>
    /// 记忆类型：情景记忆（事件/经历/过程记录）.
    /// </summary>
    public const int TypeEpisodic = 1;

    /// <summary>
    /// 记忆类型：程序记忆（操作方法/经验教训/流程）.
    /// </summary>
    public const int TypeProcedural = 2;

    /// <summary>
    /// 默认向量维度（应用未显式配置时）.
    /// </summary>
    public const int DefaultEmbeddingDimensions = 1024;

    /// <summary>
    /// 默认单用户记忆条数上限.
    /// </summary>
    public const int DefaultMaxPerUser = 500;

    /// <summary>
    /// 默认检索条数.
    /// </summary>
    public const int DefaultRetrievalTopK = 5;

    /// <summary>
    /// 检索条数上限.
    /// </summary>
    public const int MaxRetrievalTopK = 50;

    /// <summary>
    /// 默认相似度阈值（余弦相似度 0~1）.
    /// </summary>
    public const double DefaultMinScore = 0.35;

    /// <summary>
    /// 用户记忆模式：未设置（历史遗留值），按开启生效.
    /// </summary>
    public const int UserModeFollowApp = 0;

    /// <summary>
    /// 用户记忆模式：对我关闭.
    /// </summary>
    public const int UserModeOff = 1;

    /// <summary>
    /// 用户记忆模式：开启（应用内记忆；历史值 2/3 同样按开启生效——跨应用功能已移除，各应用向量模型与维度不同无法统一检索）.
    /// </summary>
    public const int UserModeAppOnly = 2;

    /// <summary>
    /// 记忆去重相似度阈值：候选与既有记忆相似度达到该值时合并更新而非新增.
    /// </summary>
    public const double DedupeScoreThreshold = 0.92;

    /// <summary>
    /// 是否开启长期记忆.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 向量化模型 id（ai_model.id，须为 embedding 模型），开启后锁定；更换需先清空应用记忆.
    /// </summary>
    public Guid EmbeddingModelId { get; set; }

    /// <summary>
    /// 向量维度（1~2000，pgvector HNSW 上限）.
    /// </summary>
    public int EmbeddingDimensions { get; set; } = DefaultEmbeddingDimensions;

    /// <summary>
    /// 提取/检索语义记忆.
    /// </summary>
    public bool EnableSemantic { get; set; } = true;

    /// <summary>
    /// 提取/检索情景记忆.
    /// </summary>
    public bool EnableEpisodic { get; set; } = true;

    /// <summary>
    /// 提取/检索程序记忆.
    /// </summary>
    public bool EnableProcedural { get; set; } = true;

    /// <summary>
    /// 每轮对话后自动提取并合并记忆（mem0 式选择性写入）；关闭则仅可由 AI 通过工具显式保存.
    /// </summary>
    public bool AutoExtract { get; set; } = true;

    /// <summary>
    /// 每轮注入上下文与 memory_search 的默认召回条数（1~50）.
    /// </summary>
    public int RetrievalTopK { get; set; } = DefaultRetrievalTopK;

    /// <summary>
    /// 相似度阈值（0~1，低于该值的召回被过滤）.
    /// </summary>
    public double MinScore { get; set; } = DefaultMinScore;

    /// <summary>
    /// 单用户在该应用的记忆条数上限（10~10000），超限删除最旧记忆.
    /// </summary>
    public int MaxPerUser { get; set; } = DefaultMaxPerUser;

    /// <summary>
    /// 判断记忆类型是否启用.
    /// </summary>
    /// <param name="memoryType">记忆类型常量.</param>
    /// <returns>是否启用.</returns>
    public bool IsTypeEnabled(int memoryType) => memoryType switch
    {
        TypeSemantic => EnableSemantic,
        TypeEpisodic => EnableEpisodic,
        TypeProcedural => EnableProcedural,
        _ => false
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// 从 execution_settings JSON 解析 memory 配置节；缺失或非法返回 null（表示未配置）.
    /// </summary>
    /// <param name="executionSettingsJson">执行参数 JSON 文本.</param>
    /// <returns>记忆配置或 null.</returns>
    public static AgentMemorySettings? ParseExecutionSettings(string? executionSettingsJson)
    {
        if (string.IsNullOrWhiteSpace(executionSettingsJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(executionSettingsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("memory", out var memory)
                || memory.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return memory.Deserialize<AgentMemorySettings>(JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// 解析用户生效记忆模式：仅 关闭（1） / 开启（其余值，含未设置 0 与历史 2/3）两态.
    /// </summary>
    /// <param name="userMode">用户存储的记忆模式.</param>
    /// <returns>生效模式常量.</returns>
    public int ResolveUserMode(int userMode) => userMode == UserModeOff ? UserModeOff : UserModeAppOnly;
}
