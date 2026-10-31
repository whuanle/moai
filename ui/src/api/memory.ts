import { getApiClient } from '@/api/kiota'

/** 长期记忆条目（对齐后端 MemoryItem） */
export interface MyMemoryItem {
  id?: string | null
  appId?: string | null
  /** app 应用内记忆 / global 全局（跨应用）记忆 */
  scope?: string | null
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
  await client.api.app.byId(appId).memory.mine.byMemoryId(memoryId).delete()
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

// ==================== 记忆管理（跨应用） ====================

/** 记忆总览条目（本人在某应用的记忆概况） */
export interface MyMemoryAppItem {
  appId?: string | null
  appName?: string | null
  teamId?: number | null
  teamName?: string | null
  memoryCount?: number | null
  lastActiveTime?: string | null
}

/** 记忆总览响应 */
export interface MyMemoryOverview {
  apps: MyMemoryAppItem[]
  global: { enabled: boolean; myCount: number }
}

/** 查询本人的记忆总览：有记忆的应用列表与全局记忆概况 */
export async function getMyMemoryOverview(): Promise<MyMemoryOverview> {
  const client = getApiClient()
  const res = await client.api.memory.overview.get()
  const raw = res as unknown as {
    apps?: MyMemoryAppItem[] | null
    global?: { enabled?: boolean | null; myCount?: number | null } | null
  } | undefined
  return {
    apps: raw?.apps ?? [],
    global: {
      enabled: raw?.global?.enabled === true,
      myCount: raw?.global?.myCount ?? 0,
    },
  }
}

/** 编辑本人的一条应用内记忆（内容重嵌向量） */
export async function updateMyMemory(appId: string, memoryId: string, content: string, memoryType: number): Promise<void> {
  const client = getApiClient()
  await client.api.app.byId(appId).memory.mine.byMemoryId(memoryId).put({
    appId,
    memoryId,
    content,
    memoryType,
  })
}

/** 全局记忆列表结果 */
export interface MyGlobalMemoriesResult {
  items: MyMemoryItem[]
  total: number
  /** 平台是否开启全局记忆 */
  globalEnabled: boolean
}

/** 分页查询本人的全局记忆（按创建时间倒序） */
export async function getMyGlobalMemories(page = 1, pageSize = 20): Promise<MyGlobalMemoriesResult> {
  const client = getApiClient()
  const res = await client.api.memory.global.mine.get({
    queryParameters: { page, pageSize },
  })
  const raw = res as unknown as {
    items?: MyMemoryItem[] | null
    total?: number | null
    globalEnabled?: boolean | null
  } | undefined
  return {
    items: raw?.items ?? [],
    total: raw?.total ?? 0,
    globalEnabled: raw?.globalEnabled === true,
  }
}

/** 新增本人的一条全局记忆（平台未开启时后端返回 400） */
export async function saveMyGlobalMemory(content: string, memoryType: number): Promise<MyMemoryItem> {
  const client = getApiClient()
  const res = await client.api.memory.global.mine.post({ content, memoryType })
  return (res ?? {}) as MyMemoryItem
}

/** 把本人的一条应用内记忆存为全局记忆（原应用记忆保留） */
export async function promoteMemoryToGlobal(appId: string, memoryId: string): Promise<MyMemoryItem> {
  const client = getApiClient()
  const res = await client.api.memory.global.promote.post({ appId, memoryId })
  return (res ?? {}) as MyMemoryItem
}

/** 编辑本人的一条全局记忆 */
export async function updateMyGlobalMemory(memoryId: string, content: string, memoryType: number): Promise<void> {
  const client = getApiClient()
  await client.api.memory.global.mine.byMemoryId(memoryId).put({
    memoryId,
    content,
    memoryType,
  })
}

/** 删除本人的一条全局记忆 */
export async function deleteMyGlobalMemory(memoryId: string): Promise<void> {
  const client = getApiClient()
  await client.api.memory.global.mine.byMemoryId(memoryId).delete()
}

/** 清空本人的全部全局记忆 */
export async function clearMyGlobalMemory(): Promise<void> {
  const client = getApiClient()
  await client.api.memory.global.mine.delete()
}

/** 全局记忆可选向量化模型（root，供系统设置选择） */
export interface GlobalMemoryModelOption {
  id?: string | null
  name?: string | null
}

/** 查询全局记忆可选向量化模型（公开启用中的 embedding 模型，root） */
export async function getGlobalMemoryModelOptions(): Promise<GlobalMemoryModelOption[]> {
  const client = getApiClient()
  const res = await client.api.memory.global.modelOptions.get()
  const raw = res as unknown as { items?: GlobalMemoryModelOption[] | null } | undefined
  return raw?.items ?? []
}

/** 清空平台全部记忆（所有应用记忆 + 全部全局记忆）并解除统一向量化模型/维度锁定（root，危险操作） */
export async function clearAllMemory(): Promise<void> {
  const client = getApiClient()
  await client.api.memory.all.delete()
}

/** 迁移任务状态 */
export interface MemoryMigrationTaskStateInfo {
  taskId?: string | null
  state?: number | null
  message?: string | null
  targetModelId?: string | null
  targetModelName?: string | null
  targetDimensions?: number | null
  totalCount?: number | null
  migratedCount?: number | null
  createTime?: string | null
  updateTime?: string | null
}

/** 长期记忆向量化模型迁移状态（root） */
export interface MemoryMigrationStatus {
  currentModelId?: string | null
  currentModelName?: string | null
  currentDimensions?: number | null
  totalMemoryCount?: number | null
  activeMigration?: MemoryMigrationTaskStateInfo | null
}

/** 查询长期记忆向量化模型迁移状态：当前生效模型/维度、平台记忆总数与最近一次迁移任务进度（root） */
export async function getMemoryModelMigrationStatus(): Promise<MemoryMigrationStatus> {
  const client = getApiClient()
  const res = await client.api.memory.modelMigration.get()
  return (res ?? {}) as MemoryMigrationStatus
}

/** 发起长期记忆向量化模型迁移（root）：用新模型/维度重建平台全部记忆向量，返回迁移任务 id */
export async function startMemoryModelMigration(modelId: string, dimensions: number): Promise<string> {
  const client = getApiClient()
  const res = await client.api.memory.modelMigration.post({ modelId, dimensions })
  return res?.taskId ?? ''
}
