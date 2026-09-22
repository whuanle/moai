import { getApiClient } from '@/api/kiota'
import { uploadImageWithKey } from '@/utils/storage'

export interface KnowledgeGraphItem {
  kgId?: string | number | null
  teamId?: string | number | null
  name?: string | null
  description?: string | null
  templateKey?: string | null
  mode?: string | null
  database?: string | null
  readOnly?: boolean | null
  avatarPath?: string | null
  createTime?: string | null
}

export interface KnowledgeGraphListResult {
  teamId?: string | number | null
  myRole?: number | null
  enabled?: boolean | null
  items?: KnowledgeGraphItem[] | null
}

export interface KnowledgeGraphDetail {
  kgId?: string | number | null
  teamId?: string | number | null
  name?: string | null
  description?: string | null
  templateKey?: string | null
  mode?: string | null
  database?: string | null
  readOnly?: boolean | null
  myRole?: number | null
  enabled?: boolean | null
  avatarPath?: string | null
  createTime?: string | null
  /** 向量化模型 id（托管图配置后参与向量检索；未配置为空） */
  embeddingModelId?: string | null
  /** 向量维度（1-2000） */
  embeddingDimensions?: number | null
}

export interface KnowledgeGraphTemplateItem {
  key?: string | null
  name?: string | null
  description?: string | null
  entityTypes?: string[] | null
  relationTypes?: string[] | null
}

export interface KnowledgeGraphEntityTypeItem {
  entityTypeId?: string | number | null
  name?: string | null
  color?: string | null
  description?: string | null
  count?: string | number | null
  /** 属性定义（托管图） */
  properties?: KnowledgeGraphEntityTypeProperty[] | null
}

/** 实体类型属性定义 */
export interface KnowledgeGraphEntityTypeProperty {
  name?: string | null
  /** string / number / boolean / date */
  type?: string | null
  required?: boolean | null
  description?: string | null
}

export interface KnowledgeGraphRelationTypeItem {
  relationTypeId?: string | number | null
  name?: string | null
  color?: string | null
  description?: string | null
  sourceTypeId?: string | number | null
  targetTypeId?: string | number | null
  count?: string | number | null
}

export interface KnowledgeGraphSchema {
  mode?: string | null
  database?: string | null
  readOnly?: boolean | null
  entityTypes?: KnowledgeGraphEntityTypeItem[] | null
  relationTypes?: KnowledgeGraphRelationTypeItem[] | null
  propertyKeys?: string[] | null
  /** 内省相对上次基线的变化（仅接入图新鲜内省时返回） */
  changes?: KnowledgeGraphIntrospectionDiff | null
  fromCache?: boolean | null
}

/** 接入图内省变化 */
export interface KnowledgeGraphIntrospectionDiff {
  addedLabels?: string[] | null
  removedLabels?: string[] | null
  addedRelationTypes?: string[] | null
  removedRelationTypes?: string[] | null
}

export interface KnowledgeGraphNodeItem {
  nodeId?: string | null
  entityTypeId?: string | number | null
  /** 接入图：节点首个标签 */
  entityLabel?: string | null
  name?: string | null
  description?: string | null
  /** 实例属性值（键为实体类型定义的属性名，值均为字符串） */
  properties?: Record<string, string> | null
}

/**
 * Kiota 把后端 Dictionary<string,string> 映射为接口对象时，真实键值会落在 additionalData 里。
 * 此处归一化为纯键值字典，节点属性的所有消费方（图览详情/实例列表/邻接）统一走这里。
 */
function flattenNodeProperties(raw: unknown): Record<string, string> {
  if (!raw || typeof raw !== 'object') return {}
  const inner = (raw as { additionalData?: Record<string, string> }).additionalData
  return inner ?? (raw as Record<string, string>)
}

export interface KnowledgeGraphEdgeItem {
  edgeId?: string | null
  relationTypeId?: string | number | null
  /** 接入图：关系类型名 */
  relationName?: string | null
  sourceNodeId?: string | null
  targetNodeId?: string | null
}

export interface PagedResult<T> { items?: T[] | null; total?: string | number | null }

