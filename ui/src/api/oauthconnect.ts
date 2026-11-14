import { getApiClient } from '@/api/kiota'
import type { OAuthPrivider } from '@/api/client/models'

export interface OAuthConnectionItem {
  id?: string | null
  name?: string | null
  iconUrl?: string | null
  provider?: string | null
  key?: string | null
  wellKnown?: string | null
  agentId?: string | null
  authorizeUrl?: string | null
  /** 是否显示到登录页与个人中心绑定页（false=隐藏连接器，纯身份映射用） */
  isVisible?: boolean | null
  createTime?: string | null
  createUserName?: string | null
  updateTime?: string | null
  updateUserName?: string | null
}

export interface CreateOAuthConnectionPayload {
  name: string
  provider: OAuthPrivider
  key: string
  secret: string
  iconUrl: string
  wellKnown?: string
  agentId?: string
  /** 是否显示到登录页与个人中心绑定页；false=隐藏连接器（纯身份映射用） */
  isVisible?: boolean
}

export type UpdateOAuthConnectionPayload = Omit<CreateOAuthConnectionPayload, 'secret' | 'wellKnown'> & {
  secret?: string
  wellKnown?: string
}

export interface OAuthConnectionList {
  items: OAuthConnectionItem[]
  /** 回调地址（{WebUI}/oauth_login），需配置到提供方后台的重定向/回调域名白名单 */
  redirectUri: string
}

export async function getOAuthConnections(): Promise<OAuthConnectionList> {
  const client = getApiClient()
  const res = await client.api.oauthconnect.connections.get()
  return { items: res?.items ?? [], redirectUri: res?.redirectUri ?? '' }
}

export async function createOAuthConnection(payload: CreateOAuthConnectionPayload): Promise<void> {
  const client = getApiClient()
  await client.api.oauthconnect.connections.post(payload)
}

export async function updateOAuthConnection(id: string, payload: UpdateOAuthConnectionPayload): Promise<void> {
  const client = getApiClient()
  await client.api.oauthconnect.connections.byId(id).put({ ...payload, oAuthConnectionId: id })
}

export async function deleteOAuthConnection(id: string): Promise<void> {
  const client = getApiClient()
  await client.api.oauthconnect.connections.byId(id).delete()
}
