import { type Guid } from '@microsoft/kiota-abstractions'
import { getApiClient } from '@/api/kiota'
import { fromUntypedNode, toUntypedNode } from '@/api/untyped'
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
  /** 是否外部应用：false=内部应用，true=外部应用（仅外部用户/匿名可用） */
  isExternal?: boolean | null
  /** 是否需要授权访问；仅外部应用有效（需经应用接入 key 换 token） */
  isAuth?: boolean | null
  /** 是否公开到平台；仅内部应用有效（平台内任意用户可用） */
  isPublic?: boolean | null
  /** 发布状态：0=草稿（未发布）1=已发布 */
  publishStatus?: number | null
  /** 发布时间，未发布为 null */
  publishTime?: string | null
  createTime?: string | null
}

export interface AppsResult {
  teamId?: string | number | null
  /** 0=Member 1=Admin 2=Owner；-1=非团队成员（仅公开应用可只读查看） */
  myRole?: number | null
  items?: AppItem[] | null
}

/** 查询某个团队下的内部应用列表（外部应用不入此列表） */
export async function getApps(teamId: number): Promise<AppsResult> {
  const client = getApiClient()
  const res = await client.api.app.list.get({
    queryParameters: { teamId: String(teamId) },
  })
  return { teamId: res?.teamId, myRole: res?.myRole, items: res?.items ?? [] }
}

/** 查询某个团队下的外部应用列表（仅团队 Admin+） */
export async function getExternalApps(teamId: number): Promise<AppsResult> {
  const client = getApiClient()
  const res = await client.api.app.external.list.get({
    queryParameters: { teamId: String(teamId) },
  })
  return { teamId: res?.teamId, myRole: res?.myRole, items: res?.items ?? [] }
}

/** 查询平台公开应用（内部且已公开、已发布、未禁用），任意登录用户可访问 */
export async function getPublicApps(): Promise<AppItem[]> {
  const client = getApiClient()
  const res = await client.api.app.public.list.get()
  return (res?.items ?? []) as AppItem[]
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
  /** 是否外部应用；缺省=内部应用 */
  isExternal?: boolean
  /** 是否需要授权访问；仅外部应用有效 */
  isAuth?: boolean
  /** 是否公开到平台；仅内部应用有效 */
  isPublic?: boolean
}): Promise<string> {
  const client = getApiClient()
  const res = await client.api.app.post({
    teamId: String(payload.teamId),
    name: payload.name,
    description: payload.description,
    appType: payload.appType,
    avatar: payload.avatar,
    isExternal: payload.isExternal,
    isAuth: payload.isAuth,
    isPublic: payload.isPublic,
  })
  return String(res?.value ?? '')
}