export async function getKnowledgeGraphs(teamId: number): Promise<KnowledgeGraphListResult> {
  const client = getApiClient()
  const res = await client.api.knowledgeGraph.list.get({ queryParameters: { teamId: String(teamId) } })
  return { teamId: res?.teamId, myRole: res?.myRole, enabled: res?.enabled ?? false, items: res?.items ?? [] }
}

export async function createKnowledgeGraph(payload: {
  teamId: number
  name: string
  description?: string
  mode?: 'managed' | 'connected'
  templateKey?: string | null
  database?: string | null
}): Promise<number> {
  const client = getApiClient()
  const res = await client.api.knowledgeGraph.post({
    teamId: String(payload.teamId),
    name: payload.name,
    description: payload.description,
    mode: payload.mode ?? 'managed',
    templateKey: payload.templateKey ?? undefined,
    database: payload.database ?? undefined,
  })
  return Number(res?.value ?? 0)
}

export async function getKnowledgeGraphTemplates(): Promise<KnowledgeGraphTemplateItem[]> {
  const client = getApiClient()
  const res = await client.api.knowledgeGraph.templates.get()
  return res?.items ?? []
}

export async function getKnowledgeGraphDetail(kgId: number): Promise<KnowledgeGraphDetail> {
  const client = getApiClient()
  const res = await client.api.knowledgeGraph.byId(String(kgId)).get()
  return { ...res, avatarPath: res?.avatarPath ?? '' } as KnowledgeGraphDetail
}

export async function updateKnowledgeGraph(kgId: number, payload: { name: string; description?: string }): Promise<void> {
  const client = getApiClient()
  await client.api.knowledgeGraph.byId(String(kgId)).put({ name: payload.name, description: payload.description })
}

export async function deleteKnowledgeGraph(kgId: number): Promise<void> {
  const client = getApiClient()
  await client.api.knowledgeGraph.byId(String(kgId)).delete()
}

export async function setKnowledgeGraphAvatar(kgId: number, objectKey: string): Promise<void> {
  const client = getApiClient()
  await client.api.knowledgeGraph.byId(String(kgId)).avatar.post({ objectKey })
}

/** 上传图片并设为图谱头像（走存储直传管线），返回公开访问地址 */
export async function uploadKnowledgeGraphAvatar(kgId: number, file: File): Promise<string> {
  const { objectKey, url } = await uploadImageWithKey(file)
  await setKnowledgeGraphAvatar(kgId, objectKey)
  return url
}

export async function getKnowledgeGraphSchema(kgId: number, refresh = false): Promise<KnowledgeGraphSchema> {
  const client = getApiClient()
  const res = await client.api.knowledgeGraph.byId(String(kgId)).schema.get({ queryParameters: { refresh } })
  return {
    mode: res?.mode,
    database: res?.database,
    readOnly: res?.readOnly,
    entityTypes: res?.entityTypes ?? [],
    relationTypes: res?.relationTypes ?? [],
    propertyKeys: res?.propertyKeys ?? [],
    changes: res?.changes
      ? {
          addedLabels: res.changes.addedLabels ?? [],
          removedLabels: res.changes.removedLabels ?? [],
          addedRelationTypes: res.changes.addedRelationTypes ?? [],
          removedRelationTypes: res.changes.removedRelationTypes ?? [],
        }
      : null,
    fromCache: res?.fromCache ?? false,
  }
}

export async function createEntityType(
  kgId: number,
  payload: { name: string; color?: string; description?: string; properties?: KnowledgeGraphEntityTypeProperty[] },
): Promise<number> {
  const client = getApiClient()
  const res = await client.api.knowledgeGraph.byId(String(kgId)).entityTypes.post({
    name: payload.name,
    color: payload.color,
    description: payload.description,
    properties: payload.properties?.map((x) => ({ name: x.name ?? '', type: x.type ?? 'string', required: x.required ?? false, description: x.description ?? '' })),
  })
  return Number(res?.value ?? 0)
}

export async function updateEntityType(
  kgId: number,
  entityTypeId: number,
  payload: { name: string; color?: string; description?: string; properties?: KnowledgeGraphEntityTypeProperty[] },
): Promise<void> {
  const client = getApiClient()
  await client.api.knowledgeGraph.byId(String(kgId)).entityTypes.byTypeId(String(entityTypeId)).put({
    name: payload.name,
    color: payload.color,
    description: payload.description,
    properties: payload.properties?.map((x) => ({ name: x.name ?? '', type: x.type ?? 'string', required: x.required ?? false, description: x.description ?? '' })),
  })
}

