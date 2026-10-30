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
  QueryAIChannelAuthorizationCommandResponse,
  QueryAIModelAuthorizationCommandResponse,
  QueryAIModelAuthorizationCommandResponseItem,
  QueryAIModelListCommandResponseItem,
  SyncAIModelCatalogCommandResponse,
  SyncAIModelCommand,
  SyncAIModelCommandResponse,
  SyncAIModelCandidateItem,
  UpdateAIChannelAuthorizationCommand,
  UpdateAIChannelCommand,
  UpdateAIModelAuthorizationCommand,
  UpdateAIModelCommand,
  UpdateAIModelVisibilityCommand,
} from '@/api/client/models'

/** AI 渠道项（Kiota 生成类型别名）. */
export type AIChannelItem = QueryAIChannelListCommandResponseItem
/** AI 模型项（Kiota 生成类型别名）. */
export type AIModelItem = QueryAIModelListCommandResponseItem
/** 模型元数据（Kiota 生成类型别名）. */
export type AIModelMeta = AIChannelModelMeta
/** 同步候选模型（Kiota 生成类型别名）. */
export type SyncModelCandidate = SyncAIModelCandidateItem

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

function toChannelCommand(payload: CreateAIChannelPayload): CreateAIChannelCommand {
  return {
    providerKey: payload.providerKey,
    name: payload.name,
    protocolFamily: payload.protocolFamily as AIProtocolFamily,
    baseUrl: payload.baseUrl,
    apiKey: payload.apiKey,
    enabled: payload.enabled,
    description: payload.description,
  } as CreateAIChannelCommand
}

function toModelCommand(payload: CreateAIModelPayload): CreateAIModelCommand {
  return {
    channelId: payload.channelId as Guid,
    meta: payload.meta,
    enabled: payload.enabled,
  } as CreateAIModelCommand
}

async function getChannels(): Promise<AIChannelItem[]> {
  const client = getApiClient()
  const res = await client.api.ai.channel.get()
  return res?.items ?? []
}

async function createChannel(payload: CreateAIChannelPayload): Promise<void> {
  const client = getApiClient()
  await client.api.ai.channel.post(toChannelCommand(payload))
}

