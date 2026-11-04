import { getApiClient } from '@/api/kiota'

export interface AccessAppItem {
  /** 后端 Guid 序列化为字符串 */
  accessAppId?: string | null
  name?: string | null
  description?: string | null
  /** 接入 appid（明文，可再次查看；前端默认掩码、点击展开） */
  key?: string | null
  createTime?: string | null
  /** 最近使用时间（通过模型网关调用时刷新） */
  lastUsedTime?: string | null
}

export interface AccessAppsResult {
  teamId?: string | number | null
  /** 0=Member 1=Admin 2=Owner */
  myRole?: number | null
  items?: AccessAppItem[] | null
}

export interface CreatedAccessApp {
  accessAppId?: string | null
  /** appid 原文，仅创建时返回一次 */
  key?: string | null
  /** 接入密钥原文，仅创建时返回返回一次 */
  secret?: string | null
  keyPrefix?: string | null
}

/** 查询团队下的应用接入列表（仅团队 Admin+）；moai-ac- 可全量访问 /api/teamres */
export async function getAccessApps(teamId: number): Promise<AccessAppsResult> {
  const client = getApiClient()
  const res = await client.api.accessApp.list.get({
    queryParameters: { teamId: String(teamId) },
  })
  return { teamId: res?.teamId, myRole: res?.myRole, items: res?.items ?? [] }
}

/** 创建应用接入（moai-ac-，全量访问 /api/teamres），返回 appid 与密钥原文（仅此一次） */
export async function createAccessApp(payload: {
  teamId: number
  name: string
  description?: string
}): Promise<CreatedAccessApp> {
  const client = getApiClient()
  const res = await client.api.accessApp.post({
    teamId: String(payload.teamId),
    name: payload.name,
    description: payload.description,
  })
  return { accessAppId: res?.accessAppId, key: res?.key, secret: res?.secret, keyPrefix: res?.keyPrefix }
}

/** 更新应用接入（名称/描述；appid 不可改） */
export async function updateAccessApp(
  accessAppId: string,
  payload: { name: string; description?: string },
): Promise<void> {
  const client = getApiClient()
  await client.api.accessApp.byId(accessAppId).put({
    name: payload.name,
    description: payload.description,
  })
}

/** 重置应用接入密钥，新密钥原文仅此一次返回，旧密钥立即失效 */
export async function resetAccessAppSecret(accessAppId: string): Promise<string | null | undefined> {
  const client = getApiClient()
  const res = await client.api.accessApp.byId(accessAppId).resetSecret.post()
  return res?.secret
}

/** 删除应用接入（软删除） */
export async function deleteAccessApp(accessAppId: string): Promise<void> {
  const client = getApiClient()
  await client.api.accessApp.byId(accessAppId).delete()
}

/** 应用对外功能（ACP/A2A）与该应用的团队资源 key（一个应用一把，ACP/A2A 共用） */
export interface AppFeature {
  isAcpEnabled?: boolean | null
  isA2aEnabled?: boolean | null
  teamResourceKeyId?: string | null
  key?: string | null
  keyPrefix?: string | null
  status?: 'enabled' | 'disabled' | null
}

/** 查询应用对外功能开关与团队资源 key（仅团队 Admin+） */
export async function getAppFeature(appId: string): Promise<AppFeature> {
  const client = getApiClient()
  const res = await client.api.app.byId(appId).feature.get()
  return {
    isAcpEnabled: res?.isAcpEnabled,
    isA2aEnabled: res?.isA2aEnabled,
    teamResourceKeyId: res?.teamResourceKeyId,
    key: res?.key,
    keyPrefix: res?.keyPrefix,
    status: res?.status ?? null,
  }
}

/** 更新应用对外功能开关（ACP/A2A）；开启时后端自动生成/复用该应用的团队资源 key */
export async function updateAppFeature(appId: string, payload: { isAcpEnabled: boolean; isA2aEnabled: boolean }): Promise<AppFeature> {
  const client = getApiClient()
  const res = await client.api.app.byId(appId).feature.put({
    isAcpEnabled: payload.isAcpEnabled,
    isA2aEnabled: payload.isA2aEnabled,
  })
  return {
    isAcpEnabled: res?.isAcpEnabled,
    isA2aEnabled: res?.isA2aEnabled,
    teamResourceKeyId: res?.teamResourceKeyId,
    key: res?.key,
    keyPrefix: res?.keyPrefix,
    status: res?.status ?? null,
  }
}
