using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoAI.KnowledgeGraph.Models;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 知识图谱 Cypher 只读访问服务：供动态插件 kg_cypher_query 消费，托管图强制 $kgId 隔离，接入图按库路由只读会话.
/// </summary>
public interface IKgCypherAccessService
{
    /// <summary>
    /// 执行只读 Cypher 查询（文本守卫由调用方先行校验；托管图在此强制 $kgId）.
    /// </summary>
    /// <remarks>
    /// 调用方契约：
    /// 1. 只读文本守卫与图谱团队归属校验均由调用方负责，本服务不做鉴权（防 IDOR 误用）.
    /// 2. 托管模式对结果中的节点/关系图元素做 kgId 结果侧校验（缺失或不属于本图谱即 403）；
    ///    但纯属性投影（如 RETURN n.name）不经过该校验，残余风险由调用方的提示词与审核覆盖.
    /// </remarks>
    /// <param name="knowledgeGraphId">图谱 id.</param>
    /// <param name="cypher">已过守卫的只读 Cypher.</param>
    /// <param name="parameters">查询参数（键不含 $ 前缀；托管图的 kgId 键被服务端覆盖注入）.</param>
    /// <param name="maxRows">返回行数上限.</param>
    /// <param name="timeoutSeconds">查询超时秒数.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>表格化结果.</returns>
    Task<KgCypherQueryResult> ExecuteQueryAsync(long knowledgeGraphId, string cypher, IReadOnlyDictionary<string, object?>? parameters, int maxRows, int timeoutSeconds, CancellationToken cancellationToken);

    /// <summary>
    /// 获取图谱自描述摘要.
    /// </summary>
    /// <param name="knowledgeGraphId">图谱 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>图结构摘要.</returns>
    Task<KgCypherSchemaDigest> GetSchemaDigestAsync(long knowledgeGraphId, CancellationToken cancellationToken);
}
