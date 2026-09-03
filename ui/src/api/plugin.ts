import { getAiPluginClient } from '@/api/kiota'
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

async function getManagePlugins(kind?: PluginKind): Promise<StaticPluginManageItem[]> {
  const client = getAiPluginClient()
  const res = await client.api.ai.plugin.manage.list.get({
    queryParameters: kind ? { kind } : undefined,
  })
  return (res?.items ?? []) as StaticPluginManageItem[]
}

/** 运行插件（复用 /ai/plugin/run）；动态插件传实例 key 为 key，不传 configJson（后端按存储配置初始化）.*/
async function runPlugin(payload: { key: string; requestJson: string }): Promise<PluginRunResult | null> {
  const client = getAiPluginClient()
  const body: RunPluginCommand = { key: payload.key, requestJson: payload.requestJson }
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

/** 保存/创建动态插件实例.*/
async function saveDynamicPlugin(payload: {
  pluginKey: string
  templeteKey: string
  title: string
  description: string
  classifyId: number
  config: string
}): Promise<EmptyCommandResponse | null> {
  const client = getAiPluginClient()
  const body: SaveDynamicPluginCommand = {
    pluginKey: payload.pluginKey,
    templeteKey: payload.templeteKey,
    title: payload.title,
    description: payload.description,
    classifyId: payload.classifyId,
    config: payload.config,
  }
  return (await client.api.ai.plugin.dynamic.save.post(body)) ?? null
}

/** 删除动态插件实例.*/
async function deleteDynamicPlugin(pluginKey: string): Promise<EmptyCommandResponse | null> {
  const client = getAiPluginClient()
  const body: DeleteDynamicPluginCommand = { pluginKey }
  return (await client.api.ai.plugin.dynamic.delete(body)) ?? null
}

export const pluginApi = {
  getManagePlugins,
  runPlugin,
  saveStaticPlugin,
  getDynamicTemplates,
  saveDynamicPlugin,
  deleteDynamicPlugin,
}
