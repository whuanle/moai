import { getApiClient } from '@/api/kiota'

/** 提示词列表项/详情（Kiota 生成类型的业务侧投影） */
export interface PromptItem {
  /** 后端 int 序列化为数字 */
  promptId?: number | null
  name?: string | null
  description?: string | null
  avatarPath?: string | null
  /** 0 表示未分类 */
  promptClassId?: number | null
  /** 是否已上架到提示词市场 */
  isPublic?: boolean | null
  /** 市场提示词被他人查看次数 */
  counter?: number | null
  /** 0 表示个人提示词 */
  teamId?: number | null
  /** 待审核的上架申请 id（后端 long 序列化为字符串），无待审核申请时为 null */
  pendingPublicationId?: string | number | null
  createTime?: string | null
  createUserName?: string | null
  updateTime?: string | null
  updateUserName?: string | null
}

/** 提示词详情，含内容 */
export interface PromptDetail extends PromptItem {
  content?: string | null
}

export async function getMyPrompts(filters?: { keywords?: string; promptClassId?: number }): Promise<PromptItem[]> {
  const client = getApiClient()
  const res = await client.api.prompt.my_list.get({
    queryParameters: {
      keywords: filters?.keywords || undefined,
      promptClassId: filters?.promptClassId || undefined,
    },
  })
  return (res?.items ?? []) as PromptItem[]
}

export async function getTeamPrompts(teamId: number, filters?: { keywords?: string; promptClassId?: number }): Promise<PromptItem[]> {
  const client = getApiClient()
  const res = await client.api.prompt.team_list.get({
    queryParameters: {
      teamId,
      keywords: filters?.keywords || undefined,
      promptClassId: filters?.promptClassId || undefined,
    },
  })
  return (res?.items ?? []) as PromptItem[]
}

export async function getPromptMarketList(filters?: { keywords?: string; promptClassId?: number }): Promise<PromptItem[]> {
  const client = getApiClient()
  const res = await client.api.prompt.market_list.get({
    queryParameters: {
      keywords: filters?.keywords || undefined,
      promptClassId: filters?.promptClassId || undefined,
    },
  })
  return (res?.items ?? []) as PromptItem[]
}

export async function getPromptDetail(promptId: number): Promise<PromptDetail | undefined> {
  const client = getApiClient()
  return client.api.prompt.byId(promptId).get()
}

/** 创建提示词：teamId=0 个人提示词，teamId>0 团队提示词（需团队 Admin+） */
export async function createPrompt(payload: {
  teamId?: number
  name: string
  description?: string
  content: string
  promptClassId?: number
  avatarPath?: string
}): Promise<number> {
  const client = getApiClient()
  const res = await client.api.prompt.post({
    teamId: payload.teamId ?? 0,
    name: payload.name,
    description: payload.description,
    content: payload.content,
    promptClassId: payload.promptClassId ?? 0,
    avatarPath: payload.avatarPath,
  })
  return Number(res?.value ?? 0)
}

export async function updatePrompt(
  promptId: number,
  payload: { name: string; description?: string; content: string; promptClassId?: number; avatarPath?: string },
): Promise<void> {
  const client = getApiClient()
  await client.api.prompt.byId(promptId).put({
    promptId: null,
    name: payload.name,
    description: payload.description,
    content: payload.content,
    promptClassId: payload.promptClassId ?? 0,
    avatarPath: payload.avatarPath,
  })
}

export async function deletePrompt(promptId: number): Promise<void> {
  const client = getApiClient()
  await client.api.prompt.byId(promptId).delete()
}