export async function deleteEntityType(kgId: number, entityTypeId: number): Promise<void> {
  const client = getApiClient()
  await client.api.knowledgeGraph.byId(String(kgId)).entityTypes.byTypeId(String(entityTypeId)).delete()
}

export async function createRelationType(kgId: number, payload: { name: string; color?: string; description?: string; sourceTypeId?: number | null; targetTypeId?: number | null }): Promise<number> {
  const client = getApiClient()
  const res = await client.api.knowledgeGraph.byId(String(kgId)).relationTypes.post({
    ...payload,
    sourceTypeId: payload.sourceTypeId != null ? String(payload.sourceTypeId) : undefined,
    targetTypeId: payload.targetTypeId != null ? String(payload.targetTypeId) : undefined,
  })
  return Number(res?.value ?? 0)
}

export async function updateRelationType(kgId: number, relationTypeId: number, payload: { name: string; color?: string; description?: string; sourceTypeId?: number | null; targetTypeId?: number | null }): Promise<void> {
  const client = getApiClient()
  await client.api.knowledgeGraph.byId(String(kgId)).relationTypes.byTypeId(String(relationTypeId)).put({
    ...payload,
    sourceTypeId: payload.sourceTypeId != null ? String(payload.sourceTypeId) : undefined,
    targetTypeId: payload.targetTypeId != null ? String(payload.targetTypeId) : undefined,
  })
}

export async function deleteRelationType(kgId: number, relationTypeId: number): Promise<void> {
  const client = getApiClient()
  await client.api.knowledgeGraph.byId(String(kgId)).relationTypes.byTypeId(String(relationTypeId)).delete()
}

export async function getKnowledgeGraphNodes(
  kgId: number,
  params: { entityTypeId?: number | null; keyword?: string; pageNo?: number; pageSize?: number },
): Promise<PagedResult<KnowledgeGraphNodeItem>> {
  const client = getApiClient()
  const res = await client.api.knowledgeGraph.byId(String(kgId)).nodes.list.post({
    entityTypeId: params.entityTypeId != null ? String(params.entityTypeId) : undefined,
    keyword: params.keyword,
    pageNo: params.pageNo ?? 1,
    pageSize: params.pageSize ?? 20,
  })
  return { items: (res?.items ?? []).map((x) => ({ ...x, properties: flattenNodeProperties(x.properties) }) as KnowledgeGraphNodeItem), total: Number(res?.total ?? 0) }
}

export async function createKnowledgeGraphNode(
  kgId: number,
  payload: { entityTypeId: number; name: string; description?: string; properties?: Record<string, string> },
): Promise<string> {
  const client = getApiClient()
  const res = await client.api.knowledgeGraph.byId(String(kgId)).nodes.post({
    entityTypeId: String(payload.entityTypeId),
    name: payload.name,
    description: payload.description,
    properties: payload.properties,
  })
  return String(res?.value ?? '')
}

export async function updateKnowledgeGraphNode(
  kgId: number,
  nodeId: string,
  payload: { entityTypeId: number; name: string; description?: string; properties?: Record<string, string> },
): Promise<void> {
  const client = getApiClient()
  await client.api.knowledgeGraph.byId(String(kgId)).nodes.byNodeId(nodeId).put({
    entityTypeId: String(payload.entityTypeId),
    name: payload.name,
    description: payload.description,
    properties: payload.properties,
  })
}

export async function deleteKnowledgeGraphNode(kgId: number, nodeId: string): Promise<void> {
  const client = getApiClient()
  await client.api.knowledgeGraph.byId(String(kgId)).nodes.byNodeId(nodeId).delete()
}

