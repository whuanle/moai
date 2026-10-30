import { getApiClient } from '@/api/kiota'

/** 应用接入可选功能范围（知识库/知识图谱维度 + 应用对话 + 两域 MCP + 应用 ACP/A2A） */
export type AccessAppScope =
  | 'model'
  | 'wiki_read'
  | 'wiki_write'
  | 'wiki_mcp'
  | 'kg_read'
  | 'kg_write'
  | 'kg_mcp'
  | 'app_chat'
  | 'app_acp'
  | 'app_a2a'

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
  scopes?: string[] | null
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
  /** 接入密钥原文，仅创建时返回一次（换取 token 时携带） */
  secret?: string | null
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

/** 创建应用接入，返回 appid 与密钥原文（仅此一次） */
export async function createAccessApp(payload: {
  teamId: number
  name: string
  description?: string
  /** 不传=默认读写全量；空数组=纯对话接入 */
  scopes?: string[]
}): Promise<CreatedAccessApp> {
  const client = getApiClient()
  const res = await client.api.accessApp.post({
    teamId: String(payload.teamId),
    name: payload.name,
    description: payload.description,
    scopes: payload.scopes,
  })
  return { accessAppId: res?.accessAppId, key: res?.key, secret: res?.secret, keyPrefix: res?.keyPrefix }
}

/** 更新应用接入（名称/描述；appid 不可改） */
export async function updateAccessApp(
  accessAppId: string,
  payload: { name: string; description?: string; scopes?: string[] },
): Promise<void> {
  const client = getApiClient()
  await client.api.accessApp.byId(accessAppId).put({
    name: payload.name,
    description: payload.description,
    scopes: payload.scopes,
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
