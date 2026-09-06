namespace MoAI.Database.Enums;

/// <summary>
/// 模型使用来源类型，与 ai_model_token_audit.use_type / ai_model_usage_log.use_type 对应，新增类型依次递增.
/// </summary>
public enum AiModelUseType
{
    /// <summary>
    /// 个人会话.
    /// </summary>
    UserConversation = 0,

    /// <summary>
    /// 应用.
    /// </summary>
    App = 1,

    /// <summary>
    /// 知识库.
    /// </summary>
    Knowledge = 2,

    /// <summary>
    /// 工作流.
    /// </summary>
    Workflow = 3,

    /// <summary>
    /// 开放接口（团队网关 API Key 调用），use_resource_id 记录 team_api_key.id.
    /// </summary>
    OpenApi = 4,
}
