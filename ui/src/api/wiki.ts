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

export interface WikiDocumentItem {
  /** 后端 long 序列化为字符串 */
  documentId?: string | number | null
  wikiId?: string | number | null
  title?: string | null
  createTime?: string | null
  updateTime?: string | null
}

export interface WikiDocumentsResult {
  wikiId?: string | number | null
  /** 0=Owner 1=Admin 2=Member */
  myRole?: number | null
  items?: WikiDocumentItem[] | null
}

export async function getWikiDocuments(wikiId: number): Promise<WikiDocumentsResult> {
  const client = getApiClient()
  const res = await client.api.wiki.byId(String(wikiId)).documents.get()
  return { wikiId: res?.wikiId, myRole: res?.myRole, items: res?.items ?? [] }
}

export async function getWikiDocumentDetail(documentId: number) {
  const client = getApiClient()
  return client.api.wiki.document.byDocumentId(String(documentId)).get()
}

export async function createWikiDocument(wikiId: number, payload: { title: string; content?: string }): Promise<number> {
  const client = getApiClient()
  const res = await client.api.wiki.byId(String(wikiId)).documents.post({ title: payload.title, content: payload.content })
  return Number(res?.value ?? 0)
}

export async function updateWikiDocument(documentId: number, payload: { title: string; content?: string }): Promise<void> {
  const client = getApiClient()
  await client.api.wiki.document.byDocumentId(String(documentId)).put({ title: payload.title, content: payload.content })
}

export async function deleteWikiDocument(documentId: number): Promise<void> {
  const client = getApiClient()
  await client.api.wiki.document.byDocumentId(String(documentId)).delete()
}
