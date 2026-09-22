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
  })
  return String(res?.value ?? '')
}

/**
 * 基础信息更新：名称、描述、授权开关（应用类型创建后不可修改；头像走独立接口）。
 * 公开（is_public）只能通过「上架审核」由系统管理员审批后设置，此处不再提供。
 */
export async function updateApp(
  appId: string,
  payload: { name: string; description?: string; isExternal?: boolean; isAuth?: boolean },
): Promise<void> {
  const client = getApiClient()
  await client.api.app.byId(appId).put({
    name: payload.name,
    description: payload.description,
    isExternal: payload.isExternal,
    isAuth: payload.isAuth,
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

/** 沙箱资源上限（每个应用可配置的存活时间 / CPU / 内存最大值，由超级管理员在系统设置调整） */
export interface SandboxLimits {
  maxTtlSeconds: number
  maxCpu: string
  maxMemory: string
}

/** 查询沙箱资源上限；接口异常时回退内置默认值（与后端 SettingDefinitions 一致） */
export async function getSandboxLimits(): Promise<SandboxLimits> {
  const client = getApiClient()
  const res = await client.api.app.sandboxLimits.get()
  return {
    maxTtlSeconds: res?.maxTtlSeconds ?? 86400,
    maxCpu: res?.maxCpu || '4',
    maxMemory: res?.maxMemory || '8Gi',
  }
}

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
  /** 允许检索的知识图谱 id 列表（元素为 kg.id，仅托管图） */
  graphIds?: number[] | null
  /** 允许使用的插件 id 列表（元素为 plugin.id，uuid 字符串） */
  plugins?: string[] | null
  /** 绑定为工具的流程应用 id 列表（元素为 app.id，uuid 字符串，本团队已发布流程应用） */
  workflowApps?: string[] | null
  /** 应用默认使用的技能 id 列表（元素为 skill.id，uuid 字符串），用户可在应用设置中取消勾选 */
  skills?: string[] | null
  /** 对话执行参数（自由 JSON，含沙箱等扩展配置） */
  executionSettings?: Record<string, unknown> | null
  /** 对话开场白，未配置时为空串 */
  openingStatement?: string | null
  /** 是否启用对话开场白；启用且内容非空时，新会话开始时展示 */
  openingStatementEnabled?: boolean | null
  /** 快捷输入列表（管理员配置，对话欢迎态点击即发送），未配置为空列表 */
  quickInputs?: string[] | null
  /** 配置状态：0=草稿有未发布变更 1=当前草稿与已发布一致；已发布应用 0 时线上仍按发布快照执行 */
  status?: number | null
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
    graphIds: (res?.graphIds ?? []).map((id) => Number(id)),
    plugins: (res?.plugins ?? []).map((id) => String(id)),
    workflowApps: (res?.workflowApps ?? []).map((id) => String(id)),
    skills: (res?.skills ?? []).map((id) => String(id)),
    executionSettings: (fromUntypedNode(res?.executionSettings) as Record<string, unknown> | undefined) ?? {},
    openingStatement: res?.openingStatement ?? '',
    openingStatementEnabled: res?.openingStatementEnabled ?? false,
    quickInputs: (res?.quickInputs ?? []).map((x) => String(x)),
    status: res?.status ?? 0,
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
    /** 允许检索的知识图谱 id 列表（仅托管图，后端校验归属与模式） */
    graphIds?: number[]
    plugins: string[]
    /** 绑定为工具的流程应用 id 列表；null / 缺省表示保持已保存的流程应用绑定不变 */
    workflowApps?: string[] | null
    skills?: string[] | null
    openingStatement?: string
    openingStatementEnabled?: boolean
    /** 快捷输入列表；null / 缺省表示保持已保存的快捷输入不变 */
    quickInputs?: string[] | null
    executionSettings?: Record<string, unknown>
  },
): Promise<void> {
  const client = getApiClient()
  await client.api.app.byId(appId).agentConfig.put({
    modelId: (payload.modelId || null) as Guid | null,
    prompt: payload.prompt,
    // 后端 wiki_ids 为 long，Kiota 生成的请求体为 string[]，此处按生成类型传字符串
    wikiIds: payload.wikiIds.map((id) => String(id)),
    // 后端 graph_ids 为 long（仅托管图），同样按生成类型传字符串
    graphIds: (payload.graphIds ?? []).map((id) => String(id)),
    plugins: payload.plugins as Guid[],
    workflowApps: (payload.workflowApps ?? null) as Guid[] | null,
    skills: (payload.skills ?? null) as Guid[] | null,
    openingStatement: payload.openingStatement ?? '',
    openingStatementEnabled: payload.openingStatementEnabled ?? false,
    quickInputs: payload.quickInputs ?? null,
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
  /** 会话绑定的专家提示词 id，0 表示未绑定 */
  promptId?: number | null
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

/** 创建 Agent 应用会话，返回会话 id（前端 threadId）；promptId 为可选绑定的专家提示词 */
export async function createAppSession(appId: string, title?: string, promptId?: number): Promise<string> {
  const client = getApiClient()
  const res = await client.api.app.byId(appId).session.post({ title, promptId: promptId || 0 })
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

/** 设置会话绑定的专家提示词（promptId=0 清除绑定），仅会话归属用户可操作 */
export async function updateAppSessionPrompt(sessionId: string, promptId: number): Promise<void> {
  const client = getApiClient()
  await client.api.app.session.bySessionId(sessionId).prompt.put({ promptId })
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

// ==================== 用户级应用配置 ====================

/** 应用设置中展示的默认技能项（管理员配置） */
export interface AppUserSkillOption {
  /** 技能 id（uuid 字符串） */
  id?: string | null
  key?: string | null
  name?: string | null
  description?: string | null
  isSystem?: boolean | null
  /** 所属团队 id，0=系统内置或市场公开 */
  teamId?: number | null
}

export interface AppUserConfig {
  /** 用户当前选择的专家提示词 id，0=未选择 */
  promptId: number
  /** 用户当前勾选的技能 id 列表（应用默认技能的子集；未配置过时为默认技能全集） */
  skills: string[]
  /** 应用默认技能目录（管理员配置） */
  defaultSkills: AppUserSkillOption[]
  /** 工具审批模式：auto=自动执行；approval=重要工具调用前需人工批准 */
  toolApprovalMode: 'auto' | 'approval'
  /** 无需展示审批卡的工具名（只读检索类），与后端契约同步下发 */
  toolApprovalExemptNames: string[]
  /** 无需展示审批卡的工具名前缀（技能装载） */
  toolApprovalExemptPrefixes: string[]
  /** 审批策略自动放行的工具名（应用配置白名单插件产出），审批模式下后端直接执行 */
  toolApprovalAutoApprovedNames: string[]
  /** 审批策略自动放行的工具名前缀（沙箱工具 sandbox_），审批模式下后端直接执行 */
  toolApprovalAutoApprovedPrefixes: string[]
}

/** userconfig.get 响应的原始 JSON 形状（新契约字段经原始 JSON 投射读取，regen 后行为一致） */
interface RawAppUserConfigResponse {
  promptId?: number | null
  skills?: string[] | null
  defaultSkills?: AppUserSkillOption[] | null
  toolApprovalMode?: string | null
  toolApprovalExemptNames?: string[] | null
  toolApprovalExemptPrefixes?: string[] | null
  toolApprovalAutoApprovedNames?: string[] | null
  toolApprovalAutoApprovedPrefixes?: string[] | null
}

/**
 * 查询当前用户在某应用下的个性化配置与应用默认技能目录（含工具审批模式与审批卡豁免清单）。
 */
export async function getAppUserConfig(appId: string): Promise<AppUserConfig> {
  const client = getApiClient()
  const res = (await client.api.app.byId(appId).userconfig.get()) as unknown as RawAppUserConfigResponse
  return {
    promptId: res?.promptId ?? 0,
    skills: (res?.skills ?? []).map((id) => String(id)),
    defaultSkills: res?.defaultSkills ?? [],
    toolApprovalMode: res?.toolApprovalMode === 'approval' ? 'approval' : 'auto',
    toolApprovalExemptNames: res?.toolApprovalExemptNames ?? [],
    toolApprovalExemptPrefixes: res?.toolApprovalExemptPrefixes ?? [],
    toolApprovalAutoApprovedNames: res?.toolApprovalAutoApprovedNames ?? [],
    toolApprovalAutoApprovedPrefixes: res?.toolApprovalAutoApprovedPrefixes ?? [],
  }
}

/** 保存当前用户在某应用下的个性化配置，跨会话复用；技能仅能在应用默认范围内勾选 */
export async function saveAppUserConfig(
  appId: string,
  payload: { promptId: number; skills: string[]; toolApprovalMode?: 'auto' | 'approval' },
): Promise<void> {
  const client = getApiClient()
  await client.api.app.byId(appId).userconfig.put({
    promptId: payload.promptId,
    skills: payload.skills as Guid[],
    toolApprovalMode: payload.toolApprovalMode,
  })
}

/**
 * 对会话中挂起等待人工审批的工具调用做出决策（批准/拒绝）。
 * 返回后端状态：approved/rejected=决策已生效；missing=无匹配待审批记录（已自动执行或已被处理）。
 */
export async function decideAppSessionToolApproval(
  sessionId: string,
  toolName: string,
  approved: boolean,
): Promise<string> {
  const client = getApiClient()
  const res = await client.api.app.session.bySessionId(sessionId).toolApproval.post({
    toolName,
    approved,
  })
  return res?.status ?? 'missing'
}

/** 对话附件文本提取结果 */
export interface ChatAttachmentExtractResult {
  markdown: string
  contentLength: number
  truncated: boolean
}

/**
 * 对话附件文本提取：对已上传到 public/chat 目录的文档做提取，返回 markdown 文本。
 * 由前端拼进用户消息发送给模型（对话链路为纯文本）。
 */
export async function extractChatAttachment(
  objectKey: string,
  fileName: string,
): Promise<ChatAttachmentExtractResult> {
  const client = getApiClient()
  const res = await client.api.app.chatAttachment.extract.post({
    objectKey,
    fileName,
  })
  return {
    markdown: res?.markdown ?? '',
    contentLength: res?.contentLength ?? 0,
    truncated: res?.truncated ?? false,
  }
}
