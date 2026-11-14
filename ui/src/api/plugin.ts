import { createAiPluginRequestAdapter, getAiPluginClient, getApiClient } from '@/api/kiota'
import type {
  QueryPluginTeamAuthorizationCommandResponse,
  UpdatePluginTeamAuthorizationCommand,
} from '@/api/client/models'
import type {
  DeleteCustomPluginCommand,
  DeleteDynamicPluginCommand,
  EmptyCommandResponse,
  ImportMcpServerPluginCommand,
  ImportOpenApiPluginCommand,
  KeyValueBool,
  KeyValueString,
  PluginBaseInfoItem,
  PluginFunctionItem,
  PluginRunResult,
  PluginType,
  PreUploadOpenApiFilePluginCommand,
  PreUploadOpenApiFilePluginCommandResponse,
  QueryCustomPluginBaseListCommandResponse,
  QueryCustomPluginDetailCommand,
  QueryCustomPluginDetailCommandResponse,
  QueryCustomPluginFunctionsListCommand,
  QueryCustomPluginFunctionsListCommandResponse,
  QueryCustomPluginListCommand,
  QueryPluginListCommandResponseItem,
  QueryPluginManageListCommandResponseItem,
  RefreshMcpServerPluginCommand,
  RunPluginCommand,
  SaveDynamicPluginCommand,
  SaveStaticPluginCommand,
  UpdateMcpServerPluginCommand,
  UpdateOpenApiPluginCommand,
} from '@/api/aiplugin-client/models'
import { Env } from '@/config/env'
import { HttpMethod, RequestInformation } from '@microsoft/kiota-abstractions'
/** 插件管理项（Kiota 生成类型别名）. */
export type PluginManageItem = QueryPluginManageListCommandResponseItem

/** 静态插件管理项（含 pluginKey/paramsExample）—— 供抽屉与编辑写回使用. */
export type StaticPluginManageItem = PluginManageItem & {
  pluginKey?: string | null
  paramsExample?: string | null
}

/** 动态插件实例管理项（含 templeteKey/config/configExample/paramsExample）. */
export type DynamicPluginManageItem = PluginManageItem & {
  pluginKey?: string | null
  templeteKey?: string | null
  config?: string | null
  configExample?: string | null
  paramsExample?: string | null
}

/** 动态插件模板（注册表发现项）—— 供新建实例下拉选择. */
export type DynamicPluginTemplate = QueryPluginListCommandResponseItem

/** 插件种类：custom|dynamic|static. */
export type PluginKind = 'custom' | 'dynamic' | 'static'

/** 插件统计 TOP 项（管理端团队自有插件带 teamName；团队端无团队字段）. */
export interface PluginStatsTopItem {
  id: string
  name: string
  teamId?: number | null
  teamName?: string | null
  counter: number
  avatarPath?: string | null
}

/** 插件统计分类分布项（classifyId=0 表示未分类，classifyName 为空）. */
export interface PluginStatsClassifyItem {
  classifyId: number
  classifyName?: string | null
  count: number
}

/** 插件统计趋势点. */
export interface PluginStatsTrendPoint {
  date: string
  count: number
}

/** 插件统计趋势（近 30 天，无数据日期补 0）. */
export interface PluginStatsTrend {
  items: PluginStatsTrendPoint[]
  total: number
  startDate: string
  endDate: string
}

/** 插件使用统计（管理端=全团队汇总；团队端=仅本团队自有插件）. */
export interface PluginStatsData {
  totalPlugins: number
  mcpCount: number
  openApiCount: number
  totalCalls: number
  totalFunctions: number
  top: PluginStatsTopItem[]
  classifies: PluginStatsClassifyItem[]
  trend: PluginStatsTrend
}

