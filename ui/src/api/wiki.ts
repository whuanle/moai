import { getApiClient } from '@/api/kiota'

export interface WikiItem {
  /** 后端 long 序列化为字符串 */
  wikiId?: string | number | null
  teamId?: string | number | null
  name?: string | null
  description?: string | null
  createTime?: string | null
}

export interface WikisResult {
  teamId?: string | number | null
  /** 0=Owner 1=Admin 2=Member */
  myRole?: number | null
  items?: WikiItem[] | null
}

export async function getWikis(teamId: number): Promise<WikisResult> {
  const client = getApiClient()
  const res = await client.api.wiki.list.get({
    queryParameters: { teamId: String(teamId) },
  })
  return { teamId: res?.teamId, myRole: res?.myRole, items: res?.items ?? [] }
}

export async function getWikiDetail(wikiId: number) {
  const client = getApiClient()
  return client.api.wiki.byId(String(wikiId)).get()
}

export async function createWiki(payload: { teamId: number; name: string; description?: string }): Promise<number> {
  const client = getApiClient()
  const res = await client.api.wiki.post({ teamId: String(payload.teamId), name: payload.name, description: payload.description })
  return Number(res?.value ?? 0)
}

export async function updateWiki(wikiId: number, payload: { name: string; description?: string }): Promise<void> {
  const client = getApiClient()
  await client.api.wiki.byId(String(wikiId)).put({ name: payload.name, description: payload.description })
}

export async function deleteWiki(wikiId: number): Promise<void> {
  const client = getApiClient()
  await client.api.wiki.byId(String(wikiId)).delete()
}
