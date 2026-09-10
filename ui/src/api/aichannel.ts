import { type Guid } from '@microsoft/kiota-abstractions'
import { getApiClient } from '@/api/kiota'
import { AIProtocolFamilyObject } from '@/api/client/models'
import type {
  AIChannelModelMeta,
  AIModelQuotaInfo,
  AIProtocolFamily,
  BatchDeleteAIModelCommand,
  BatchUpdateAIModelCommand,
  CreateAIChannelCommand,
  CreateAIModelCommand,
  ImportAIModelCommand,
  QueryAIChannelListCommandResponseItem,
  QueryAIModelAuthorizationCommandResponse,
  QueryAIModelAuthorizationCommandResponseItem,
  QueryAIModelListCommandResponseItem,
  SyncAIModelCommand,
  SyncAIModelCommandResponse,
  UpdateAIChannelCommand,
  UpdateAIModelAuthorizationCommand,
  UpdateAIModelCommand,
  UpdateAIModelQuotaCommand,
  UpdateAIModelVisibilityCommand,
} from '@/api/client/models'

/** AI 渠道项（Kiota 生成类型别名）. */
export type AIChannelItem = QueryAIChannelListCommandResponseItem
/** AI 模型项（Kiota 生成类型别名）. */
export type AIModelItem = QueryAIModelListCommandResponseItem
/** 模型元数据（Kiota 生成类型别名）. */
export type AIModelMeta = AIChannelModelMeta

export interface CreateAIChannelPayload {
  providerKey: string
  name: string
  protocolFamily: string
  baseUrl?: string | null
  apiKey?: string | null
  enabled?: boolean
  description?: string | null
}

export interface CreateAIModelPayload {
  channelId: string
  meta: AIModelMeta
  enabled?: boolean
}

export interface ImportAIModelPayload {
  channelId: string
  items: AIModelMeta[]
}

async function getChannels(): Promise<AIChannelItem[]> {
  const client = getApiClient()
  const res = await client.api.ai.channel.get()
  return res?.items ?? []
}

async function createChannel(payload: CreateAIChannelPayload): Promise<void> {
  const client = getApiClient()
  await client.api.ai.channel.post({
    providerKey: payload.providerKey,
    name: payload.name,
    protocolFamily: payload.protocolFamily as AIProtocolFamily,
    baseUrl: payload.baseUrl,
    apiKey: payload.apiKey,
    enabled: payload.enabled,
    description: payload.description,
  } as CreateAIChannelCommand)
}

async function updateChannel(id: string, payload: CreateAIChannelPayload): Promise<void> {
  const client = getApiClient()
  await client.api.ai.channel.byId(id as Guid).put({
    channelId: id as Guid,
    providerKey: payload.providerKey,
    name: payload.name,
    protocolFamily: payload.protocolFamily as AIProtocolFamily,
    baseUrl: payload.baseUrl,
    apiKey: payload.apiKey,
    enabled: payload.enabled,
    description: payload.description,
  } as UpdateAIChannelCommand)
}

async function deleteChannel(id: string): Promise<void> {
  const client = getApiClient()
  await client.api.ai.channel.byId(id as Guid).delete()
}

async function getModels(channelId?: string, teamId?: number): Promise<AIModelItem[]> {
  const client = getApiClient()
  const res = await client.api.ai.model.get({
    queryParameters: channelId || teamId !== undefined ? { channelId: channelId as Guid | undefined, teamId } : undefined,
  })
  return res?.items ?? []
}

async function createModel(payload: CreateAIModelPayload): Promise<void> {
  const client = getApiClient()
  await client.api.ai.model.post({
    channelId: payload.channelId as Guid,
    meta: payload.meta,
    enabled: payload.enabled,
  } as CreateAIModelCommand)
}

async function updateModel(id: string, payload: CreateAIModelPayload): Promise<void> {
  const client = getApiClient()
  await client.api.ai.model.byId(id as Guid).put({
    modelId: id as Guid,
    meta: payload.meta,
    enabled: payload.enabled,
  } as UpdateAIModelCommand)
}

async function deleteModel(id: string): Promise<void> {
  const client = getApiClient()
  await client.api.ai.model.byId(id as Guid).delete()
}

