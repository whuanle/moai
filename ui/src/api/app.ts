import { type Guid } from '@microsoft/kiota-abstractions'
import { getApiClient } from '@/api/kiota'
import { fromUntypedNode, toUntypedNode } from '@/api/untyped'
import { uploadImageWithKey } from '@/utils/storage'

/** 应用类型：agent=Agent 应用，workflow=流程应用（对齐后端 AppType 枚举） */
export type AppKind = 'agent' | 'workflow'

/** 后端 Guid 字段未配置时的全零值 */
const EMPTY_GUID = '00000000-0000-0000-0000-000000000000'

/** 应用开通的外部渠道（应用的对外服务入口，如飞书/钉钉机器人单聊） */
export interface AppExternalChannel {
  /** 渠道类型：feishu=飞书，dingtalk=钉钉（未回填机器人 dingtalk id 时用 AppKey 兜底生成链接） */
  channelType?: 'feishu' | 'dingtalk' | null
  /** 打开渠道对话的链接（飞书为 applink、钉钉为 jumprobot 机器人单聊地址），新窗口打开唤起客户端 */
  chatUrl?: string | null
}

export interface AppItem {
  /** 后端 Guid 序列化为字符串 */
  appId?: string | null
  /** 后端 long 序列化为字符串 */
  teamId?: string | number | null
  /** 所属团队名称（团队已解散时为空串） */
  teamName?: string | null
  /** 使用次数（累计会话数，与用量监控会话数同口径） */
  useCount?: number | null
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
  /** 分类 id，0=未分类 */
  classifyId?: number | null
  /** 发布状态：0=草稿（未发布）1=已发布 */
  publishStatus?: number | null
  /** 发布时间，未发布为 null */
  publishTime?: string | null
  createTime?: string | null
  /** 当前用户是否已收藏（仅公开列表填充，团队/外部列表恒为 false） */
  isFavorite?: boolean | null
  /** 应用开通的外部渠道列表（如飞书/钉钉机器人单聊入口），未开通为空 */
  externalChannels?: AppExternalChannel[] | null
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

/** 查询平台公开应用（内部且已公开、已发布、未禁用），任意登录用户可访问；支持名称/描述关键字与分类过滤；收藏的应用置顶 */
export async function getPublicApps(filters?: { keywords?: string; classifyId?: number }): Promise<AppItem[]> {
  const client = getApiClient()
  const res = await client.api.app.public.list.get({
    queryParameters: {
      keywords: filters?.keywords?.trim() || undefined,
      classifyId: filters?.classifyId && filters.classifyId > 0 ? filters.classifyId : undefined,
    },
  })
  return (res?.items ?? []) as AppItem[]
}

/** 收藏应用（仅限应用广场可见的应用），重复收藏幂等返回成功；收藏后广场列表置顶 */
export async function favoriteApp(appId: string): Promise<void> {
  const client = getApiClient()
  await client.api.app.byId(appId).favorite.post()
}

/** 取消收藏应用，未收藏时幂等返回成功；应用下架/停用后仍可取消收藏 */
export async function unfavoriteApp(appId: string): Promise<void> {
  const client = getApiClient()
  await client.api.app.byId(appId).favorite.delete()
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
  /** 分类 id，0=未分类 */
  classifyId?: number
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
    classifyId: payload.classifyId ?? 0,
    isExternal: payload.isExternal,
    isAuth: payload.isAuth,
  })
  return String(res?.value ?? '')
}

/**
 * 基础信息更新：名称、描述、分类、授权开关（应用类型创建后不可修改；头像走独立接口）。
 * 版本号不随基础信息修改，只能在发布或申请上架时设置。
 * 公开（is_public）只能通过「上架审核」由系统管理员审批后设置，此处不再提供。
 */