export async function getKnowledgeGraphEdges(
  kgId: number,
  params: { relationTypeId?: number | null; nodeId?: string; pageNo?: number; pageSize?: number },
): Promise<PagedResult<KnowledgeGraphEdgeItem>> {
  const client = getApiClient()
  const res = await client.api.knowledgeGraph.byId(String(kgId)).edges.list.post({
    relationTypeId: params.relationTypeId != null ? String(params.relationTypeId) : undefined,
    nodeId: params.nodeId,
    pageNo: params.pageNo ?? 1,
    pageSize: params.pageSize ?? 20,
  })
  return { items: res?.items ?? [], total: Number(res?.total ?? 0) }
}

export async function createKnowledgeGraphEdge(kgId: number, payload: { relationTypeId: number; sourceNodeId: string; targetNodeId: string }): Promise<string> {
  const client = getApiClient()
  const res = await client.api.knowledgeGraph.byId(String(kgId)).edges.post({
    relationTypeId: String(payload.relationTypeId),
    sourceNodeId: payload.sourceNodeId,
    targetNodeId: payload.targetNodeId,
  })
  return String(res?.value ?? '')
}

export async function updateKnowledgeGraphEdge(kgId: number, edgeId: string, payload: { relationTypeId: number }): Promise<void> {
  const client = getApiClient()
  await client.api.knowledgeGraph.byId(String(kgId)).edges.byEdgeId(edgeId).put({ relationTypeId: String(payload.relationTypeId) })
}

export async function deleteKnowledgeGraphEdge(kgId: number, edgeId: string): Promise<void> {
  const client = getApiClient()
  await client.api.knowledgeGraph.byId(String(kgId)).edges.byEdgeId(edgeId).delete()
}

/** 画布/邻接子图响应 */
export interface KnowledgeGraphSubgraph {
  nodes: KnowledgeGraphNodeItem[]
  edges: KnowledgeGraphEdgeItem[]
  truncated: boolean
}

/** 画布有界子图查询（托管图按类型过滤，接入图按标签过滤） */
export async function getKnowledgeGraphCanvas(
  kgId: number,
  params: { entityTypeId?: number | null; label?: string | null; relationTypeId?: number | null; keyword?: string; limit?: number },
): Promise<KnowledgeGraphSubgraph> {
  const client = getApiClient()
  const res = await client.api.knowledgeGraph.byId(String(kgId)).canvas.post({
    entityTypeId: params.entityTypeId != null ? String(params.entityTypeId) : undefined,
    label: params.label ?? undefined,
    relationTypeId: params.relationTypeId != null ? String(params.relationTypeId) : undefined,
    keyword: params.keyword,
    limit: params.limit ?? 200,
  })
  return { nodes: (res?.nodes ?? []).map((x) => ({ ...x, properties: flattenNodeProperties(x.properties) }) as KnowledgeGraphNodeItem), edges: res?.edges ?? [], truncated: res?.truncated ?? false }
}

/** 节点一跳邻接展开（托管图按节点 id，接入图按 elementId） */
export async function getKnowledgeGraphNodeNeighbors(kgId: number, nodeId: string, limit = 100): Promise<KnowledgeGraphSubgraph> {
  const client = getApiClient()
  const res = await client.api.knowledgeGraph.byId(String(kgId)).nodes.byNodeId(nodeId).neighbors.get({ queryParameters: { limit } })
  return { nodes: (res?.nodes ?? []).map((x) => ({ ...x, properties: flattenNodeProperties(x.properties) }) as KnowledgeGraphNodeItem), edges: res?.edges ?? [], truncated: res?.truncated ?? false }
}

// ==================== AI 导入文件生成图谱 ====================

export interface KnowledgeGraphModelOption {
  id?: string | null
  name?: string | null
}

export interface KnowledgeGraphModelOptionsResult {
  /** 可用的对话模型列表（用于 AI 导入文件） */
  conversationModels?: KnowledgeGraphModelOption[] | null
  /** 可用的向量化模型列表（用于图谱向量检索配置） */
  embeddingModels?: KnowledgeGraphModelOption[] | null
}

/** 查询团队可用的 AI 模型选项（对话模型用于 AI 导入文件，向量化模型用于图检索配置） */
export async function getKnowledgeGraphModelOptions(teamId: number): Promise<KnowledgeGraphModelOptionsResult> {
  const client = getApiClient()
  const res = await client.api.knowledgeGraph.modelOptions.get({ queryParameters: { teamId: String(teamId) } })
  return { conversationModels: res?.conversationModels ?? [], embeddingModels: res?.embeddingModels ?? [] }
}

