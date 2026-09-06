/**
 * 模型网关（团队 API Key + 可用模型）API 封装，基于 Kiota 生成的客户端。
 */
import { type Guid } from '@microsoft/kiota-abstractions'
import { getApiClient } from '@/api/kiota'

/** 团队网关 API Key 项 */
export interface TeamApiKeyItem {
  id?: string | null
  name?: string | null
  keyPrefix?: string | null
  isDisable?: boolean | null
  isExpired?: boolean | null
  expireTime?: string | null
  lastUsedTime?: string | null
  createTime?: string | null
}

/** 创建密钥响应：secret 仅此一次返回 */
export interface CreateTeamApiKeyResult {
  apiKeyId?: string | null
  secret?: string | null
  keyPrefix?: string | null
}

/** 网关模型额度状态 */
export interface TeamGatewayModelQuota {
  periodValue?: number | null
  periodUnit?: number | null
  limitValue?: number | string | null
  usedTokens?: number | string | null
  periodEnd?: string | null
  expirationTime?: string | null
}

/** 团队可用网关模型项 */
export interface TeamGatewayModelItem {
  aiModelId?: string | null
  name?: string | null
  modelId?: string | null
  channelName?: string | null
  providerKey?: string | null
  quota?: TeamGatewayModelQuota | null
  totalUsedTokens?: number | string | null
}

/** 查询团队 API Key 列表（团队管理员） */
export async function getTeamApiKeys(teamId: number): Promise<TeamApiKeyItem[]> {
  const res = await getApiClient().api.team.byId(String(teamId)).gateway.keys.get()
  return res?.items ?? []
}

/** 创建团队 API Key，secret 仅创建时返回一次 */
export async function createTeamApiKey(teamId: number, payload: { name: string; expireTime?: string }): Promise<CreateTeamApiKeyResult> {
  const res = await getApiClient().api.team.byId(String(teamId)).gateway.keys.post({
    name: payload.name,
    expireTime: payload.expireTime,
  })
  return {
    apiKeyId: res?.apiKeyId ?? null,
    secret: res?.secret ?? null,
    keyPrefix: res?.keyPrefix ?? null,
  }
}

/** 修改团队 API Key（名称、启用/禁用） */
export async function updateTeamApiKey(teamId: number, keyId: string, payload: { name?: string; isDisable: boolean }): Promise<void> {
  await getApiClient().api.team.byId(String(teamId)).gateway.keys.byKeyId(keyId as Guid).put({
    name: payload.name,
    isDisable: payload.isDisable,
  })
}

/** 删除团队 API Key */
export async function deleteTeamApiKey(teamId: number, keyId: string): Promise<void> {
  await getApiClient().api.team.byId(String(teamId)).gateway.keys.byKeyId(keyId as Guid).delete()
}

/** 查询团队可用的网关模型与额度（团队成员） */
export async function getTeamGatewayModels(teamId: number): Promise<TeamGatewayModelItem[]> {
  const res = await getApiClient().api.team.byId(String(teamId)).gateway.models.get()
  return res?.items ?? []
}
