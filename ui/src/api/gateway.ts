/**
 * 模型网关（可用模型）API 封装，基于 Kiota 生成的客户端。
 * 接入统一使用应用接入 key（moai-ac-，应用接入页维护），本模块不再提供密钥管理。
 */
import { getApiClient } from '@/api/kiota'

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

/** 查询团队可用的网关模型与额度（团队成员） */
export async function getTeamGatewayModels(teamId: number): Promise<TeamGatewayModelItem[]> {
  const res = await getApiClient().api.team.byId(String(teamId)).gateway.models.get()
  return res?.items ?? []
}