// ==================== 向量化配置（设置页） ====================

/** 配置知识图谱向量化模型与维度（仅托管图 Owner/Admin 可操作）；配置后图谱参与向量检索 */
export async function updateKnowledgeGraphEmbeddingConfig(
  kgId: number,
  payload: { embeddingModelId: string; embeddingDimensions: number },
): Promise<void> {
  const client = getApiClient()
  await client.api.knowledgeGraph.byId(String(kgId)).embeddingConfig.put({
    knowledgeGraphId: String(kgId),
    embeddingModelId: payload.embeddingModelId,
    embeddingDimensions: payload.embeddingDimensions,
  })
}

// ==================== 语义检索（向量 topK + 一跳扩展） ====================

/** 图谱语义检索命中项 */
export interface KnowledgeGraphSearchHit {
  kgId?: string | null
  nodeId?: string | null
  name?: string | null
  /** 实体类型名（类型未定义时为 null） */
  entityTypeName?: string | null
  description?: string | null
  /** 相似度得分（Cosine Similarity，来自向量库） */
  score?: number | null
  /** 一跳邻居 */
  neighbors?: KnowledgeGraphSearchNeighbor[] | null
}

/** 命中节点的一跳邻居摘要 */
export interface KnowledgeGraphSearchNeighbor {
  name?: string | null
  /** 关系类型名（类型未定义时为 null） */
  relationName?: string | null
  /** out（出边）/ in（入边） */
  direction?: string | null
  description?: string | null
}

/** 图谱语义检索响应 */
export interface KnowledgeGraphSearchResult {
  /** 命中列表（按得分降序） */
  hits: KnowledgeGraphSearchHit[]
  /** 命中数量 */
  count: number
  /** 命中节点文本片段（与 hits 同序，每项为「名称：描述」+一跳邻居行，可直接作 LLM 上下文） */
  contents: string[]
  /** 全部片段按命中顺序拼接的检索文本（超长截断） */
  text: string
  /** 模型不可用等运行期提示（未配置向量化在 Handler 即 409，不进本响应） */
  skippedHints: string[]
}

/** 图谱语义检索：向量 topK 召回 + 一跳关系扩展（GraphRAG local search 轻量版） */
export async function searchKnowledgeGraph(
  kgId: number,
  payload: { query: string; topK?: number; minScore?: number },
): Promise<KnowledgeGraphSearchResult> {
  const client = getApiClient()
  const res = await client.api.knowledgeGraph.byId(String(kgId)).search.post({
    knowledgeGraphId: String(kgId),
    query: payload.query,
    topK: payload.topK,
    minScore: payload.minScore,
  })
  const hits = (res?.hits ?? []).map((x) => ({ ...x })) as KnowledgeGraphSearchHit[]
  return {
    hits,
    count: hits.length,
    contents: res?.contents ?? [],
    text: res?.text ?? '',
    skippedHints: res?.skippedHints ?? [],
  }
}

export interface KnowledgeGraphImportResult {
  nodesCreated?: number | null
  edgesCreated?: number | null
  skippedNodes?: number | null
  skippedEdges?: number | null
  contentLength?: number | null
  truncated?: boolean | null
  message?: string | null
}

/** AI 导入文件生成图谱：objectKey 须为已直传到公开 chat 目录的文档文件 */
export async function importKnowledgeGraphFile(
  kgId: number,
  payload: { objectKey: string; fileName: string; aiModelId: string },
): Promise<KnowledgeGraphImportResult> {
  const client = getApiClient()
  const res = await client.api.knowledgeGraph.byId(String(kgId)).importFile.post({
    objectKey: payload.objectKey,
    fileName: payload.fileName,
    aiModelId: payload.aiModelId,
  })
  return {
    nodesCreated: res?.nodesCreated ?? 0,
    edgesCreated: res?.edgesCreated ?? 0,
    skippedNodes: res?.skippedNodes ?? 0,
    skippedEdges: res?.skippedEdges ?? 0,
    contentLength: res?.contentLength ?? 0,
    truncated: res?.truncated ?? false,
    message: res?.message ?? null,
  }
}
