import { getApiClient } from '@/api/kiota'
import type {
  EmptyCommandResponse,
  KeyValueString,
  PluginFunctionItem,
  PluginRunResult,
  PluginType,
  PreUploadOpenApiFilePluginCommandResponse,
  PreUploadTeamOpenApiFileCommand,
  QueryCustomPluginDetailCommandResponse,
  QueryCustomPluginFunctionsListCommandResponse,
  QueryPluginListCommandResponse,
  QueryTeamPluginsCommandResponse,
  SaveTeamDynamicPluginCommand,
  SaveTeamMcpPluginCommand,
  SaveTeamOpenApiPluginCommand,
  SimpleGuid,
  TeamPluginItem,
} from '@/api/client/models'

/** 团队可用插件项（与生成客户端 TeamPluginItem 字段对齐）. */
export type TeamPluginItemType = TeamPluginItem

/** 团队可用插件列表响应（与生成客户端 QueryTeamPluginsCommandResponse 字段对齐）. */
export interface TeamPluginsResponse {
  teamId?: string | number | null
  myRole?: number | null
  canManage?: boolean | null
  items?: TeamPluginItemType[] | null
}

/** 静态插件请求参数示例（team-plugin 列表项已携带）. */
export type TeamStaticPlugin = TeamPluginItemType

/** 动态插件实例管理项. */
export type TeamDynamicPluginItem = TeamPluginItemType & {
  instanceKey?: string | null
  templeteKey?: string | null
  config?: string | null
  configExample?: string | null
  paramsExample?: string | null
}

/** 自定义插件项. */
export type TeamCustomPluginItem = TeamPluginItemType & {
  server?: string | null
}

/** 团队插件种类：custom|dynamic|static. */
export type TeamPluginKind = 'custom' | 'dynamic' | 'static'

/** 查询团队可用插件列表（团队自有插件 + 可用的系统插件）；仅团队成员可访问. */
export async function getTeamPlugins(teamId: number): Promise<TeamPluginsResponse> {
  const client = getApiClient()
  const res: QueryTeamPluginsCommandResponse | undefined = await client.api.team
    .byId(String(teamId))
    .plugin.list.get()
  return {
    teamId: res?.teamId ?? null,
    myRole: res?.myRole ?? null,
    canManage: res?.canManage ?? null,
    items: res?.items ?? [],
  }
}

/** 保存/创建团队动态插件实例. */
export async function saveTeamDynamicPlugin(payload: {
  teamId: number
  instanceKey: string
  templeteKey: string
  title: string
  description: string
  config: string
  classifyId?: number
}): Promise<EmptyCommandResponse | null> {
  const client = getApiClient()
  const body: SaveTeamDynamicPluginCommand = {
    teamId: String(payload.teamId),
    instanceKey: payload.instanceKey,
    templeteKey: payload.templeteKey,
    title: payload.title,
    description: payload.description,
    config: payload.config,
    classifyId: payload.classifyId ?? 0,
  }
  return (await client.api.team.byId(String(payload.teamId)).plugin.dynamic.post(body)) ?? null
}

/** 导入/更新团队 MCP 插件. */
export async function saveTeamMcpPlugin(payload: {
  teamId: number
  pluginId?: string | null
  name: string
  title: string
  description: string
  serverUrl: string
  header?: KeyValueString[]
  query?: KeyValueString[]
  classifyId?: number
}): Promise<string | null> {
  const client = getApiClient()
  const body: SaveTeamMcpPluginCommand = {
    teamId: String(payload.teamId),
    pluginId: payload.pluginId ?? null,
    name: payload.name,
    title: payload.title,
    description: payload.description,
    serverUrl: payload.serverUrl,
    header: payload.header ?? [],
    query: payload.query ?? [],
    classifyId: payload.classifyId ?? 0,
  }
  const res: SimpleGuid | undefined = await client.api.team
    .byId(String(payload.teamId))
    .plugin.mcp.post(body)
  return res?.value ?? null
}

