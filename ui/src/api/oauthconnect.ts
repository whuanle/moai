import { getApiClient } from '@/api/kiota'
import type { OAuthPrivider } from '@/api/client/models'

export interface OAuthConnectionItem {
  id?: string | null
  name?: string | null
  iconUrl?: string | null
  provider?: string | null
  key?: string | null
  wellKnown?: string | null
  authorizeUrl?: string | null
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
}

export type UpdateOAuthConnectionPayload = Omit<CreateOAuthConnectionPayload, 'secret' | 'wellKnown'> & {
  secret?: string
  wellKnown?: string
}

export async function getOAuthConnections(): Promise<OAuthConnectionItem[]> {
  const client = getApiClient()
  const res = await client.api.oauthconnect.connections.get()
  return res?.items ?? []
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