/** Kiota 统计响应原始形态（long 生成物可能为 string、date 为 Kiota DateOnly，统一 unknown 归一）. */
export interface PluginStatsRaw {
  totalPlugins?: unknown
  mcpCount?: unknown
  openApiCount?: unknown
  totalCalls?: unknown
  totalFunctions?: unknown
  top?:
    | {
        id?: unknown
        name?: unknown
        teamId?: unknown
        teamName?: unknown
        counter?: unknown
        avatarPath?: unknown
      }[]
    | null
  classifies?:
    | {
        classifyId?: unknown
        classifyName?: unknown
        count?: unknown
      }[]
    | null
  trend?: {
    items?: { date?: unknown; count?: unknown }[] | null
    total?: unknown
    startDate?: unknown
    endDate?: unknown
  } | null
}

/** 归一 Kiota 统计响应为数值形态. */
export function normalizePluginStats(res: PluginStatsRaw | undefined | null): PluginStatsData {
  const num = (v: unknown): number => {
    const n = Number(v ?? 0)
    return Number.isFinite(n) ? n : 0
  }
  return {
    totalPlugins: num(res?.totalPlugins),
    mcpCount: num(res?.mcpCount),
    openApiCount: num(res?.openApiCount),
    totalCalls: num(res?.totalCalls),
    totalFunctions: num(res?.totalFunctions),
    top: (res?.top ?? []).map((item) => ({
      id: String(item.id ?? ''),
      name: String(item.name ?? ''),
      teamId: item.teamId == null ? null : num(item.teamId),
      teamName: item.teamName == null ? null : String(item.teamName),
      counter: num(item.counter),
      avatarPath: item.avatarPath == null ? null : String(item.avatarPath),
    })),
    classifies: (res?.classifies ?? []).map((item) => ({
      classifyId: num(item.classifyId),
      classifyName: item.classifyName == null ? null : String(item.classifyName),
      count: num(item.count),
    })),
    trend: {
      items: (res?.trend?.items ?? []).map((item) => ({
        date: String(item.date ?? ''),
        count: num(item.count),
      })),
      total: num(res?.trend?.total),
      startDate: String(res?.trend?.startDate ?? ''),
      endDate: String(res?.trend?.endDate ?? ''),
    },
  }
}

/** 分类筛选值：全部（''）、未分类（'0'）、具体分类 id 字符串. */
export type ClassifyFilter = 'all' | 'uncategorized' | string

/** 自定义插件基础项（Kiota 生成类型别名，pluginId 为 Guid 字符串）. */
export type CustomPlugin = PluginBaseInfoItem

/** 自定义插件详情（Kiota 生成类型别名，serverUrl 为字符串）. */
export type CustomPluginDetail = QueryCustomPluginDetailCommandResponse

/** 自定义插件函数项. */
export type CustomPluginFunction = PluginFunctionItem

/** 键值对（Header/Query）. */
export type CustomKeyValue = KeyValueString

/** 插件类型（mcp|openApi）. */
export type CustomPluginType = PluginType

/** 私有系统插件团队授权项. */
export interface PluginTeamAuthorization {
  pluginId: string | null
  isPublic: boolean | null
  items: { teamId: number; teamName: string }[]
}

/** 表格排序状态：字段名 + 方向（ascend 升序 / descend 降序）. */
export interface CustomPluginSort {
  field: string | null
  order: 'ascend' | 'descend' | null
}

const emptyKeyValues = (): CustomKeyValue[] => []

/**
 * 将前端排序状态转成 Kiota 需要的 orderByFields 数组.
 */
function toOrderByFields(sort: CustomPluginSort): KeyValueBool[] | undefined {
  if (!sort.field || !sort.order) return undefined
  return [{ key: sort.field, value: sort.order === 'ascend' }]
}

async function getCustomPlugins(params: {
  name?: string
  type?: CustomPluginType
  classifyId?: number
  isPublic?: boolean
  sort?: CustomPluginSort
}): Promise<CustomPlugin[]> {
  const client = getAiPluginClient()
  const body: QueryCustomPluginListCommand = {
    name: params.name || undefined,
    type: params.type || undefined,
    classifyId: params.classifyId ?? undefined,
    isPublic: params.isPublic ?? undefined,
    orderByFields: toOrderByFields(params.sort ?? { field: null, order: null }),
  }
  const res: QueryCustomPluginBaseListCommandResponse | undefined =
    await client.api.ai.plugin.custom.plugin_list.post(body)
  return res?.items ?? []
}