async function updateChannel(id: string, payload: CreateAIChannelPayload): Promise<void> {
  const client = getApiClient()
  await client.api.ai.channel.byId(id as Guid).put({
    channelId: id as Guid,
    ...toChannelCommand(payload),
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
  await client.api.ai.model.post(toModelCommand(payload))
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
  return res ?? { total: 0, items: [] }
}

/** 模型目录在线同步结果（Kiota 生成类型别名）. */
export type SyncCatalogResult = SyncAIModelCatalogCommandResponse

/** 从 models.opencode.ai 下载最新模型目录保存到服务端 configs 目录，保存后立即生效（仅管理员）. */
async function syncCatalog(): Promise<SyncCatalogResult> {
  const client = getApiClient()
  const res = await client.api.ai.model.catalog.sync.post()
  return res ?? { providerCount: 0, modelCount: 0 }
}

/** 模型授权查询响应（Kiota 生成类型别名）. */
export type ModelAuthorization = QueryAIModelAuthorizationCommandResponse
/** 授权团队项（Kiota 生成类型别名）. */
export type ModelAuthorizationItem = QueryAIModelAuthorizationCommandResponseItem
/** 渠道级授权查询响应（Kiota 生成类型别名）. */
export type ChannelAuthorization = QueryAIChannelAuthorizationCommandResponse

async function getModelAuthorization(modelId: string): Promise<ModelAuthorization | null> {
  const client = getApiClient()
  const res = await client.api.ai.model.byId(modelId as Guid).authorization.get()
  return res ?? null
}

/** 查询渠道级授权视图：模型数量统计 + 各私有模型授权团队的并集. */
async function getChannelAuthorization(channelId: string): Promise<ChannelAuthorization | null> {
  const client = getApiClient()
  const res = await client.api.ai.channel.byId(channelId as Guid).authorization.get()
  return res ?? null
}

/** 渠道级批量授权（增量语义）：grant 补齐缺失授权、revoke 移除已有授权，均幂等，公开模型不受影响. */
async function updateChannelAuthorization(
  channelId: string,
  payload: { grantTeamIds: number[]; revokeTeamIds: number[] },
): Promise<void> {
  const client = getApiClient()
  await client.api.ai.channel.byId(channelId as Guid).authorization.put({
    channelId: channelId as Guid,
    grantTeamIds: payload.grantTeamIds,
    revokeTeamIds: payload.revokeTeamIds,
  } as UpdateAIChannelAuthorizationCommand)
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

// ---------- 团队自有模型渠道（/team/{teamId}/ai/*，团队 Owner/Admin 可管理） ----------

async function getTeamChannels(teamId: number): Promise<AIChannelItem[]> {
  const client = getApiClient()
  const res = await client.api.team.byId(String(teamId)).ai.channel.get()
  return res?.items ?? []
}

async function createTeamChannel(teamId: number, payload: CreateAIChannelPayload): Promise<void> {
  const client = getApiClient()
  await client.api.team.byId(String(teamId)).ai.channel.post(toChannelCommand(payload))
}

async function updateTeamChannel(teamId: number, id: string, payload: CreateAIChannelPayload): Promise<void> {
  const client = getApiClient()
  await client.api.team.byId(String(teamId)).ai.channel.byId(id as Guid).put({
    channelId: id as Guid,
    ...toChannelCommand(payload),
  } as UpdateAIChannelCommand)
}

async function deleteTeamChannel(teamId: number, id: string): Promise<void> {
  const client = getApiClient()
  await client.api.team.byId(String(teamId)).ai.channel.byId(id as Guid).delete()
}

async function getTeamModels(teamId: number, channelId?: string): Promise<AIModelItem[]> {
  const client = getApiClient()
  const res = await client.api.team.byId(String(teamId)).ai.model.get({
    queryParameters: channelId ? { channelId: channelId as Guid } : undefined,
  })
  return res?.items ?? []
}

async function createTeamModel(teamId: number, payload: CreateAIModelPayload): Promise<void> {
  const client = getApiClient()
  await client.api.team.byId(String(teamId)).ai.model.post(toModelCommand(payload))
}

async function updateTeamModel(teamId: number, id: string, payload: CreateAIModelPayload): Promise<void> {
  const client = getApiClient()
  await client.api.team.byId(String(teamId)).ai.model.byId(id as Guid).put({
    modelId: id as Guid,
    meta: payload.meta,
    enabled: payload.enabled,
  } as UpdateAIModelCommand)
}

async function deleteTeamModel(teamId: number, id: string): Promise<void> {
  const client = getApiClient()
  await client.api.team.byId(String(teamId)).ai.model.byId(id as Guid).delete()
}

async function importTeamModels(teamId: number, payload: ImportAIModelPayload): Promise<void> {
  const client = getApiClient()
  await client.api.team.byId(String(teamId)).ai.model.importEscaped.post({
    channelId: payload.channelId as Guid,
    items: payload.items,
  } as ImportAIModelCommand)
}

async function syncTeamModels(teamId: number, channelId: string): Promise<SyncAIModelCommandResponse> {
  const client = getApiClient()
  const res = await client.api.team.byId(String(teamId)).ai.model.sync.post({ channelId: channelId as Guid } as SyncAIModelCommand)
  return res ?? { total: 0, items: [] }
}

async function batchUpdateTeamModel(teamId: number, modelIds: string[], enabled: boolean): Promise<void> {
  const client = getApiClient()
  await client.api.team.byId(String(teamId)).ai.model.batch.post({
    modelIds: modelIds.map((id) => id as Guid),
    enabled,
  } as BatchUpdateAIModelCommand)
}

async function batchDeleteTeamModel(teamId: number, modelIds: string[]): Promise<void> {
  const client = getApiClient()
  await client.api.team.byId(String(teamId)).ai.model.batchDelete.post({
    modelIds: modelIds.map((id) => id as Guid),
  } as BatchDeleteAIModelCommand)
}

// ---------- 团队可用模型（/team/{teamId}/ai/model/available，团队成员可访问） ----------

/** 团队可用模型项 */
export interface TeamAvailableModelItem {
  aiModelId?: string | null
  name?: string | null
  modelId?: string | null
  channelName?: string | null
  providerKey?: string | null
  /** 是否支持视觉（图片输入） */
  supportsVision?: boolean | null
  /** 是否来自团队自有渠道（false=系统公开或授权模型） */
  isTeamChannel?: boolean | null
}

/** 查询团队可用模型列表（团队自有渠道 ∪ 公开模型 ∪ 已授权模型，同名去重后团队自有优先），供应用配置与流程节点模型选择器使用 */
export async function getTeamAvailableModels(teamId: number): Promise<TeamAvailableModelItem[]> {
  const client = getApiClient()
  const res = await client.api.team.byId(String(teamId)).ai.model.available.get()
  return res?.items ?? []
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
  syncCatalog,
  batchUpdateModel,
  batchDeleteModel,
  getModelAuthorization,
  getChannelAuthorization,
  updateChannelAuthorization,
  updateModelAuthorization,
  updateModelVisibility,
  getTeamChannels,
  createTeamChannel,
  updateTeamChannel,
  deleteTeamChannel,
  getTeamModels,
  createTeamModel,
  updateTeamModel,
  deleteTeamModel,
  importTeamModels,
  syncTeamModels,
  batchUpdateTeamModel,
  batchDeleteTeamModel,
  getTeamAvailableModels,
}