export async function updateApp(
  appId: string,
  payload: { name: string; description?: string; isExternal?: boolean; isAuth?: boolean; classifyId?: number },
): Promise<void> {
  const client = getApiClient()
  await client.api.app.byId(appId).put({
    name: payload.name,
    description: payload.description,
    isExternal: payload.isExternal,
    isAuth: payload.isAuth,
    classifyId: payload.classifyId ?? 0,
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

/** 沙箱资源上限（每个应用可配置的存活时间 / CPU / 内存最大值与出站网络管控模式，由超级管理员在系统设置调整） */
export interface SandboxLimits {
  maxTtlSeconds: number
  maxCpu: string
  maxMemory: string
  /** 出站网络管控模式：app=应用自定义 / deny=强制默认拒绝 / intranet=默认拒绝并放行内网网段 */
  networkMode: string
  /** 内网网段（IP / CIDR，逗号分隔原文），intranet 模式下运行时自动放行 */
  intranetCidrs: string
}

/** 查询沙箱资源上限；接口异常时回退内置默认值（与后端 SettingDefinitions 一致） */
export async function getSandboxLimits(): Promise<SandboxLimits> {
  const client = getApiClient()
  const res = await client.api.app.sandboxLimits.get()
  return {
    maxTtlSeconds: res?.maxTtlSeconds ?? 86400,
    maxCpu: res?.maxCpu || '4',
    maxMemory: res?.maxMemory || '8Gi',
    networkMode: res?.networkMode || 'app',
    intranetCidrs: res?.intranetCidrs || '10.0.0.0/8,172.16.0.0/12,192.168.0.0/16',
  }
}

export interface AppAgentConfig {
  appId?: string | null
  teamId?: string | number | null
  appType?: AppKind | null
  /** 系统提示词，未配置时为空串 */
  prompt?: string | null
  /** 默认对话模型 id（uuid）；模型选择未开放时为空 Guid */
  modelId?: string | null
  /** 开放给用户选择的对话模型 id 列表（含默认模型，uuid）；未配置时仅含默认模型 */
  modelIds?: string[] | null
  /** 允许使用的知识库 id 列表（元素为 wiki.id） */
  wikiIds?: number[] | null
  /** 允许检索的知识图谱 id 列表（元素为 kg.id，仅托管图） */
  graphIds?: number[] | null
  /** 绑定的团队智能体 id 列表（元素为 team_agent.id，uuid，本团队启用中智能体） */
  teamAgentIds?: string[] | null
  /** 默认团队智能体 id（uuid）；空=取列表第一个 */
  defaultTeamAgentId?: string | null
  /** 绑定的团队智能体选项（按绑定顺序，含实时启用状态与弱依赖声明） */
  teamAgents?: Array<{ id?: string | null; key?: string | null; name?: string | null; description?: string | null; avatarObjectKey?: string | null; enabled?: boolean | null; mainPluginIds?: string[] | null; mainWikiIds?: Array<string | number> | null; mainGraphIds?: Array<string | number> | null }> | null
  /** 允许使用的插件 id 列表（元素为 plugin.id，uuid 字符串） */
  plugins?: string[] | null
  /** 绑定为工具的流程应用 id 列表（元素为 app.id，uuid 字符串，本团队已发布流程应用） */
  workflowApps?: string[] | null
  /** 绑定为工具的 Agent 应用 id 列表（元素为 app.id，uuid 字符串，本团队已发布内部 Agent 应用且不含自身） */
  agentApps?: string[] | null
  /** 应用默认使用的技能 id 列表（元素为 skill.id，uuid 字符串），用户可在应用设置中取消勾选 */
  skills?: string[] | null
  /** 开放给用户选择的可选专家提示词 id 列表（元素为 prompt.id，整数；市场公开或本团队提示词） */
  prompts?: number[] | null
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
    modelIds: (res?.modelIds ?? []).map((id) => String(id)),
    // Kiota 把后端 long 生成为 string，前端统一收敛为 number 便于与 wikiId 比较
    wikiIds: (res?.wikiIds ?? []).map((id) => Number(id)),
    graphIds: (res?.graphIds ?? []).map((id) => Number(id)),
    teamAgentIds: (res?.teamAgentIds ?? []).map((id) => String(id)),
    defaultTeamAgentId: res?.defaultTeamAgentId ?? null,
    teamAgents: (res?.teamAgents ?? []).map((item) => ({
      id: String(item?.id ?? ''),
      key: item?.key ?? '',
      name: item?.name ?? '',
      description: item?.description ?? '',
      avatarObjectKey: item?.avatarObjectKey ?? '',
      enabled: item?.enabled !== false,
      // 弱依赖声明（Kiota 把 long 生成为 string，统一收敛为 number 便于比较）
      mainPluginIds: (item?.mainPluginIds ?? []).map((id) => String(id)),
      mainWikiIds: (item?.mainWikiIds ?? []).map((id) => Number(id)),
      mainGraphIds: (item?.mainGraphIds ?? []).map((id) => Number(id)),
    })),
    plugins: (res?.plugins ?? []).map((id) => String(id)),
    workflowApps: (res?.workflowApps ?? []).map((id) => String(id)),
    agentApps: (res?.agentApps ?? []).map((id) => String(id)),
    skills: (res?.skills ?? []).map((id) => String(id)),
    prompts: (res?.prompts ?? []).map((id) => Number(id)).filter((id) => Number.isFinite(id) && id > 0),
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
 * `modelId` 传 null / 空串表示不选择模型；`modelIds` 为开放给用户选择的可选模型列表（须包含默认模型，
 * 后端校验逐个团队可用性与上限），null / 空时后端归一为仅默认模型。
 */
export async function saveAppAgentConfig(
  appId: string,
  payload: {
    modelId?: string | null
    /** 开放给用户选择的可选模型列表（含默认模型）；缺省表示仅默认模型 */
    modelIds?: string[] | null
    prompt: string
    wikiIds: number[]
    /** 允许检索的知识图谱 id 列表（仅托管图，后端校验归属与模式） */
    graphIds?: number[]
    /** 绑定的团队智能体 id 列表（本团队启用中智能体）；绑定独立于子代理协作，主智能体提示词始终生效 */
    teamAgentIds?: string[] | null
    /** 默认团队智能体 id，须在 teamAgentIds 内；null=取列表第一个 */
    defaultTeamAgentId?: string | null
    plugins: string[]
    /** 绑定为工具的流程应用 id 列表；null / 缺省表示保持已保存的流程应用绑定不变 */
    workflowApps?: string[] | null
    /** 绑定为工具的 Agent 应用 id 列表；null / 缺省表示保持已保存的 Agent 应用绑定不变 */
    agentApps?: string[] | null
    skills?: string[] | null
    /** 绑定的可选专家提示词 id 列表（市场公开或本团队提示词）；null / 缺省表示保持已保存绑定不变 */
    prompts?: number[] | null
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
    modelIds: ((payload.modelIds ?? []).filter(Boolean)) as Guid[],
    prompt: payload.prompt,
    // 后端 wiki_ids 为 long，Kiota 生成的请求体为 string[]，此处按生成类型传字符串
    wikiIds: payload.wikiIds.map((id) => String(id)),
    // 后端 graph_ids 为 long（仅托管图），同样按生成类型传字符串
    graphIds: (payload.graphIds ?? []).map((id) => String(id)),
    teamAgentIds: ((payload.teamAgentIds ?? []).filter(Boolean)) as Guid[],
    defaultTeamAgentId: (payload.defaultTeamAgentId || null) as Guid | null,
    plugins: payload.plugins as Guid[],
    workflowApps: (payload.workflowApps ?? null) as Guid[] | null,
    agentApps: (payload.agentApps ?? null) as Guid[] | null,
    skills: (payload.skills ?? null) as Guid[] | null,
    prompts: (payload.prompts ?? null) as number[] | null,
    openingStatement: payload.openingStatement ?? '',
    openingStatementEnabled: payload.openingStatementEnabled ?? false,
    quickInputs: payload.quickInputs ?? null,
    executionSettings: payload.executionSettings ? toUntypedNode(payload.executionSettings) : null,
  })
}

// ==================== Agent 压缩默认参数 ====================

/** Agent 上下文压缩生效默认参数（系统设置，应用配置页预填值与自动模式换算展示） */
export interface AgentCompactionDefaults {
  /** 默认压缩策略（auto=按模型上下文窗口比例推导） */
  compactionMode: string
  /** 自动模式：工具结果折叠触发比例（占 input budget） */
  toolEvictionRatio: number
  /** 自动模式：LLM 摘要压缩触发比例（占 input budget） */
  summarizeTriggerRatio: number
  /** 自动模式：摘要压缩目标比例（压回该水位） */
  summarizeTargetRatio: number
  /** 自动模式：紧急截断触发比例（占 input budget） */
  truncationRatio: number
  /** 自定义模式：工具结果折叠触发消息数默认值 */
  toolResultTriggerMessages: number
  /** 自定义模式：摘要触发 token 默认值 */
  summarizeTriggerTokens: number
  /** 自定义模式：紧急截断 token 上限默认值 */
  hardTokenLimit: number
  enableSummarization: boolean
  summarizerModelId: string
}

/** 查询 Agent 上下文压缩生效默认参数（登录用户可用） */
export async function getCompactionDefaults(): Promise<AgentCompactionDefaults> {
  const client = getApiClient()
  const res = await client.api.app.agent.compactionDefaults.get()
  const raw = res as unknown as Partial<AgentCompactionDefaults> | undefined
  return {
    compactionMode: raw?.compactionMode ?? 'auto',
    toolEvictionRatio: raw?.toolEvictionRatio ?? 0.5,
    summarizeTriggerRatio: raw?.summarizeTriggerRatio ?? 0.8,
    summarizeTargetRatio: raw?.summarizeTargetRatio ?? 0.5,
    truncationRatio: raw?.truncationRatio ?? 0.95,
    toolResultTriggerMessages: raw?.toolResultTriggerMessages ?? 16,
    summarizeTriggerTokens: raw?.summarizeTriggerTokens ?? 16000,
    hardTokenLimit: raw?.hardTokenLimit ?? 64000,
    enableSummarization: raw?.enableSummarization === true,
    summarizerModelId: raw?.summarizerModelId ?? '',
  }
}

/** 长期记忆默认提示词与平台记忆设置回显（应用未配置 memory.prompt 时生效的平台默认值） */
export interface MemoryDefaults {
  prompt: string
  /** 平台是否已开启长期记忆（总开关关闭时应用不可开启记忆） */
  platformEnabled: boolean
  /** 平台是否已开启跨应用记忆（关闭时应用不可开启用户跨应用共享记忆） */
  globalEnabled: boolean
  /** 平台统一向量化模型名称（应用记忆与全局记忆共用；未配置为空） */
  embeddingModelName: string
  /** 平台统一向量维度 */
  embeddingDimensions: number
}

/** 查询长期记忆默认提示词与平台记忆设置（登录用户可用，配置页占位与只读回显） */
export async function getMemoryDefaults(): Promise<MemoryDefaults> {
  const client = getApiClient()
  const res = await client.api.app.agent.memoryDefaults.get()
  const raw = res as unknown as Partial<MemoryDefaults> | undefined
  return {
    prompt: raw?.prompt ?? '',
    platformEnabled: raw?.platformEnabled === true,
    globalEnabled: raw?.globalEnabled === true,
    embeddingModelName: raw?.embeddingModelName ?? '',
    embeddingDimensions: raw?.embeddingDimensions ?? 1024,
  }
}

// ==================== 应用发布 ====================

/** 发布应用（发布后团队成员可进入对话），仅团队 Admin+ 且仅 Agent 应用；version（aaa.bb.cc）可选，携带时把应用版本更新为该值并随发布生效 */
export async function publishApp(appId: string, version?: string): Promise<void> {
  const client = getApiClient()
  await client.api.app.byId(appId).publish.post({ version })
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

/** 会话上下文用量：累计 token 与生效对话模型的上下文窗口上限（contextWindow 未知为 0） */
export interface AppSessionContextUsage {
  sessionId: string
  modelName: string
  contextWindow: number
  inputTokens: number
  outTokens: number
  totalTokens: number
  /** 缓存命中的输入 token（模型响应 usage 上报，未上报为 0） */
  cachedTokens: number
  /** 当前上下文占用（最近一次模型调用的输入 token；上下文压缩后随下一轮回落） */
  contextTokens: number
}

/** 查询会话上下文用量（热态优先、落库兜底），用于对话输入区的上下文占用环形指示 */
export async function getAppSessionContextUsage(sessionId: string): Promise<AppSessionContextUsage> {
  const client = getApiClient()
  const res = await client.api.app.session.bySessionId(sessionId).contextUsage.get()
  return {
    sessionId: String(res?.sessionId ?? sessionId),
    modelName: res?.modelName ?? '',
    contextWindow: toFiniteNumber(res?.contextWindow),
    inputTokens: toFiniteNumber(res?.inputTokens),
    outTokens: toFiniteNumber(res?.outTokens),
    totalTokens: toFiniteNumber(res?.totalTokens),
    cachedTokens: toFiniteNumber(res?.cachedTokens),
    contextTokens: toFiniteNumber(res?.contextTokens),
  }
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
  /** 会话归属用户的登录用户名（仅内部用户；createUserName 为昵称，两者并列展示） */
  ownerUserName?: string | null
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
      ownerUserName: item.ownerUserName ?? null,
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

/** 归一化 Kiota DateOnly（{year,month,day} 对象或 ISO 字符串）为 YYYY-MM-DD 文本 */
const toDateText = (value: unknown): string => {
  if (typeof value === 'string') {
    return value
  }
  const d = value as { year?: number; month?: number; day?: number } | null | undefined
  if (d && typeof d.year === 'number' && typeof d.month === 'number' && typeof d.day === 'number') {
    return `${d.year}-${String(d.month).padStart(2, '0')}-${String(d.day).padStart(2, '0')}`
  }
  return ''
}

export interface AppUsageSummary {
  callCount: number
  promptTokens: number
  completionTokens: number
  totalTokens: number
  /** 缓存命中的输入 token（模型响应 usage 上报，未上报为 0） */
  cachedTokens: number
}

export interface AppUsageModelItem {
  modelId?: string | null
  modelName?: string | null
  callCount: number
  promptTokens: number
  completionTokens: number
  totalTokens: number
  /** 缓存命中的输入 token（模型响应 usage 上报，未上报为 0） */
  cachedTokens: number
  /** 是否当前生效配置的默认对话模型（已发布取快照，否则草稿） */
  isDefault?: boolean
  /** 是否在当前生效配置的可选模型列表中（配置但未调用的模型以零用量项补全） */
  isConfigured?: boolean
}

/** 应用使用活动统计（会话/用户/消息维度） */
export interface AppUsageActivityStats {
  /** 累计使用次数（会话创建计数，删除会话不减少） */
  useCount: number
  sessionCount: number
  userCount: number
  messageCount: number
  lastActiveTime?: string | null
}

export interface AppUsageResult {
  summary: AppUsageSummary
  byModel: AppUsageModelItem[]
  stats: AppUsageActivityStats
}

/** 查询应用用量统计（汇总 + 按模型分布）；后端 long 以字符串下发，此处归一化为 number */
export async function getAppUsage(appId: string): Promise<AppUsageResult> {
  const client = getApiClient()
  const res = await client.api.app.byId(appId).usage.get()
  const summary = res?.summary
  const stats = res?.stats
  return {
    stats: {
      useCount: toFiniteNumber(stats?.useCount),
      sessionCount: toFiniteNumber(stats?.sessionCount),
      userCount: toFiniteNumber(stats?.userCount),
      messageCount: toFiniteNumber(stats?.messageCount),
      lastActiveTime: stats?.lastActiveTime ?? null,
    },
    summary: {
      callCount: toFiniteNumber(summary?.callCount),
      promptTokens: toFiniteNumber(summary?.promptTokens),
      completionTokens: toFiniteNumber(summary?.completionTokens),
      totalTokens: toFiniteNumber(summary?.totalTokens),
      cachedTokens: toFiniteNumber(summary?.cachedTokens),
    },
    byModel: (res?.byModel ?? []).map((item) => ({
      modelId: item.modelId,
      modelName: item.modelName,
      callCount: toFiniteNumber(item.callCount),
      promptTokens: toFiniteNumber(item.promptTokens),
      completionTokens: toFiniteNumber(item.completionTokens),
      totalTokens: toFiniteNumber(item.totalTokens),
      cachedTokens: toFiniteNumber(item.cachedTokens),
      isDefault: item.isDefault ?? false,
      isConfigured: item.isConfigured ?? false,
    })),
  }
}

/** 应用使用趋势单日数据 */
export interface AppUsageTrendItem {
  /** 统计日期（YYYY-MM-DD） */
  date?: string | null
  /** 当日使用次数（会话创建计数） */
  useCount: number
  /** 当日消息条数 */
  messageCount: number
}

/** 各模型 Token 消耗趋势单点 */
export interface AppUsageModelTokenPoint {
  /** 统计日期（YYYY-MM-DD） */
  date?: string | null
  /** 当日合计 token */
  tokens: number
}

/** 单模型 Token 消耗趋势序列（覆盖完整区间，无数据日补 0） */
export interface AppUsageModelTokenSeries {
  modelId?: string | null
  modelName?: string | null
  points: AppUsageModelTokenPoint[]
}

/** 应用使用趋势（按日聚合）结果 */
export interface AppUsageTrendResult {
  items: AppUsageTrendItem[]
  /** 各模型 Token 序列（按区间合计降序，最多 8 条；无数据为空） */
  modelTokenSeries: AppUsageModelTokenSeries[]
  startDate?: string | null
  endDate?: string | null
}

/** 查询应用使用趋势（近 N 天使用次数/消息条数按日聚合 + 各模型 token 序列）；后端 long 以字符串下发，此处归一化为 number */
export async function getAppUsageTrend(appId: string, days = 30): Promise<AppUsageTrendResult> {
  const client = getApiClient()
  const res = await client.api.app.byId(appId).usage.trend.get({ queryParameters: { days } })
  return {
    items: (res?.items ?? []).map((item) => ({
      date: toDateText(item.date),
      useCount: toFiniteNumber(item.useCount),
      messageCount: toFiniteNumber(item.messageCount),
    })),
    modelTokenSeries: (res?.modelTokenSeries ?? []).map((series) => ({
      modelId: series.modelId,
      modelName: series.modelName,
      points: (series.points ?? []).map((point) => ({
        date: toDateText(point.date),
        tokens: toFiniteNumber(point.tokens),
      })),
    })),
    startDate: toDateText(res?.startDate),
    endDate: toDateText(res?.endDate),
  }
}

// ==================== 用户级应用配置 ====================

/** 应用设置中展示的可选技能项（候选池=应用默认 ∪ 市场公开 ∪ 本人个人） */
export interface AppUserSkillOption {
  /** 技能 id（uuid 字符串） */
  id?: string | null
  key?: string | null
  name?: string | null
  description?: string | null
  isSystem?: boolean | null
  /** 所属团队 id，0=系统内置/市场公开/个人技能 */
  teamId?: number | null
  /** 是否已上架市场公开 */
  isPublic?: boolean | null
  /** 使用次数（技能装载工具被模型调用时自增），热门排序依据 */
  useCount?: number | null
}

/** 应用设置中展示的可选专家（提示词）项（候选池=应用绑定 ∪ 市场公开 ∪ 本人个人，不含团队） */
export interface AppUserPromptOption {
  /** 提示词 id */
  promptId?: number | null
  name?: string | null
  description?: string | null
  /** 头像存储路径（objectKey，空串=未设置） */
  avatarPath?: string | null
  /** 所属团队 id，0=个人提示词 */
  teamId?: number | null
  /** 是否已上架市场公开 */
  isPublic?: boolean | null
  /** 被选用次数，热门排序依据 */
  useCount?: number | null
}

/** 应用绑定的团队智能体切换器选项（对话端，多选时展示切换下拉） */
export interface AppUserTeamAgentOption {
  /** 团队智能体 id（uuid 字符串） */
  id: string
  /** 智能体标识 key（团队内唯一，如 plan/build） */
  key?: string | null
  /** 智能体名称（展示名） */
  name: string
  /** 智能体描述 */
  description?: string | null
  /** 头像文件 objectKey，空串=未设置（经 resolveStorageUrl 转可访问地址） */
  avatarObjectKey?: string | null
}

export interface AppUserConfig {
  /** 用户当前选择的专家提示词 id，0=未选择 */
  promptId: number
  /** 默认对话模型 id（uuid，与正式对话同一生效口径）；未配置模型时为空串 */
  defaultModelId: string
  /** 开放给用户选择的对话模型 id 列表（含默认模型）；仅默认模型或未配置时为单元素/空列表 */
  modelIds: string[]
  /** 应用绑定的团队智能体切换器选项（与正式对话同一生效口径）；仅 1 个或为空时前端不展示切换器 */
  teamAgents: AppUserTeamAgentOption[]
  /** 默认团队智能体 id（uuid）；空=取列表第一个 */
  defaultTeamAgentId: string
  /** 用户当前勾选的技能 id 列表（可选技能池的子集；未配置过时为应用默认技能全集） */
  skills: string[]
  /** 应用默认技能目录（管理员绑定，按配置顺序），未保存过用户配置时默认全部勾选 */
  defaultSkills: AppUserSkillOption[]
  /** 可选技能候选池 = 应用默认 ∪ 市场公开 ∪ 本人个人（排除禁用；默认在前，按 默认→市场→个人 排序去重） */
  skillOptions: AppUserSkillOption[]
  /** 可选专家候选池 = 应用绑定 ∪ 市场公开 ∪ 本人个人（不含团队提示词；按 绑定→市场→个人 排序去重） */
  promptOptions: AppUserPromptOption[]
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
  /** 用户长期记忆模式：0=跟随应用默认；1=对我关闭；2=仅本应用内；3=跨应用 */
  memoryMode: number
  /** 应用是否开启长期记忆（未开启时前端隐藏记忆偏好区） */
  memoryEnabled: boolean
  /** 用户全局记忆接入模式：0=默认（应用允许时接入）；1=不接入全局记忆 */
  memoryGlobalMode: number
  /** 应用是否允许用户接入全局记忆（false 时前端不展示全局接入开关） */
  memoryGlobalAllowed: boolean
}

/** userconfig.get 响应的原始 JSON 形状（新契约字段经原始 JSON 投射读取，regen 后行为一致） */
interface RawAppUserConfigResponse {
  promptId?: number | null
  defaultModelId?: string | null
  modelIds?: string[] | null
  teamAgents?: Array<{ id?: string | null; key?: string | null; name?: string | null; description?: string | null; avatarObjectKey?: string | null }> | null
  defaultTeamAgentId?: string | null
  skills?: string[] | null
  defaultSkills?: AppUserSkillOption[] | null
  skillOptions?: AppUserSkillOption[] | null
  promptOptions?: AppUserPromptOption[] | null
  toolApprovalMode?: string | null
  toolApprovalExemptNames?: string[] | null
  toolApprovalExemptPrefixes?: string[] | null
  toolApprovalAutoApprovedNames?: string[] | null
  toolApprovalAutoApprovedPrefixes?: string[] | null
  memoryMode?: number | null
  memoryEnabled?: boolean | null
  memoryGlobalMode?: number | null
  memoryGlobalAllowed?: boolean | null
}

/**
 * 查询当前用户在某应用下的个性化配置与应用默认技能目录（含工具审批模式与审批卡豁免清单）。
 */
export async function getAppUserConfig(appId: string): Promise<AppUserConfig> {
  const client = getApiClient()
  const res = (await client.api.app.byId(appId).userconfig.get()) as unknown as RawAppUserConfigResponse
  return {
    promptId: res?.promptId ?? 0,
    defaultModelId: res?.defaultModelId ? String(res.defaultModelId) : '',
    // 存量应用未配置多模型时后端已归一为 [默认模型]；空 Guid 视为未配置
    modelIds: (res?.modelIds ?? []).map((id) => String(id)).filter((id) => id && id !== EMPTY_GUID),
    teamAgents: (res?.teamAgents ?? [])
      .filter((item) => item?.id && item.name)
      .map((item) => ({
        id: String(item.id),
        key: item.key ?? '',
        name: String(item.name),
        description: item.description ?? '',
        avatarObjectKey: item.avatarObjectKey ?? '',
      })),
    defaultTeamAgentId: res?.defaultTeamAgentId ? String(res.defaultTeamAgentId) : '',
    skills: (res?.skills ?? []).map((id) => String(id)),
    defaultSkills: res?.defaultSkills ?? [],
    skillOptions: res?.skillOptions ?? [],
    promptOptions: res?.promptOptions ?? [],
    toolApprovalMode: res?.toolApprovalMode === 'approval' ? 'approval' : 'auto',
    toolApprovalExemptNames: res?.toolApprovalExemptNames ?? [],
    toolApprovalExemptPrefixes: res?.toolApprovalExemptPrefixes ?? [],
    toolApprovalAutoApprovedNames: res?.toolApprovalAutoApprovedNames ?? [],
    toolApprovalAutoApprovedPrefixes: res?.toolApprovalAutoApprovedPrefixes ?? [],
    memoryMode: res?.memoryMode ?? 0,
    memoryEnabled: res?.memoryEnabled === true,
    memoryGlobalMode: res?.memoryGlobalMode ?? 0,
    memoryGlobalAllowed: res?.memoryGlobalAllowed === true,
  }
}

/** 保存当前用户在某应用下的个性化配置，跨会话复用；技能仅能在可选池（应用默认 ∪ 市场公开 ∪ 本人个人）内勾选 */
export async function saveAppUserConfig(
  appId: string,
  payload: { promptId: number; skills: string[]; toolApprovalMode?: 'auto' | 'approval'; memoryMode?: number; memoryGlobalMode?: number },
): Promise<void> {
  const client = getApiClient()
  await client.api.app.byId(appId).userconfig.put({
    promptId: payload.promptId,
    skills: payload.skills as Guid[],
    toolApprovalMode: payload.toolApprovalMode,
    memoryMode: payload.memoryMode,
    memoryGlobalMode: payload.memoryGlobalMode,
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

/** 脱敏规则类型代码（对齐后端 AppSecurityRuleTypes） */
export const SECURITY_RULE_TYPES = ['phone', 'idCard', 'email', 'bankCard', 'injection', 'custom'] as const
export type SecurityRuleType = (typeof SECURITY_RULE_TYPES)[number]

/** 规则处理策略代码（对齐后端 AppSecurityStrategies） */
export const SECURITY_STRATEGIES = ['redact', 'mask', 'hash', 'block', 'flag'] as const
export type SecurityStrategy = (typeof SECURITY_STRATEGIES)[number]

/** 应用安全脱敏规则 */
export interface AppSecurityRuleItem {
  name: string
  type: SecurityRuleType
  pattern?: string | null
  /** 命中后替换文本，仅 redact 策略生效 */
  replacement?: string | null
  /** 处理策略，存量数据缺省 redact */
  strategy: SecurityStrategy
  /** mask 策略保留末位（custom/银行卡），0~18 */
  keepLast?: number | null
}

/** 应用安全配置（内容脱敏） */
export interface AppSecurityConfig {
  appId: string
  enabled: boolean
  maskToolResult: boolean
  maskToolArgs: boolean
  maskModelOutput: boolean
  /** 是否处理用户输入文本（内容脱敏总开关开启时生效） */
  maskInput: boolean
  /** 内容脱敏规则（用户输入/工具调用结果/工具调用参数范围共用） */
  rules: AppSecurityRuleItem[]
  /** 模型回复专属脱敏规则，与 rules 相互独立维护 */
  modelOutputRules: AppSecurityRuleItem[]
  myRole: number | null
}

/** 应用安全事件（规则命中审计聚合行） */
export interface AppSecurityEventItem {
  id: string
  sessionId: string | null
  /** 后端 long 按 long→string 约定序列化 */
  userId: string | null
  scope: string
  ruleName: string
  ruleType: string
  strategy: string
  action: string
  hitCount: number
  createTime: string
}

/** 规则响应归一化：非法类型代码兜底为 custom、非法策略兜底为 redact，空值补默认 */
function normalizeRule(r: {
  name?: string | null
  type?: string | null
  pattern?: string | null
  replacement?: string | null
  strategy?: string | null
  keepLast?: number | null
}): AppSecurityRuleItem {
  return {
    name: r?.name ?? '',
    type: (SECURITY_RULE_TYPES as readonly string[]).includes(r?.type ?? '') ? (r?.type as SecurityRuleType) : 'custom',
    pattern: r?.pattern ?? null,
    replacement: r?.replacement ?? null,
    strategy: (SECURITY_STRATEGIES as readonly string[]).includes(r?.strategy ?? '') ? (r?.strategy as SecurityStrategy) : 'redact',
    keepLast: r?.keepLast ?? null,
  }
}

/**
 * 查询应用安全配置（内容脱敏）；未保存过配置时返回默认值（关闭 + 空规则）。
 */
export async function getAppSecurity(appId: string): Promise<AppSecurityConfig> {
  const client = getApiClient()
  const res = await client.api.app.byId(appId).security.get()
  return {
    appId: res?.appId ?? appId,
    enabled: res?.enabled ?? false,
    maskToolResult: res?.maskToolResult ?? true,
    maskToolArgs: res?.maskToolArgs ?? false,
    maskModelOutput: res?.maskModelOutput ?? false,
    maskInput: res?.maskInput ?? false,
    rules: (res?.rules ?? []).map(normalizeRule),
    modelOutputRules: (res?.modelOutputRules ?? []).map(normalizeRule),
    myRole: res?.myRole ?? null,
  }
}

/**
 * 分页查询应用安全事件（规则命中审计），需要团队管理员及以上角色。
 */
export async function getAppSecurityEvents(
  appId: string,
  pageNo = 1,
  pageSize = 20,
  filters?: { keyword?: string; scope?: string },
): Promise<{ items: AppSecurityEventItem[]; total: number; pageNo: number; pageSize: number }> {
  const client = getApiClient()
  const res = await client.api.app.byId(appId).security.events.get({
    queryParameters: {
      pageNo,
      pageSize,
      keyword: filters?.keyword || undefined,
      scope: filters?.scope || undefined,
    },
  })
  return {
    items: (res?.items ?? []).map((e) => ({
      id: e?.id ?? '',
      sessionId: e?.sessionId ?? null,
      userId: e?.userId ?? null,
      scope: e?.scope ?? '',
      ruleName: e?.ruleName ?? '',
      ruleType: e?.ruleType ?? '',
      strategy: e?.strategy ?? '',
      action: e?.action ?? '',
      hitCount: e?.hitCount ?? 0,
      createTime: e?.createTime ?? '',
    })),
    total: res?.total ?? 0,
    pageNo: res?.pageNo ?? pageNo,
    pageSize: res?.pageSize ?? pageSize,
  }
}

/**
 * 保存应用安全配置（内容脱敏），需要团队管理员及以上角色；自定义规则须携带可编译正则、
 * 模型回复规则不支持 block 策略（后端校验）。
 */
export async function saveAppSecurity(
  appId: string,
  payload: {
    enabled: boolean
    maskToolResult: boolean
    maskToolArgs: boolean
    maskModelOutput: boolean
    maskInput: boolean
    rules: AppSecurityRuleItem[]
    modelOutputRules: AppSecurityRuleItem[]
  },
): Promise<void> {
  const client = getApiClient()
  const serializeRules = (rules: AppSecurityRuleItem[]) =>
    rules.map((r) => ({
      name: r.name,
      type: r.type,
      pattern: r.type === 'custom' ? r.pattern ?? '' : null,
      replacement: r.strategy === 'redact' ? r.replacement ?? null : null,
      strategy: r.strategy,
      keepLast: r.strategy === 'mask' ? r.keepLast ?? null : null,
    }))
  await client.api.app.byId(appId).security.put({
    enabled: payload.enabled,
    maskToolResult: payload.maskToolResult,
    maskToolArgs: payload.maskToolArgs,
    maskModelOutput: payload.maskModelOutput,
    maskInput: payload.maskInput,
    rules: serializeRules(payload.rules),
    modelOutputRules: serializeRules(payload.modelOutputRules),
  })
}