async function getCustomPluginDetail(pluginId: string): Promise<CustomPluginDetail | null> {
  const client = getAiPluginClient()
  const body: QueryCustomPluginDetailCommand = { pluginId }
  return (await client.api.ai.plugin.custom.plugin_detail.post(body)) ?? null
}

async function getCustomPluginFunctions(pluginId: string): Promise<CustomPluginFunction[]> {
  const client = getAiPluginClient()
  const body: QueryCustomPluginFunctionsListCommand = { pluginId }
  const res: QueryCustomPluginFunctionsListCommandResponse | undefined =
    await client.api.ai.plugin.custom.function_list.post(body)
  return res?.items ?? []
}

async function importMcp(
  payload: Omit<ImportMcpServerPluginCommand, 'header' | 'query'> & {
    header?: CustomKeyValue[]
    query?: CustomKeyValue[]
  },
): Promise<string | null> {
  const client = getAiPluginClient()
  const body: ImportMcpServerPluginCommand = {
    ...payload,
    header: payload.header ?? emptyKeyValues(),
    query: payload.query ?? emptyKeyValues(),
  }
  const res = await client.api.ai.plugin.custom.import_mcp.post(body)
  return res?.value ?? null
}

async function updateMcp(payload: UpdateMcpServerPluginCommand): Promise<void> {
  const client = getAiPluginClient()
  await client.api.ai.plugin.custom.update_mcp.post({
    ...payload,
    header: payload.header ?? emptyKeyValues(),
    query: payload.query ?? emptyKeyValues(),
  })
}

async function importOpenApi(payload: ImportOpenApiPluginCommand): Promise<string | null> {
  const client = getAiPluginClient()
  const res = await client.api.ai.plugin.custom.import_openapi.post(payload)
  return res?.value ?? null
}

async function updateOpenApi(payload: UpdateOpenApiPluginCommand): Promise<void> {
  const client = getAiPluginClient()
  await client.api.ai.plugin.custom.update_openapi.post(payload)
}

async function refreshMcp(pluginId: string): Promise<void> {
  const client = getAiPluginClient()
  const body: RefreshMcpServerPluginCommand = { pluginId }
  await client.api.ai.plugin.custom.refresh_mcp.post(body)
}

async function deleteCustomPlugin(pluginId: string): Promise<void> {
  const client = getAiPluginClient()
  const body: DeleteCustomPluginCommand = { pluginId }
  await client.api.ai.plugin.custom.delete(body)
}

/** 预上传 OpenAPI 文件，返回（可能）已存在的 fileId 与签名 URL. */
async function preUploadOpenApiFile(
  payload: PreUploadOpenApiFilePluginCommand,
): Promise<PreUploadOpenApiFilePluginCommandResponse | null> {
  const client = getAiPluginClient()
  return (await client.api.ai.plugin.custom.pre_upload_openapi.post(payload)) ?? null
}

/** 查询私有系统插件的团队授权：公开插件返回 isPublic=true 且 items 为空. */
async function getPluginTeamAuthorization(pluginId: string): Promise<PluginTeamAuthorization | null> {
  const client = getApiClient()
  const res: QueryPluginTeamAuthorizationCommandResponse | undefined =
    await client.api.ai.plugin.byPluginId(pluginId).authorization.get()
  if (!res) return null
  return {
    pluginId: res.pluginId ?? null,
    isPublic: res.isPublic ?? null,
    items: (res.items ?? []).map((x) => ({ teamId: Number(x.teamId ?? 0), teamName: String(x.teamName ?? '') })),
  }
}

/** 更新私有系统插件的团队授权（全量替换），仅私有插件可设置. */
async function updatePluginTeamAuthorization(pluginId: string, teamIds: number[]): Promise<void> {
  const client = getApiClient()
  const body: UpdatePluginTeamAuthorizationCommand = { pluginId, teamIds }
  await client.api.ai.plugin.byPluginId(pluginId).authorization.put(body)
}

