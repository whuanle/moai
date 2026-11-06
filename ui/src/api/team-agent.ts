import { getApiClient } from '@/api/kiota'
import { uploadImageWithKey } from '@/utils/storage'

/** 子智能体定义：主智能体运行时按 description 决定何时派发（Phase 2 派发，本期管理面定义） */
export interface TeamAgentSubItem {
  /** 标识 key，智能体内唯一，小写字母开头的小写字母/数字/下划线；运行时作为 subagent_{key} 工具名 */
  key?: string | null
  /** 展示名（可中文） */
  name?: string | null
  /** 主智能体判断"何时派发它"的唯一依据 */
  description?: string | null
  /** 子智能体系统提示词 */
  prompt?: string | null
  /** 插件依赖（团队可用插件 id 列表） */
  pluginIds?: string[] | null
  /** 知识库依赖（本团队知识库 id 列表，后端 long 序列化为字符串） */
  wikiIds?: string[] | null
  /** 知识图谱依赖（本团队托管图谱 id 列表，后端 long 序列化为字符串） */
  graphIds?: string[] | null
}

export interface TeamAgentListItem {
  id?: string | null
  /** 标识 key，团队内唯一，创建后不可变更 */
  key?: string | null
  name?: string | null
  description?: string | null
  /** 头像 ObjectKey（空串=未设置），用 resolveStorageUrl 转可访问地址 */
  avatarObjectKey?: string | null
  enabled?: boolean | null
  /** 系统内置智能体（团队创建时自动种入，不可删除、可编辑定制） */
  isSystem?: boolean | null
  subCount?: number | null
  createUserId?: number | null
  createUserName?: string | null
  createTime?: string | null
  updateTime?: string | null
  updateUserName?: string | null
}

export interface TeamAgentDetail {
  id?: string | null
  teamId?: string | null
  /** 标识 key，团队内唯一，创建后不可变更 */
  key?: string | null
  name?: string | null
  description?: string | null
  avatarObjectKey?: string | null
  enabled?: boolean | null
  /** 系统内置智能体（团队创建时自动种入，不可删除、可编辑定制） */
  isSystem?: boolean | null
  mainPrompt?: string | null
  mainPluginIds?: string[] | null
  mainWikiIds?: string[] | null
  mainGraphIds?: string[] | null
  subs?: TeamAgentSubItem[] | null
  createUserId?: number | null
  createUserName?: string | null
  createTime?: string | null
  updateTime?: string | null
  updateUserName?: string | null
}

export interface TeamAgentSavePayload {
  key?: string
  name: string
  description?: string
  enabled?: boolean
  mainPrompt: string
  mainPluginIds?: string[]
  mainWikiIds?: string[]
  mainGraphIds?: string[]
  subs: TeamAgentSubItem[]
}

export interface GetTeamAgentsParams {
  searchText?: string
}

/** 查询团队智能体列表（团队成员可见），返回当前用户角色 2=Owner 1=Admin 0=Member */
export async function getTeamAgents(
  teamId: number,
  params?: GetTeamAgentsParams,
): Promise<{ myRole: number; items: TeamAgentListItem[] }> {
  const client = getApiClient()
  const res = await client.api.teamAgent.list.get({
    queryParameters: {
      teamId: String(teamId),
      searchText: params?.searchText || undefined,
    },
  })
  return { myRole: res?.myRole ?? 0, items: (res?.items ?? []) as TeamAgentListItem[] }
}

export async function getTeamAgent(id: string): Promise<TeamAgentDetail> {
  const client = getApiClient()
  const res = await client.api.teamAgent.byId(id).get()
  return (res?.agent ?? {}) as TeamAgentDetail
}

export async function createTeamAgent(teamId: number, payload: TeamAgentSavePayload, avatar?: string): Promise<string | undefined> {
  const client = getApiClient()
  const res = await client.api.teamAgent.post({
    teamId: String(teamId),
    key: payload.key ?? '',
    name: payload.name,
    description: payload.description ?? '',
    enabled: payload.enabled ?? true,
    // 创建时若要带头像，objectKey 须先走存储直传完成登记
    avatar,
    mainPrompt: payload.mainPrompt,
    mainPluginIds: payload.mainPluginIds ?? [],
    mainWikiIds: payload.mainWikiIds ?? [],
    mainGraphIds: payload.mainGraphIds ?? [],
    subs: payload.subs,
  })
  return res?.value ?? undefined
}

export async function updateTeamAgent(id: string, payload: TeamAgentSavePayload): Promise<void> {
  const client = getApiClient()
  await client.api.teamAgent.byId(id).put({
    name: payload.name,
    description: payload.description ?? '',
    enabled: payload.enabled ?? true,
    mainPrompt: payload.mainPrompt,
    mainPluginIds: payload.mainPluginIds ?? [],
    mainWikiIds: payload.mainWikiIds ?? [],
    mainGraphIds: payload.mainGraphIds ?? [],
    subs: payload.subs,
  })
}

export async function deleteTeamAgent(id: string): Promise<void> {
  const client = getApiClient()
  await client.api.teamAgent.byId(id).delete()
}

/** 为全部存量团队补种内置智能体（规划师/构建师，按名称幂等），仅平台管理员；返回本次种入数量 */
export async function seedBuiltinTeamAgents(): Promise<number> {
  const client = getApiClient()
  const res = await client.api.teamAgent.seedBuiltin.post()
  return res?.value ?? 0
}

/** 设置智能体头像（objectKey 为直传完成并登记的图片 key） */
export async function setTeamAgentAvatar(id: string, objectKey: string): Promise<void> {
  const client = getApiClient()
  await client.api.teamAgent.byId(id).avatar.post({ objectKey })
}

/** 上传图片并设为智能体头像（走存储直传管线），返回公开访问地址 */
export async function uploadTeamAgentAvatar(id: string, file: File): Promise<string> {
  const { objectKey, url } = await uploadImageWithKey(file)
  await setTeamAgentAvatar(id, objectKey)
  return url
}
