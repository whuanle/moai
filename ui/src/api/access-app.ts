import { type Guid } from '@microsoft/kiota-abstractions'
import { getApiClient } from '@/api/kiota'

export interface AccessAppItem {
  /** 后端 Guid 序列化为字符串 */
  accessAppId?: string | null
  name?: string | null
  description?: string | null
  /** 授权可访问的外部应用 id（uuid 字符串） */
  appIds?: string[] | null
  /** 接入 key（明文，可再次查看；前端默认掩码、点击展开） */
  key?: string | null
  createTime?: string | null
}

export interface AccessAppsResult {
  teamId?: string | number | null
  /** 0=Member 1=Admin 2=Owner */
  myRole?: number | null
  items?: AccessAppItem[] | null
}

export interface CreatedAccessApp {
  accessAppId?: string | null
  /** key 原文，仅创建时返回一次 */
  key?: string | null
  keyPrefix?: string | null
}

/** 查询团队下的应用接入列表（仅团队 Admin+） */
export async function getAccessApps(teamId: number): Promise<AccessAppsResult> {
  const client = getApiClient()
  const res = await client.api.accessApp.list.get({
    queryParameters: { teamId: String(teamId) },
  })
  return { teamId: res?.teamId, myRole: res?.myRole, items: res?.items ?? [] }
}

/** 创建应用接入，返回 key 原文（仅此一次） */
export async function createAccessApp(payload: {
  teamId: number
  name: string
  description?: string
  appIds: string[]
}): Promise<CreatedAccessApp> {
  const client = getApiClient()
  const res = await client.api.accessApp.post({
    teamId: String(payload.teamId),
    name: payload.name,
    description: payload.description,
    appIds: payload.appIds as Guid[],
  })
  return { accessAppId: res?.accessAppId, key: res?.key, keyPrefix: res?.keyPrefix }
}

/** 更新应用接入（名称/描述/授权外部应用；key 不可改） */
export async function updateAccessApp(
  accessAppId: string,
  payload: { name: string; description?: string; appIds: string[] },
): Promise<void> {
  const client = getApiClient()
  await client.api.accessApp.byId(accessAppId).put({
    name: payload.name,
    description: payload.description,
    appIds: payload.appIds as Guid[],
  })
}

/** 删除应用接入（软删除） */
export async function deleteAccessApp(accessAppId: string): Promise<void> {
  const client = getApiClient()
  await client.api.accessApp.byId(accessAppId).delete()
}