export const customPluginApi = {
  getCustomPlugins,
  getCustomPluginDetail,
  getCustomPluginFunctions,
  importMcp,
  updateMcp,
  importOpenApi,
  updateOpenApi,
  refreshMcp,
  deleteCustomPlugin,
  preUploadOpenApiFile,
}

export const pluginAuthorizationApi = {
  getPluginTeamAuthorization,
  updatePluginTeamAuthorization,
}

async function getManagePlugins(kind?: PluginKind): Promise<StaticPluginManageItem[]> {
  const client = getAiPluginClient()
  const res = await client.api.ai.plugin.manage.list.get({
    queryParameters: kind ? { kind } : undefined,
  })
  return (res?.items ?? []) as StaticPluginManageItem[]
}

/** 查询插件使用统计（全团队汇总：概览 + TOP（团队自有插件带团队名）+ 分类分布 + 近 30 天趋势），仅管理员. */
async function getPluginStats(): Promise<PluginStatsData> {
  const client = getAiPluginClient()
  const res = await client.api.ai.plugin.manage.stats.get()
  return normalizePluginStats(res)
}

/** 设置插件头像（管理员）：objectKey 为直传完成并登记的图片 key. */
async function updatePluginAvatar(pluginId: string, objectKey: string): Promise<void> {
  const client = getAiPluginClient()
  await client.api.ai.plugin.manage.byId(pluginId).avatar.post({ objectKey })
}

/** 运行插件（复用 /ai/plugin/run）；动态插件传实例 key 为 key，不传 configJson（后端按存储配置初始化）；多工具插件经 toolKey 选工具.*/
async function runPlugin(payload: {
  key: string
  requestJson: string
  /** 多工具插件的目标工具 key；单工具插件不传. */
  toolKey?: string | null
}): Promise<PluginRunResult | null> {
  const client = getAiPluginClient()
  const body: RunPluginCommand = {
    key: payload.key,
    requestJson: payload.requestJson,
    // Kiota 将关键字 function 转义为 functionEscaped（后端字段 function：工具/函数标识）
    ...(payload.toolKey ? { functionEscaped: payload.toolKey } : {}),
  }
  return (await client.api.ai.plugin.run.post(body)) ?? null
}

/** 保存/写回静态插件信息.*/
async function saveStaticPlugin(payload: {
  pluginKey: string
  title: string
  description: string
  classifyId: number
}): Promise<EmptyCommandResponse | null> {
  const client = getAiPluginClient()
  const body: SaveStaticPluginCommand = {
    pluginKey: payload.pluginKey,
    title: payload.title,
    description: payload.description,
    classifyId: payload.classifyId,
  }
  return (await client.api.ai.plugin.static.save.post(body)) ?? null
}

/** 查询动态插件模板列表（注册表已发现的动态插件）.*/
async function getDynamicTemplates(): Promise<DynamicPluginTemplate[]> {
  const client = getAiPluginClient()
  const res = await client.api.ai.plugin.get()
  return (res?.items ?? []).filter((i) => i.isDynamic === true)
}

/** 保存/创建动态插件实例（avatarPath 仅新建时用于直接落头像；编辑态头像走 updatePluginAvatar）.*/
async function saveDynamicPlugin(payload: {
  pluginKey: string
  templeteKey: string
  title: string
  description: string
  classifyId: number
  config: string
  avatarPath?: string | null
}): Promise<EmptyCommandResponse | null> {
  const client = getAiPluginClient()
  const body: SaveDynamicPluginCommand = {
    pluginKey: payload.pluginKey,
    templeteKey: payload.templeteKey,
    title: payload.title,
    description: payload.description,
    classifyId: payload.classifyId,
    config: payload.config,
    ...(payload.avatarPath ? { avatarPath: payload.avatarPath } : {}),
  }
  return (await client.api.ai.plugin.dynamic.save.post(body)) ?? null
}

/** 删除动态插件实例.*/
async function deleteDynamicPlugin(pluginKey: string): Promise<EmptyCommandResponse | null> {
  const client = getAiPluginClient()
  const body: DeleteDynamicPluginCommand = { pluginKey }
  return (await client.api.ai.plugin.dynamic.delete(body)) ?? null
}

