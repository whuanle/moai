import { type Guid } from '@microsoft/kiota-abstractions'
import { getApiClient } from '@/api/kiota'
import { uploadImageWithKey } from '@/utils/storage'

/** 应用类型：agent=Agent 应用，workflow=流程应用（对齐后端 AppType 枚举） */
export type AppKind = 'agent' | 'workflow'

export interface AppItem {
  /** 后端 Guid 序列化为字符串 */
  appId?: string | null
  /** 后端 long 序列化为字符串 */
  teamId?: string | number | null
  name?: string | null
  description?: string | null
  appType?: AppKind | null
  /** 应用头像的 ObjectKey（空串=未设置），前端用 resolveStorageUrl 转可访问地址 */
  avatarPath?: string | null
  /** 允许外部使用；对应后端 app.enable_foreign（外部用户使用能力本身待交付，此处仅存取） */
  enableForeign?: boolean | null
  createTime?: string | null
}

export interface AppsResult {
  teamId?: string | number | null
  /** 0=Member 1=Admin 2=Owner */
  myRole?: number | null
  items?: AppItem[] | null
}

/** 查询某个团队下的应用列表（应用是团队下的产物，没有跨团队的全局列表） */
export async function getApps(teamId: number): Promise<AppsResult> {
  const client = getApiClient()
  const res = await client.api.app.list.get({
    queryParameters: { teamId: String(teamId) },
  })
  return { teamId: res?.teamId, myRole: res?.myRole, items: res?.items ?? [] }
}

export async function getAppDetail(appId: string) {
  const client = getApiClient()
  return client.api.app.byId(appId).get()
}

export async function createApp(payload: {
  teamId: number
  name: string
  description?: string
  appType: AppKind
  /** 头像 objectKey：先走存储直传管线拿 key，再随创建请求一起提交（应用此时还不存在，无法调头像接口） */
  avatar?: string
  /** 允许外部使用 */
  enableForeign?: boolean
}): Promise<string> {
  const client = getApiClient()
  const res = await client.api.app.post({
    teamId: String(payload.teamId),
    name: payload.name,
    description: payload.description,
    appType: payload.appType,
    avatar: payload.avatar,
    enableForeign: payload.enableForeign,
  })
  return String(res?.value ?? '')
}

/** 基础信息更新：名称、描述、允许外部使用（应用类型创建后不可修改；头像走独立接口） */
export async function updateApp(
  appId: string,
  payload: { name: string; description?: string; enableForeign?: boolean },
): Promise<void> {
  const client = getApiClient()
  await client.api.app.byId(appId).put({
    name: payload.name,
    description: payload.description,
    enableForeign: payload.enableForeign,
  })
}

/** 上传并设置应用头像，返回可访问的图片地址 */
export async function uploadAppAvatar(appId: string, file: File): Promise<string> {
  const { objectKey, url } = await uploadImageWithKey(file)
  const client = getApiClient()
  await client.api.app.byId(appId).avatar.post({ objectKey })
  return url
}

// ==================== Agent 应用配置 ====================

export interface AppAgentConfig {
  appId?: string | null
  teamId?: string | number | null
  appType?: AppKind | null
  /** 系统提示词，未配置时为空串 */
  prompt?: string | null
  /** 对话模型 id（uuid）；模型选择未开放时为空 Guid */
  modelId?: string | null
  /** 允许使用的知识库 id 列表（元素为 wiki.id） */
  wikiIds?: number[] | null
  /** 允许使用的插件 id 列表（元素为 plugin.id，uuid 字符串） */
  plugins?: string[] | null
  /** 0=Member 1=Admin 2=Owner */
  myRole?: number | null
}

/** 查询 Agent 应用配置（插件/知识库绑定与提示词）；未保存过配置时后端返回空配置 */
export async function getAppAgentConfig(appId: string): Promise<AppAgentConfig> {
  const client = getApiClient()
  const res = await client.api.app.byId(appId).agentConfig.get()
  return {
    appId: res?.appId,
    teamId: res?.teamId,
    appType: (res?.appType as AppKind | null | undefined) ?? null,
    prompt: res?.prompt ?? '',
    modelId: res?.modelId ?? null,
    // Kiota 把后端 long 生成为 string，前端统一收敛为 number 便于与 wikiId 比较
    wikiIds: (res?.wikiIds ?? []).map((id) => Number(id)),
    plugins: (res?.plugins ?? []).map((id) => String(id)),
    myRole: res?.myRole ?? null,
  }
}

/**
 * 保存 Agent 应用配置；对话模型与绑定的插件/知识库必须在该团队有权使用的范围内（后端校验）。
 * `modelId` 传 null / 空串表示不选择模型。
 */
export async function saveAppAgentConfig(
  appId: string,
  payload: { modelId?: string | null; prompt: string; wikiIds: number[]; plugins: string[] },
): Promise<void> {
  const client = getApiClient()
  await client.api.app.byId(appId).agentConfig.put({
    modelId: (payload.modelId || null) as Guid | null,
    prompt: payload.prompt,
    // 后端 wiki_ids 为 long，Kiota 生成的请求体为 string[]，此处按生成类型传字符串
    wikiIds: payload.wikiIds.map((id) => String(id)),
    plugins: payload.plugins as Guid[],
  })
}
