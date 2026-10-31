import { getApiClient } from '@/api/kiota'

/** 子系统状态：enabled=启用 disabled=停用 */
export type SubsystemStatus = 'enabled' | 'disabled'

/** 团队授权范围：allTeams=全员可用 specifiedTeams=指定团队 */
export type SubsystemTeamScopeType = 'allTeams' | 'specifiedTeams'

/** 已授权团队项（指定团队范围，列表展示用） */
export interface AuthorizedTeamItem {
  teamId?: number | null
  teamName?: string | null
}

/** 子系统列表项（与生成客户端 QuerySubsystemsCommandResponseItem 字段对齐） */
export interface SubsystemItem {
  subsystemId?: string | null
  key?: string | null
  name?: string | null
  description?: string | null
  status?: SubsystemStatus | null
  teamScope?: SubsystemTeamScopeType | null
  authorizedTeamCount?: number | null
  /** 指定团队范围下的授权团队名称清单（按团队 id 升序，团队已删除时名称为 #id） */
  authorizedTeams?: AuthorizedTeamItem[] | null
  online?: boolean | null
  /** 委托 SSO 登录回调地址，空串=未开放登录接入 */
  callbackUrl?: string | null
  createTime?: string | null
  createUserName?: string | null
  updateTime?: string | null
  updateUserName?: string | null
}

/** 创建/更新子系统载荷 */
export interface SubsystemPayload {
  name: string
  description?: string
  status: SubsystemStatus
  teamScope: SubsystemTeamScopeType
  /** specifiedTeams 时必填，整体覆盖 */
  teamIds: number[]
  /** 委托 SSO 登录回调地址，空=暂不开放登录接入 */
  callbackUrl?: string
}

/** 委托 SSO 委托码签发结果 */
export interface SubsystemSsoAuthorizeResult {
  code?: string | null
  callbackUrl?: string | null
  name?: string | null
}

/** 创建子系统结果：密钥原文仅此一次 */
export interface CreatedSubsystem {
  subsystemId?: string | null
  secret?: string | null
}

/** 查询子系统列表（仅 root），附带在线状态与授权团队数 */
export async function getSubsystems(search?: string): Promise<SubsystemItem[]> {
  const client = getApiClient()
  const res = await client.api.subsystem.list.get({
    queryParameters: { search: search?.trim() || undefined },
  })
  return res?.items ?? []
}

/** 创建子系统（仅 root），返回接入密钥原文（仅此一次）；新建即启用 */
export async function createSubsystem(payload: { key: string; name: string; description?: string; teamScope: SubsystemTeamScopeType; teamIds: number[]; callbackUrl?: string }): Promise<CreatedSubsystem> {
  const client = getApiClient()
  const res = await client.api.subsystem.create.post({
    key: payload.key,
    name: payload.name,
    description: payload.description,
    teamScope: payload.teamScope,
    teamIds: payload.teamIds,
    callbackUrl: payload.callbackUrl,
  })
  return { subsystemId: res?.subsystemId, secret: res?.secret }
}

/** 更新子系统（仅 root）：名称/描述/状态/团队授权范围/登录回调地址，授权团队列表整体覆盖 */
export async function updateSubsystem(subsystemId: string, payload: SubsystemPayload): Promise<void> {
  const client = getApiClient()
  await client.api.subsystem.byId(subsystemId).put({
    name: payload.name,
    description: payload.description,
    status: payload.status,
    teamScope: payload.teamScope,
    teamIds: payload.teamIds,
    callbackUrl: payload.callbackUrl,
  })
}

/** 删除子系统（仅 root），软删除 */
export async function deleteSubsystem(subsystemId: string): Promise<void> {
  const client = getApiClient()
  await client.api.subsystem.byId(subsystemId).delete()
}

/** 重置子系统接入密钥（仅 root），旧密钥立即失效，返回新密钥原文（仅此一次） */
export async function resetSubsystemSecret(subsystemId: string): Promise<{ secret?: string | null }> {
  const client = getApiClient()
  const res = await client.api.subsystem.byId(subsystemId).resetSecret.post()
  return { secret: res?.secret }
}

/** 查询子系统已授权团队 id 列表（仅 root），用于编辑回显 */
export async function getSubsystemAuthorizedTeams(subsystemId: string): Promise<number[]> {
  const client = getApiClient()
  const res = await client.api.subsystem.byId(subsystemId).teams.get()
  return res?.teamIds ?? []
}

/** 委托 SSO：登录用户为子系统签发一次性委托码（回跳地址取子系统注册值） */
export async function subsystemSsoAuthorizeCode(subsystemKey: string, state?: string): Promise<SubsystemSsoAuthorizeResult> {
  const client = getApiClient()
  const res = await client.api.subsystem.sso.code.post({
    subsystemKey,
    state: state || undefined,
  })
  return { code: res?.code, callbackUrl: res?.callbackUrl, name: res?.name }
}