async function importModels(payload: ImportAIModelPayload): Promise<void> {
  const client = getApiClient()
  await client.api.ai.model.importEscaped.post({
    channelId: payload.channelId as Guid,
    items: payload.items,
  } as ImportAIModelCommand)
}

async function syncModel(channelId: string): Promise<SyncAIModelCommandResponse> {
  const client = getApiClient()
  const res = await client.api.ai.model.sync.post({ channelId: channelId as Guid } as SyncAIModelCommand)
  return res ?? { total: 0, added: 0, skipped: 0 }
}

/** 模型授权与额度查询响应（Kiota 生成类型别名）. */
export type ModelAuthorization = QueryAIModelAuthorizationCommandResponse
/** 授权团队项（Kiota 生成类型别名）. */
export type ModelAuthorizationItem = QueryAIModelAuthorizationCommandResponseItem
/** 模型额度信息（Kiota 生成类型别名）. */
export type ModelQuotaInfo = AIModelQuotaInfo

export interface UpdateModelQuotaPayload {
  /** 重置周期单位：0=不重置 1=小时 2=天 3=周 4=月. */
  periodUnit: number
  /** 周期长度，periodUnit=0 时无效. */
  periodValue: number
  /** 每个重置周期内的 tokens 上限. */
  limitValue: number
}

async function getModelAuthorization(modelId: string): Promise<ModelAuthorization | null> {
  const client = getApiClient()
  const res = await client.api.ai.model.byId(modelId as Guid).authorization.get()
  return res ?? null
}

/** 全量替换私有模型的授权团队集合. */
async function updateModelAuthorization(modelId: string, teamIds: number[]): Promise<void> {
  const client = getApiClient()
  await client.api.ai.model.byId(modelId as Guid).authorization.put({
    modelId: modelId as Guid,
    teamIds,
  } as UpdateAIModelAuthorizationCommand)
}

/** 切换模型公私有可见性. */
async function updateModelVisibility(modelId: string, isPublic: boolean): Promise<void> {
  const client = getApiClient()
  await client.api.ai.model.byId(modelId as Guid).visibility.put({
    modelId: modelId as Guid,
    isPublic,
  } as UpdateAIModelVisibilityCommand)
}

/** 设置额度：公开模型传 teamId=0，私有模型传已授权团队 id. */
async function updateModelQuota(modelId: string, teamId: number, payload: UpdateModelQuotaPayload): Promise<void> {
  const client = getApiClient()
  await client.api.ai.model.byId(modelId as Guid).quota.byTeamId(teamId).put({
    modelId: modelId as Guid,
    teamId,
    periodUnit: payload.periodUnit,
    periodValue: payload.periodValue,
    // 后端 long 在 OpenAPI 中映射为 string
    limitValue: String(payload.limitValue),
  } as UpdateAIModelQuotaCommand)
}

async function deleteModelQuota(modelId: string, teamId: number): Promise<void> {
  const client = getApiClient()
  await client.api.ai.model.byId(modelId as Guid).quota.byTeamId(teamId).delete()
}

async function batchUpdateModel(modelIds: string[], enabled: boolean): Promise<void> {
  const client = getApiClient()
  await client.api.ai.model.batch.post({
    modelIds: modelIds.map((id) => id as Guid),
    enabled,
  } as BatchUpdateAIModelCommand)
}

async function batchDeleteModel(modelIds: string[]): Promise<void> {
  const client = getApiClient()
  await client.api.ai.model.batchDelete.post({
    modelIds: modelIds.map((id) => id as Guid),
  } as BatchDeleteAIModelCommand)
}

/** 各协议取值（协议族+风格组合，与后端 OpenAPI 枚举一致，来自 Kiota 生成结果）. */
export const AI_PROTOCOLS: AIProtocolFamily[] = Object.values(AIProtocolFamilyObject)
/** 模型类型（后端推导，仅用于展示）. */
export const AI_MODEL_KINDS = ['conversation', 'embedding', 'image-generation', 'video-generation', 'transcription'] as const

export const aichannelApi = {
  getChannels,
  createChannel,
  updateChannel,
  deleteChannel,
  getModels,
  createModel,
  updateModel,
  deleteModel,
  importModels,
  syncModel,
  batchUpdateModel,
  batchDeleteModel,
  getModelAuthorization,
  updateModelAuthorization,
  updateModelVisibility,
  updateModelQuota,
  deleteModelQuota,
}
