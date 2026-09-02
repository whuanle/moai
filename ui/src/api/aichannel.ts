import { type Guid } from '@microsoft/kiota-abstractions'
import { getApiClient } from '@/api/kiota'
import { AIProtocolFamilyObject } from '@/api/client/models'
import type {
  AIChannelModelMeta,
  AIProtocolFamily,
  BatchDeleteAIModelCommand,
  BatchUpdateAIModelCommand,
  CreateAIChannelCommand,
  CreateAIModelCommand,
  ImportAIModelCommand,
  QueryAIChannelListCommandResponseItem,
  QueryAIModelListCommandResponseItem,
  SyncAIModelCommand,
  SyncAIModelCommandResponse,
  UpdateAIChannelCommand,
  UpdateAIModelCommand,
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

async function getModels(channelId?: string): Promise<AIModelItem[]> {
  const client = getApiClient()
  const res = await client.api.ai.model.get({
    queryParameters: channelId ? { channelId: channelId as Guid } : undefined,
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
}
