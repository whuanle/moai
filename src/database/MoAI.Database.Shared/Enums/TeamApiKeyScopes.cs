namespace MoAI.Database.Enums;

/// <summary>
/// 外部接入功能范围位标记，与 access_app.scopes 位或组合存储；接入的授权范围 = 勾选的功能块.
/// </summary>
[Flags]
public enum TeamApiKeyScopes
{
    /// <summary>
    /// 无任何范围（无效，创建时至少一个）.
    /// </summary>
    None = 0,

    /// <summary>
    /// 模型网关：/api/aigateway/{"{teamId}"}/v1/* 开放接口.
    /// </summary>
    Model = 1,

    /// <summary>
    /// 知识库读：外部接口查询/搜索/读取知识库与文档内容.
    /// </summary>
    WikiRead = 2,

    /// <summary>
    /// 知识库写：外部接口上传/提取/切割/向量化/删除知识库文档.
    /// </summary>
    WikiWrite = 4,

    /// <summary>
    /// 应用对话：key 可换取用户 token 并访问外部应用会话/对话外部接口.
    /// </summary>
    AppChat = 32,

    /// <summary>
    /// 知识库 MCP（预留，功能未上线）.
    /// </summary>
    WikiMcp = 16,

    /// <summary>
    /// 知识图谱读：外部接口查询图谱模式、节点、边.
    /// </summary>
    KgRead = 64,

    /// <summary>
    /// 知识图谱写：外部接口创建/更新/删除节点、边与类型.
    /// </summary>
    KgWrite = 128,

    /// <summary>
    /// 知识图谱 MCP：访问 /api/external/knowledge-graph/{kgId}/mcp 只读工具（图谱列表/schema/节点搜索/向量召回）.
    /// </summary>
    KgMcp = 256,
}

/// <summary>
/// <see cref="TeamApiKeyScopes"/> 与对外 scope 代码（小写下划线串）互转.
/// </summary>
public static class TeamApiKeyScopeCodes
{
    /// <summary>
    /// 外部资源维度掩码：token/直连上下文可携带的范围（model/app_chat 是接入层概念，不进入）.
    /// </summary>
    public const TeamApiKeyScopes ExternalDimensions = TeamApiKeyScopes.WikiRead | TeamApiKeyScopes.WikiWrite | TeamApiKeyScopes.WikiMcp | TeamApiKeyScopes.KgRead | TeamApiKeyScopes.KgWrite | TeamApiKeyScopes.KgMcp;

    /// <summary>
    /// 应用接入（access_app）允许勾选的范围：模型网关 + 知识库/知识图谱维度 + 应用对话.
    /// </summary>
    public const TeamApiKeyScopes AccessAppAllowed = TeamApiKeyScopes.Model | TeamApiKeyScopes.WikiRead | TeamApiKeyScopes.WikiWrite | TeamApiKeyScopes.WikiMcp | TeamApiKeyScopes.AppChat | TeamApiKeyScopes.KgRead | TeamApiKeyScopes.KgWrite | TeamApiKeyScopes.KgMcp;

    /// <summary>
    /// 应用接入「未传 scopes」时的默认范围：外部资源全量（存量行为口径，不含需显式勾选的 model 直连）.
    /// </summary>
    public const TeamApiKeyScopes AccessAppDefault = ExternalDimensions | TeamApiKeyScopes.AppChat;

    /// <summary>
    /// scope 代码 → 标记位.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, TeamApiKeyScopes> Codes = new Dictionary<string, TeamApiKeyScopes>(StringComparer.Ordinal)
    {
        ["model"] = TeamApiKeyScopes.Model,
        ["wiki_read"] = TeamApiKeyScopes.WikiRead,
        ["wiki_write"] = TeamApiKeyScopes.WikiWrite,
        ["app_chat"] = TeamApiKeyScopes.AppChat,
        ["kg_read"] = TeamApiKeyScopes.KgRead,
        ["kg_write"] = TeamApiKeyScopes.KgWrite,
        ["kg_mcp"] = TeamApiKeyScopes.KgMcp,
        ["wiki_mcp"] = TeamApiKeyScopes.WikiMcp,
    };

    /// <summary>
    /// 将标记位转为 scope 代码列表（按位定义顺序，稳定输出）.
    /// </summary>
    /// <param name="scopes">标记位.</param>
    /// <returns>返回 scope 代码列表.</returns>
    public static List<string> ToCodes(TeamApiKeyScopes scopes)
    {
        var codes = new List<string>();
        foreach (var pair in Codes)
        {
            if (pair.Value != TeamApiKeyScopes.None && scopes.HasFlag(pair.Value))
            {
                codes.Add(pair.Key);
            }
        }

        return codes;
    }

    /// <summary>
    /// 将 scope 代码列表转为标记位；未知代码返回 false.
    /// </summary>
    /// <param name="codes">scope 代码列表.</param>
    /// <param name="scopes">转换结果.</param>
    /// <returns>是否全部代码合法.</returns>
    public static bool TryParseCodes(IEnumerable<string>? codes, out TeamApiKeyScopes scopes)
    {
        scopes = TeamApiKeyScopes.None;
        if (codes == null)
        {
            return false;
        }

        foreach (var code in codes)
        {
            if (!Codes.TryGetValue(code, out var flag) || flag == TeamApiKeyScopes.None)
            {
                scopes = TeamApiKeyScopes.None;
                return false;
            }

            scopes |= flag;
        }

        return scopes != TeamApiKeyScopes.None;
    }


    /// <summary>
    /// 解析应用接入（access_app）允许的 scope 代码列表（model/wiki_read/wiki_write/app_chat/wiki_mcp/kg_*），
    /// 越维代码返回 false（空列表合法 = 纯对话接入）.
    /// </summary>
    /// <param name="codes">scope 代码列表.</param>
    /// <param name="scopes">转换结果.</param>
    /// <returns>是否全部代码合法且属于应用接入允许范围.</returns>
    public static bool TryParseAccessAppCodes(IEnumerable<string>? codes, out TeamApiKeyScopes scopes)
    {
        if (!TryParseCodes(codes, out scopes))
        {
            return false;
        }

        if ((scopes & ~AccessAppAllowed) != TeamApiKeyScopes.None)
        {
            scopes = TeamApiKeyScopes.None;
            return false;
        }

        return true;
    }

    /// <summary>
    /// 解析 scope 声明串（逗号分隔代码），未知片段忽略.
    /// </summary>
    /// <param name="value">逗号分隔的 scope 代码.</param>
    /// <returns>返回标记位，全未知时为 None.</returns>
    public static TeamApiKeyScopes ParseClaimValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return TeamApiKeyScopes.None;
        }

        var scopes = TeamApiKeyScopes.None;
        foreach (var piece in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Codes.TryGetValue(piece, out var flag))
            {
                scopes |= flag;
            }
        }

        return scopes;
    }

    /// <summary>
    /// 转为逗号分隔的 scope 代码串（用于 token scope 声明）.
    /// </summary>
    /// <param name="scopes">标记位.</param>
    /// <returns>返回逗号分隔代码串，None 为空串.</returns>
    public static string ToClaimValue(TeamApiKeyScopes scopes) => string.Join(',', ToCodes(scopes));
}
