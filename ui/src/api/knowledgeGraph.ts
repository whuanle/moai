import { getApiClient } from '@/api/kiota'

export interface KnowledgeGraphItem {
  kgId?: string | number | null
  teamId?: string | number | null
  name?: string | null
  description?: string | null
  templateKey?: string | null
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
  myRole?: number | null
  enabled?: boolean | null
  createTime?: string | null
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
}

export interface KnowledgeGraphRelationTypeItem {
  relationTypeId?: string | number | null
  name?: string | null
  color?: string | null
  description?: string | null
  sourceTypeId?: string | number | null
  targetTypeId?: string | number | null
}

export interface KnowledgeGraphSchema {
  entityTypes?: KnowledgeGraphEntityTypeItem[] | null
  relationTypes?: KnowledgeGraphRelationTypeItem[] | null
}

export interface KnowledgeGraphNodeItem {
  nodeId?: string | null
  entityTypeId?: string | number | null
  name?: string | null
  description?: string | null
}

export interface KnowledgeGraphEdgeItem {
  edgeId?: string | null
  relationTypeId?: string | number | null
  sourceNodeId?: string | null
  targetNodeId?: string | null
}

export interface PagedResult<T> { items?: T[] | null; total?: string | number | null }

export async function getKnowledgeGraphs(teamId: number): Promise<KnowledgeGraphListResult> {
  const client = getApiClient()
  const res = await client.api.knowledgeGraph.list.get({ queryParameters: { teamId: String(teamId) } })
  return { teamId: res?.teamId, myRole: res?.myRole, enabled: res?.enabled ?? false, items: res?.items ?? [] }
}

export async function createKnowledgeGraph(payload: { teamId: number; name: string; description?: string; templateKey?: string | null }): Promise<number> {
  const client = getApiClient()
  const res = await client.api.knowledgeGraph.post({
    teamId: String(payload.teamId),
    name: payload.name,
    description: payload.description,
    templateKey: payload.templateKey ?? undefined,
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
  return res ?? {}
}

export async function updateKnowledgeGraph(kgId: number, payload: { name: string; description?: string }): Promise<void> {
  const client = getApiClient()
  await client.api.knowledgeGraph.byId(String(kgId)).put({ name: payload.name, description: payload.description })
}

export async function deleteKnowledgeGraph(kgId: number): Promise<void> {
  const client = getApiClient()
  await client.api.knowledgeGraph.byId(String(kgId)).delete()
}

export async function getKnowledgeGraphSchema(kgId: number): Promise<KnowledgeGraphSchema> {
  const client = getApiClient()
  const res = await client.api.knowledgeGraph.byId(String(kgId)).schema.get()
  return { entityTypes: res?.entityTypes ?? [], relationTypes: res?.relationTypes ?? [] }
}

export async function createEntityType(kgId: number, payload: { name: string; color?: string; description?: string }): Promise<number> {
  const client = getApiClient()
  const res = await client.api.knowledgeGraph.byId(String(kgId)).entityTypes.post(payload)
  return Number(res?.value ?? 0)
}

export async function updateEntityType(kgId: number, entityTypeId: number, payload: { name: string; color?: string; description?: string }): Promise<void> {
  const client = getApiClient()
  await client.api.knowledgeGraph.byId(String(kgId)).entityTypes.byTypeId(String(entityTypeId)).put(payload)
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
  return { items: res?.items ?? [], total: Number(res?.total ?? 0) }
}

export async function createKnowledgeGraphNode(kgId: number, payload: { entityTypeId: number; name: string; description?: string }): Promise<string> {
  const client = getApiClient()
  const res = await client.api.knowledgeGraph.byId(String(kgId)).nodes.post({ entityTypeId: String(payload.entityTypeId), name: payload.name, description: payload.description })
  return String(res?.value ?? '')
}

export async function updateKnowledgeGraphNode(kgId: number, nodeId: string, payload: { entityTypeId: number; name: string; description?: string }): Promise<void> {
  const client = getApiClient()
  await client.api.knowledgeGraph.byId(String(kgId)).nodes.byNodeId(nodeId).put({ entityTypeId: String(payload.entityTypeId), name: payload.name, description: payload.description })
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