/** 插件工具项（工具清单/语义配置共用；单工具插件固定一个 key=run 的项）. */
export interface PluginToolItem {
  key: string
  title: string
  description: string
  paramsExample?: string | null
}

/** 查询插件工具清单（模板 key 或实例 key；多工具插件返回全部工具）. */
async function getPluginTools(key: string): Promise<PluginToolItem[]> {
  const client = getAiPluginClient()
  const res = await client.api.ai.plugin.tools.get({
    queryParameters: { key },
  })
  return (res?.items ?? []).map((x) => ({
    key: String(x.key ?? 'run'),
    title: String(x.title ?? ''),
    description: String(x.description ?? ''),
    paramsExample: x.paramsExample ?? null,
  }))
}

/** 查询单个工具的参数运行示例 JSON（每个工具独立的示例接口）. */
async function getPluginToolExample(key: string, toolKey: string): Promise<string | null> {
  const client = getAiPluginClient()
  const res = await client.api.ai.plugin.tool.example.get({
    queryParameters: { key, toolKey },
  })
  return res?.paramsExample ?? null
}

/** 插件语义条目项（mode: always=常驻注入 / onDemand=按需经 plugin_note 取回）. */
export interface PluginSemanticNoteItem {
  id?: string | null
  toolKey: string
  title: string
  content: string
  mode: 'always' | 'onDemand'
  sortOrder: number
}

/** 插件工具元数据项（补充说明/调用策略/MCP 注解）. */
export interface PluginToolMetaItem {
  toolKey: string
  extraNotes: string
  callPolicy: string
  readOnlyHint: boolean
  destructiveHint: boolean
}

/** 插件语义配置（查询/保存共用形状）. */
export interface PluginSemanticData {
  pluginId: string
  title: string
  pluginName: string
  usageNotes: string
  notes: PluginSemanticNoteItem[]
  toolMetas: PluginToolMetaItem[]
  tools: PluginToolItem[]
}

/** 归一语义配置响应（系统侧与团队侧生成客户端共用；字段可空统一兜底）. */
export function normalizePluginSemantic(
  res: {
    pluginId?: string | null
    title?: string | null
    pluginName?: string | null
    usageNotes?: string | null
    notes?: {
      id?: string | null
      toolKey?: string | null
      title?: string | null
      content?: string | null
      mode?: string | null
      sortOrder?: unknown
    }[] | null
    toolMetas?: {
      toolKey?: string | null
      extraNotes?: string | null
      callPolicy?: string | null
      readOnlyHint?: boolean | null
      destructiveHint?: boolean | null
    }[] | null
    tools?: {
      key?: string | null
      title?: string | null
      description?: string | null
      paramsExample?: string | null
    }[] | null
  },
  fallbackPluginId: string,
): PluginSemanticData {
  return {
    pluginId: String(res.pluginId ?? fallbackPluginId),
    title: String(res.title ?? ''),
    pluginName: String(res.pluginName ?? ''),
    usageNotes: String(res.usageNotes ?? ''),
    notes: (res.notes ?? []).map((x) => ({
      id: x.id ?? null,
      toolKey: String(x.toolKey ?? ''),
      title: String(x.title ?? ''),
      content: String(x.content ?? ''),
      mode: x.mode === 'onDemand' ? 'onDemand' : 'always',
      sortOrder: Number(x.sortOrder ?? 0),
    })),
    toolMetas: (res.toolMetas ?? []).map((x) => ({
      toolKey: String(x.toolKey ?? ''),
      extraNotes: String(x.extraNotes ?? ''),
      callPolicy: String(x.callPolicy ?? ''),
      readOnlyHint: x.readOnlyHint === true,
      destructiveHint: x.destructiveHint === true,
    })),
    tools: (res.tools ?? []).map((x) => ({
      key: String(x.key ?? 'run'),
      title: String(x.title ?? ''),
      description: String(x.description ?? ''),
      paramsExample: x.paramsExample ?? null,
    })),
  }
}

