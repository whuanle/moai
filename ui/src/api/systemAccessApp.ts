import { getApiClient } from '@/api/kiota'

/** 系统应用接入状态：enabled=启用 disabled=停用 */
export type SystemAccessAppStatus = 'enabled' | 'disabled'

/** 系统资源功能范围代码（组织/用户/团队三组，读写两位一组） */
export const SYSTEM_ACCESS_APP_SCOPES = [
  'org_read',
  'org_write',
  'user_read',
  'user_write',
  'team_read',
  'team_write',
] as const

/** 系统应用接入列表项（与生成客户端 QuerySystemAccessAppsCommandResponseItem 字段对齐） */
export interface SystemAccessAppItem {
  id?: string | null
  name?: string | null
  description?: string | null
  keyPrefix?: string | null
  scopes?: string[] | null
  status?: SystemAccessAppStatus | null
  createTime?: string | null
  createUserName?: string | null
  updateTime?: string | null
  updateUserName?: string | null
}

/** 创建/更新系统应用接入载荷 */
export interface SystemAccessAppPayload {
  name: string
  description?: string
  scopes: string[]
  status: SystemAccessAppStatus
}

/** 创建结果：appid 与密钥原文仅此一次返回 */
export interface CreatedSystemAccessApp {
  id?: string | null
  key?: string | null
  secret?: string | null
  keyPrefix?: string | null
}

/** 查询系统应用接入列表（仅 root） */
export async function getSystemAccessApps(search?: string): Promise<SystemAccessAppItem[]> {
  const client = getApiClient()
  const res = await client.api.subsystem.systemApp.list.get({
    queryParameters: { search: search?.trim() || undefined },
  })
  return res?.items ?? []
}

/** 创建系统应用接入（仅 root），返回 appid 与密钥原文（仅此一次）；新建即启用 */
export async function createSystemAccessApp(payload: { name: string; description?: string; scopes: string[] }): Promise<CreatedSystemAccessApp> {
  const client = getApiClient()
  const res = await client.api.subsystem.systemApp.create.post({
    name: payload.name,
    description: payload.description,
    scopes: payload.scopes,
  })
  return { id: res?.id, key: res?.key, secret: res?.secret, keyPrefix: res?.keyPrefix }
}

/** 更新系统应用接入（仅 root）：名称/描述/范围/状态；appid 不可变更 */
export async function updateSystemAccessApp(id: string, payload: SystemAccessAppPayload): Promise<void> {
  const client = getApiClient()
  await client.api.subsystem.systemApp.byId(id).put({
    name: payload.name,
    description: payload.description,
    scopes: payload.scopes,
    status: payload.status,
  })
}

/** 重置系统应用接入密钥（仅 root），新密钥原文仅此一次返回，旧密钥立即失效 */
export async function resetSystemAccessAppSecret(id: string): Promise<string | null | undefined> {
  const client = getApiClient()
  const res = await client.api.subsystem.systemApp.byId(id).resetSecret.post()
  return res?.secret
}

/** 删除系统应用接入（仅 root，软删除），key 立即失效 */
export async function deleteSystemAccessApp(id: string): Promise<void> {
  const client = getApiClient()
  await client.api.subsystem.systemApp.byId(id).delete()
}
