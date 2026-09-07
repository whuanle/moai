import { getApiClient } from '@/api/kiota'
import type {
  EmptyCommandResponse,
  KeyValueString,
  PluginType,
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
}): Promise<EmptyCommandResponse | null> {
  const client = getApiClient()
  const body: SaveTeamDynamicPluginCommand = {
    teamId: String(payload.teamId),
    instanceKey: payload.instanceKey,
    templeteKey: payload.templeteKey,
    title: payload.title,
    description: payload.description,
    config: payload.config,
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
  }
  const res: SimpleGuid | undefined = await client.api.team
    .byId(String(payload.teamId))
    .plugin.openapi.post(body)
  return res?.value ?? null
}

/** 删除团队插件（仅团队自有插件），需团队 Owner/Admin. */
export async function deleteTeamPlugin(teamId: number, pluginId: string): Promise<EmptyCommandResponse | null> {
  const client = getApiClient()
  return (await client.api.team.byId(String(teamId)).plugin.byPluginId(pluginId).delete()) ?? null
}

export { PluginType }