/** 导入/更新团队 OpenAPI 插件. */
export async function saveTeamOpenApiPlugin(payload: {
  teamId: number
  pluginId?: string | null
  fileId: number
  fileName: string
  name: string
  title: string
  description: string
  header?: KeyValueString[]
  query?: KeyValueString[]
  classifyId?: number
}): Promise<string | null> {
  const client = getApiClient()
  const body: SaveTeamOpenApiPluginCommand = {
    teamId: String(payload.teamId),
    pluginId: payload.pluginId ?? null,
    fileId: String(payload.fileId),
    fileName: payload.fileName,
    name: payload.name,
    title: payload.title,
    description: payload.description,
    header: payload.header ?? [],
    query: payload.query ?? [],
    classifyId: payload.classifyId ?? 0,
  }
  const res: SimpleGuid | undefined = await client.api.team
    .byId(String(payload.teamId))
    .plugin.openapi.post(body)
  return res?.value ?? null
}

/** 查询团队自定义插件详情（编辑回填用，需团队上下文）.*/
export async function getTeamPluginDetail(
  teamId: number,
  pluginId: string,
): Promise<QueryCustomPluginDetailCommandResponse | null> {
  const client = getApiClient()
  return (
    (await client.api.team.byId(String(teamId)).plugin.byPluginId(pluginId).detail.get()) ?? null
  )
}

/** 查询团队插件函数列表.*/
export async function getTeamPluginFunctions(
  teamId: number,
  pluginId: string,
): Promise<PluginFunctionItem[]> {
  const client = getApiClient()
  const res: QueryCustomPluginFunctionsListCommandResponse | undefined =
    await client.api.team.byId(String(teamId)).plugin.byPluginId(pluginId).functions.post()
  return res?.items ?? []
}

/** 刷新团队 MCP 插件工具列表.*/
export async function refreshTeamMcp(teamId: number, pluginId: string): Promise<void> {
  const client = getApiClient()
  await client.api.team.byId(String(teamId)).plugin.byPluginId(pluginId).refresh_mcp.post()
}

/** 预上传团队 OpenAPI 文件（签名 URL + fileId）.*/
export async function preUploadTeamOpenApiFile(payload: {
  teamId: number
  pluginName: string
  fileName: string
  contentType: string
  fileSize: number
  sha256: string
}): Promise<PreUploadOpenApiFilePluginCommandResponse | null> {
  const client = getApiClient()
  const body: PreUploadTeamOpenApiFileCommand = {
    teamId: String(payload.teamId),
    pluginName: payload.pluginName,
    fileName: payload.fileName,
    contentType: payload.contentType,
    fileSize: payload.fileSize,
    shA256: payload.sha256,
  }
  return (await client.api.team.byId(String(payload.teamId)).plugin.pre_upload_openapi.post(body)) ?? null
}

/** 执行团队可用插件.*/
export async function runTeamPlugin(payload: {
  teamId: number
  key: string
  requestJson: string
}): Promise<PluginRunResult | null> {
  const client = getApiClient()
  return (
    (await client.api.team.byId(String(payload.teamId)).plugin.run.post({
      teamId: String(payload.teamId),
      key: payload.key,
      requestJson: payload.requestJson,
    })) ?? null
  )
}

/** 查询可用动态插件模板（注册表已发现的动态插件），仅团队成员可访问.*/
export async function getTeamDynamicTemplates(teamId: number): Promise<QueryPluginListCommandResponse> {
  const client = getApiClient()
  const res = await client.api.team.byId(String(teamId)).plugin.dynamic_templates.get()
  return res ?? { items: [] }
}

/** 删除团队插件（仅团队自有插件），需团队 Owner/Admin. */
export async function deleteTeamPlugin(teamId: number, pluginId: string): Promise<EmptyCommandResponse | null> {
  const client = getApiClient()
  return (await client.api.team.byId(String(teamId)).plugin.byPluginId(pluginId).delete()) ?? null
}

export { PluginType }
