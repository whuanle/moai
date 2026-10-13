import { getApiClient } from '@/api/kiota'

/** 长期记忆条目（对齐后端 MemoryItem） */
export interface MyMemoryItem {
  id?: string | null
  appId?: string | null
  /** 0 语义 / 1 情景 / 2 程序 */
  memoryType?: number | null
  content?: string | null
  score?: number | null
  createTime?: string | null
  updateTime?: string | null
}

export interface MyMemoriesResult {
  items: MyMemoryItem[]
  total: number
  /** 应用是否开启记忆 */
  memoryEnabled: boolean
}

/** 分页查询本人在某应用下的长期记忆（按创建时间倒序） */
export async function getMyMemories(appId: string, page = 1, pageSize = 20): Promise<MyMemoriesResult> {
  const client = getApiClient()
  const res = await client.api.app.byId(appId).memory.mine.get({
    queryParameters: { page, pageSize },
  })
  const raw = res as unknown as {
    items?: MyMemoryItem[] | null
    total?: number | null
    memoryEnabled?: boolean | null
  } | undefined
  return {
    items: raw?.items ?? [],
    total: raw?.total ?? 0,
    memoryEnabled: raw?.memoryEnabled === true,
  }
}

/** 删除本人的一条长期记忆 */
export async function deleteMyMemory(appId: string, memoryId: string): Promise<void> {
  const client = getApiClient()
  await client.api.app.byId(appId).memory.mine.byId(memoryId).delete()
}

/** 清空本人在该应用内的全部长期记忆 */
export async function clearMyAppMemory(appId: string): Promise<void> {
  const client = getApiClient()
  await client.api.app.byId(appId).memory.mine.delete()
}

/** 应用记忆统计（团队管理员视角） */
export interface AppMemoryStats {
  totalMemories: number
  userCount: number
}

/** 查询应用记忆统计（全部用户的记忆总条数与用户数，团队管理员） */
export async function getAppMemoryStats(appId: string): Promise<AppMemoryStats> {
  const client = getApiClient()
  const res = await client.api.app.byId(appId).memory.stats.get()
  const raw = res as unknown as Partial<AppMemoryStats> | undefined
  return { totalMemories: raw?.totalMemories ?? 0, userCount: raw?.userCount ?? 0 }
}

/** 清空应用内全部用户的长期记忆（团队管理员） */
export async function clearAppMemory(appId: string): Promise<void> {
  const client = getApiClient()
  await client.api.app.byId(appId).memory.delete()
}