/** 查询插件语义配置（使用须知/语义条目/工具元数据/工具清单）. */
async function getPluginSemantic(pluginId: string): Promise<PluginSemanticData | null> {
  const client = getAiPluginClient()
  const res = await client.api.ai.plugin.manage.byId(pluginId).semantic.get()
  if (!res) return null
  return normalizePluginSemantic(res, pluginId)
}

/** 保存插件语义配置（整体覆盖：使用须知/语义条目/工具元数据）. */
async function savePluginSemantic(
  pluginId: string,
  payload: { usageNotes: string; notes: PluginSemanticNoteItem[]; toolMetas: PluginToolMetaItem[] },
): Promise<void> {
  const client = getAiPluginClient()
  await client.api.ai.plugin.manage.byId(pluginId).semantic.put({
    pluginId,
    usageNotes: payload.usageNotes,
    notes: payload.notes,
    toolMetas: payload.toolMetas,
  })
}

export const pluginApi = {
  getManagePlugins,
  getPluginStats,
  runPlugin,
  saveStaticPlugin,
  getDynamicTemplates,
  saveDynamicPlugin,
  deleteDynamicPlugin,
  updatePluginAvatar,
  getPluginTools,
  getPluginToolExample,
  getPluginSemantic,
  savePluginSemantic,
}

/** 在线插件市场摘要（ID 按 MoPlatform 的 long 字符串契约回传）. */
export interface OnlinePluginItem {
  pluginId: string
  key: string
  name: string
  summary: string
  iconUrl: string
  latestVersion: string
  downloadCount: string
  isInstalled: boolean
  categoryId?: string
  categoryName?: string
  categoryEmoji?: string
}

/** MoPlatform 在线插件分类. */
export interface OnlinePluginCategory {
  id: string
  name: string
  emoji: string
}

/** 本地已安装插件快照. */
export interface InstalledOnlinePlugin {
  pluginKey: string
  name: string
  summary: string
  description: string
  avatarPath: string
  version: string
  packageSha256: string
  enabled: boolean
  createTime?: string
  createUserId?: string | number
  createUserName?: string
  updateTime?: string
  updateUserId?: string | number
  updateUserName?: string
}

async function marketplaceRequest<T>(
  path: string,
  method: HttpMethod,
  query?: Record<string, string | number | boolean | undefined>,
  body?: unknown,
): Promise<T> {
  const adapter = createAiPluginRequestAdapter()
  const request = new RequestInformation()
  request.URL = new URL(`/api/ai/plugin/online/${path}`, Env.serverUrl).toString()
  request.httpMethod = method
  request.headers.add('Accept', 'application/json')
  if (query) request.setQueryStringParametersFromRawObject(query)
  if (body !== undefined) {
    const bytes = new TextEncoder().encode(JSON.stringify(body))
    request.setStreamContent(bytes.buffer as ArrayBuffer, 'application/json')
  }
  const response = await adapter.sendPrimitive<ArrayBuffer>(request, 'ArrayBuffer', undefined)
  if (!response) throw new Error('Empty plugin marketplace response')
  return JSON.parse(new TextDecoder().decode(response)) as T
}

/** MoPlatform 在线插件目录与 MoAI 本地安装管理（请求经 Kiota adapter 和统一鉴权/错误中间件）. */
export const pluginMarketplaceApi = {
  getCatalog: (keyword = '', pageNo = 1, pageSize = 20, categoryId = '') =>
    marketplaceRequest<{ items: OnlinePluginItem[]; total: string | number }>(
      'catalog',
      HttpMethod.GET,
      { keyword: keyword || undefined, pageNo, pageSize, categoryId: categoryId || undefined },
    ),
  getCategories: () => marketplaceRequest<OnlinePluginCategory[]>('categories', HttpMethod.GET),
  getInstalled: () => marketplaceRequest<InstalledOnlinePlugin[]>('installed', HttpMethod.GET),
  install: (platformPluginId: string) =>
    marketplaceRequest<InstalledOnlinePlugin>('install', HttpMethod.POST, undefined, { platformPluginId }),
  setEnabled: (pluginKey: string, enabled: boolean) =>
    marketplaceRequest<InstalledOnlinePlugin>('status', HttpMethod.POST, undefined, { pluginKey, enabled }),
}