/** 基础信息更新：名称、描述、授权/公开开关（应用类型创建后不可修改；头像走独立接口） */
export async function updateApp(
  appId: string,
  payload: { name: string; description?: string; isExternal?: boolean; isAuth?: boolean; isPublic?: boolean },
): Promise<void> {
  const client = getApiClient()
  await client.api.app.byId(appId).put({
    name: payload.name,
    description: payload.description,
    isExternal: payload.isExternal,
    isAuth: payload.isAuth,
    isPublic: payload.isPublic,
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
  /** 对话执行参数（自由 JSON，含沙箱等扩展配置） */
  executionSettings?: Record<string, unknown> | null
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
    executionSettings: (fromUntypedNode(res?.executionSettings) as Record<string, unknown> | undefined) ?? {},
    myRole: res?.myRole ?? null,
  }
}

/**
 * 保存 Agent 应用配置；对话模型与绑定的插件/知识库必须在该团队有权使用的范围内（后端校验）。
 * `modelId` 传 null / 空串表示不选择模型。
 */
export async function saveAppAgentConfig(
  appId: string,
  payload: {
    modelId?: string | null
    prompt: string
    wikiIds: number[]
    plugins: string[]
    executionSettings?: Record<string, unknown>
  },
): Promise<void> {
  const client = getApiClient()
  await client.api.app.byId(appId).agentConfig.put({
    modelId: (payload.modelId || null) as Guid | null,
    prompt: payload.prompt,
    // 后端 wiki_ids 为 long，Kiota 生成的请求体为 string[]，此处按生成类型传字符串
    wikiIds: payload.wikiIds.map((id) => String(id)),
    plugins: payload.plugins as Guid[],
    executionSettings: payload.executionSettings ? toUntypedNode(payload.executionSettings) : null,
  })
}

// ==================== 应用发布 ====================

/** 发布应用（发布后团队成员可进入对话），仅团队 Admin+ 且仅 Agent 应用 */
export async function publishApp(appId: string): Promise<void> {
  const client = getApiClient()
  await client.api.app.byId(appId).publish.post()
}

/** 取消发布应用，仅团队 Admin+ */
export async function unpublishApp(appId: string): Promise<void> {
  const client = getApiClient()
  await client.api.app.byId(appId).unpublish.post()
}

// ==================== Agent 会话 ====================

export interface AppSessionItem {
  sessionId?: string | null
  appId?: string | null
  title?: string | null
  userType?: number | null
  inputTokens?: number | null
  outTokens?: number | null
  totalTokens?: number | null
  lastMessageTime?: string | null
  createTime?: string | null
}

export interface AspAppSessionMessageItem {
  messageId?: string | null
  seq?: number | null
  role?: string | null
  content?: string | null
  toolCalls?: string | null
  toolCallId?: string | null
  reasoning?: string | null
  completionsId?: string | null
  createTime?: string | null
}

/** 查询当前用户在某个应用下的会话列表（倒序） */
export async function getAppSessions(appId: string): Promise<AppSessionItem[]> {
  const client = getApiClient()
  const res = await client.api.app.byId(appId).session.list.get()
  return (res?.items ?? []) as AppSessionItem[]
}

/** 创建 Agent 应用会话，返回会话 id（前端 threadId） */
export async function createAppSession(appId: string, title?: string): Promise<string> {
  const client = getApiClient()
  const res = await client.api.app.byId(appId).session.post({ title })
  return String(res?.value ?? '')
}

/** 创建调试会话：未发布应用也可调试；会话仅存 Redis、不落库、不计用量，刷新即弃用 */
export async function createDebugSession(appId: string): Promise<string> {
  const client = getApiClient()
  const res = await client.api.app.byId(appId).debug.session.post()
  return String(res?.value ?? '')
}

/** 查询会话消息（落库为压缩后视图，按 seq 升序） */
export async function getAppSessionMessages(sessionId: string): Promise<AspAppSessionMessageItem[]> {
  const client = getApiClient()
  const res = await client.api.app.session.bySessionId(sessionId).messages.get()
  return (res?.items ?? []) as AspAppSessionMessageItem[]
}

/** 重命名会话 */
export async function renameAppSession(sessionId: string, title: string): Promise<void> {
  const client = getApiClient()
  await client.api.app.session.bySessionId(sessionId).title.put({ title })
}

/** 删除会话（软删除，连同消息） */
export async function deleteAppSession(sessionId: string): Promise<void> {
  const client = getApiClient()
  await client.api.app.session.bySessionId(sessionId).delete()
}

// ==================== 应用对话日志（团队管理端） ====================

/** 应用对话日志条目（正式会话的压缩后视图，含归属用户与 token 用量） */
export interface AppLogItem {
  sessionId?: string | null
  title?: string | null
  /** 会话归属用户类型：'none' | 'external' | 'externalApp' | 'normal' */
  userType?: string | null
  /** 会话归属用户原始 id（内部为 user.id，外部为 external.id），后端 long 序列化为字符串 */
  ownerId?: string | number | null
  inputTokens?: number | null
  outTokens?: number | null
  totalTokens?: number | null
  lastMessageTime?: string | null
  createTime?: string | null
  updateTime?: string | null
  createUserId?: number | null
  createUserName?: string | null
}

export interface AppLogsResult {
  items: AppLogItem[]
  total: number
  pageNo: number
  pageSize: number
}

export interface QueryAppLogsParams {
  pageNo: number
  pageSize: number
  keyword?: string
  /** 会话归属用户类型过滤：'normal' | 'external' | 'externalApp'；后端按枚举名不区分大小写绑定 */
  userType?: string
  /** 最后消息时间下界（含），ISO 字符串（RangePicker.toISOString()） */
  from?: string
  /** 最后消息时间上界（含），ISO 字符串（RangePicker.toISOString()） */
  to?: string
}

/** 分页查询应用对话日志（全部用户正式会话，压缩后视图）；需要团队 Admin 及以上角色 */
export async function getAppLogs(appId: string, params: QueryAppLogsParams): Promise<AppLogsResult> {
  const client = getApiClient()
  const res = await client.api.app.byId(appId).logs.get({
    queryParameters: {
      pageNo: params.pageNo,
      pageSize: params.pageSize,
      keyword: params.keyword || undefined,
      userType: params.userType || undefined,
      from: params.from || undefined,
      to: params.to || undefined,
    },
  })
  return {
    items: (res?.items ?? []).map((item) => ({
      sessionId: item.sessionId,
      title: item.title,
      userType: item.userType,
      ownerId: item.ownerId,
      inputTokens: item.inputTokens,
      outTokens: item.outTokens,
      totalTokens: item.totalTokens,
      lastMessageTime: item.lastMessageTime,
      createTime: item.createTime,
      updateTime: item.updateTime,
      createUserId: item.createUserId,
      createUserName: item.createUserName,
    })),
    total: res?.total ?? 0,
    pageNo: res?.pageNo ?? params.pageNo,
    pageSize: res?.pageSize ?? params.pageSize,
  }
}

/** 查询指定会话的消息详情（压缩后视图，按 seq 升序）；需要团队 Admin 及以上角色 */
export async function getAppLogMessages(appId: string, sessionId: string): Promise<AspAppSessionMessageItem[]> {
  const client = getApiClient()
  const res = await client.api.app.byId(appId).logs.bySessionId(sessionId).messages.get()
  return (res?.items ?? []) as AspAppSessionMessageItem[]
}

// ==================== 应用用量监控（团队管理端） ====================

const toFiniteNumber = (value: unknown): number => {
  const num = Number(value ?? 0)
  return Number.isFinite(num) ? num : 0
}

export interface AppUsageSummary {
  callCount: number
  promptTokens: number
  completionTokens: number
  totalTokens: number
}

export interface AppUsageModelItem {
  modelId?: string | null
  modelName?: string | null
  callCount: number
  promptTokens: number
  completionTokens: number
  totalTokens: number
}

export interface AppUsageResult {
  summary: AppUsageSummary
  byModel: AppUsageModelItem[]
}

/** 查询应用用量统计（汇总 + 按模型分布）；后端 long 以字符串下发，此处归一化为 number */
export async function getAppUsage(appId: string): Promise<AppUsageResult> {
  const client = getApiClient()
  const res = await client.api.app.byId(appId).usage.get()
  const summary = res?.summary
  return {
    summary: {
      callCount: toFiniteNumber(summary?.callCount),
      promptTokens: toFiniteNumber(summary?.promptTokens),
      completionTokens: toFiniteNumber(summary?.completionTokens),
      totalTokens: toFiniteNumber(summary?.totalTokens),
    },
    byModel: (res?.byModel ?? []).map((item) => ({
      modelId: item.modelId,
      modelName: item.modelName,
      callCount: toFiniteNumber(item.callCount),
      promptTokens: toFiniteNumber(item.promptTokens),
      completionTokens: toFiniteNumber(item.completionTokens),
      totalTokens: toFiniteNumber(item.totalTokens),
    })),
  }
}
