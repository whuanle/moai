import { getApiClient } from '@/api/kiota'
import { revealSubsysAppSecret as revealSubsysAppSecretEnvelope } from '@/api/secretEnvelope'

/** 接入状态：enabled=启用 disabled=停用 */
export type SubsystemStatus = 'enabled' | 'disabled'

/** 系统级接入类型：subsystem=子系统 app=应用接入（none=团队级行占位，不出现在本列表） */
export type AccessAppTypeValue = 'subsystem' | 'app' | 'none'

/** 团队授权范围：allTeams=全员可用 specifiedTeams=指定团队 */
export type SubsystemTeamScopeType = 'allTeams' | 'specifiedTeams'

/** 组织授权方向：self=仅该组织 up=该组织及以上 down=该组织及以下 */
export type OrgGrantScopeType = 'self' | 'up' | 'down'

/** 已授权团队项（指定团队范围，列表展示用） */
export interface AuthorizedTeamItem {
  teamId?: number | null
  teamName?: string | null
}

/** 已授权组织项（列表展示用） */
export interface AuthorizedOrgItem {
  orgRoleId?: number | null
  orgName?: string | null
  scope?: OrgGrantScopeType | null
}

/** 系统级接入列表项（子系统与应用接入同表回显，与生成客户端 QuerySubsysAppsCommandResponseItem 字段对齐） */
export interface SubsysAppItem {
  id?: string | null
  /** 接入类型：subsystem=子系统 app=应用接入 */
  appType?: AccessAppTypeValue | null
  key?: string | null
  /** key 展示前缀 */
  keyPrefix?: string | null
  /** 密钥展示掩码（前 4 + **** + 后 4，便于核对；原文可随时信封解密查看） */
  secretMask?: string | null
  name?: string | null
  description?: string | null
  status?: SubsystemStatus | null
  teamScope?: SubsystemTeamScopeType | null
  authorizedTeamCount?: number | null
  /** 指定团队范围下的授权团队名称清单（按团队 id 升序，团队已删除时名称为 #id；仅子系统行） */
  authorizedTeams?: AuthorizedTeamItem[] | null
  /** 组织授权清单（统一组织授权表，按组织 id 升序，角色已删除时名称为 #id；仅子系统行） */
  authorizedOrgs?: AuthorizedOrgItem[] | null
  online?: boolean | null
  /** 在线实例数（心跳判定窗口内有心跳的实例个数） */
  onlineInstances?: number | null
  /** 委托 SSO 登录回调地址列表（仅子系统行，空列表=未开放登录接入） */
  callbackUrls?: string[] | null
  createTime?: string | null
  createUserName?: string | null
  updateTime?: string | null
  updateUserName?: string | null
}

/** 创建系统级接入载荷（子系统需自填 key 并可带授权/回调；应用接入自动生成 appid） */
export interface CreateSubsysAppPayload {
  appType: AccessAppTypeValue
  /** 仅子系统：接入标识（appid），创建后不可修改 */
  key?: string
  name: string
  description?: string
  teamScope?: SubsystemTeamScopeType
  teamIds?: number[]
  /** 委托 SSO 登录回调地址列表（仅子系统，最多 10 个） */
  callbackUrls?: string[]
}

/** 更新系统级接入载荷：名称/描述/状态；子系统行另支持团队授权与回调地址（appid 不可变更） */
export interface UpdateSubsysAppPayload {
  name: string
  description?: string
  status: SubsystemStatus
  teamScope?: SubsystemTeamScopeType
  teamIds?: number[]
  callbackUrls?: string[]
}

/** 委托 SSO 委托码签发结果 */
export interface SubsystemSsoAuthorizeResult {
  code?: string | null
  callbackUrl?: string | null
  name?: string | null
}

/** 创建结果：appid 返回原文，密钥仅回掩码（原文可随时经 revealSubsysAppSecret 信封查看） */
export interface CreatedSubsysApp {
  id?: string | null
  appType?: AccessAppTypeValue | null
  key?: string | null
  keyPrefix?: string | null
  secretMask?: string | null
}

/** 查询系统级接入列表（仅 root），子系统与应用接入同表回显 */
export async function getSubsysApps(search?: string, appType?: AccessAppTypeValue): Promise<SubsysAppItem[]> {
  const client = getApiClient()
  const res = await client.api.subsystem.list.get({
    queryParameters: { search: search?.trim() || undefined, appType },
  })
  return res?.items ?? []
}

/** 创建系统级接入（仅 root），返回 appid 原文与密钥掩码（密钥原文可随时经 revealSubsysAppSecret 查看）；新建即启用 */
export async function createSubsysApp(payload: CreateSubsysAppPayload): Promise<CreatedSubsysApp> {
  const client = getApiClient()
  const res = await client.api.subsystem.create.post({
    appType: payload.appType,
    key: payload.key,
    name: payload.name,
    description: payload.description,
    teamScope: payload.appType === 'subsystem' ? payload.teamScope : undefined,
    teamIds: payload.appType === 'subsystem' ? payload.teamIds : undefined,
    callbackUrls: payload.appType === 'subsystem' ? payload.callbackUrls : undefined,
  })
  return { id: res?.id, appType: res?.appType, key: res?.key, keyPrefix: res?.keyPrefix, secretMask: res?.secretMask }
}

/** 更新系统级接入（仅 root）：名称/描述/状态；子系统行另支持团队授权与回调地址 */
export async function updateSubsysApp(id: string, payload: UpdateSubsysAppPayload): Promise<void> {
  const client = getApiClient()
  await client.api.subsystem.byId(id).put({
    name: payload.name,
    description: payload.description,
    status: payload.status,
    teamScope: payload.teamScope,
    teamIds: payload.teamIds,
    callbackUrls: payload.callbackUrls,
  })
}

/** 删除系统级接入（仅 root，软删除），appid 立即失效 */
export async function deleteSubsysApp(id: string): Promise<void> {
  const client = getApiClient()
  await client.api.subsystem.byId(id).delete()
}

/** 重置系统级接入密钥（仅 root），旧密钥立即失效，返回新密钥掩码（原文可随时经 revealSubsysAppSecret 查看） */
export async function resetSubsysAppSecret(id: string): Promise<{ secretMask?: string | null }> {
  const client = getApiClient()
  const res = await client.api.subsystem.byId(id).resetSecret.post()
  return { secretMask: res?.secretMask }
}

/** 回显系统级接入密钥原文（仅 root，RSA+AES 信封加密传输，随时可查看） */
export async function revealSubsysAppSecret(id: string): Promise<string> {
  return revealSubsysAppSecretEnvelope(id)
}

/** 查询子系统已授权团队 id 列表（仅 root），用于编辑回显 */
export async function getSubsystemAuthorizedTeams(subsystemId: string): Promise<number[]> {
  const client = getApiClient()
  const res = await client.api.subsystem.byId(subsystemId).teams.get()
  return res?.teamIds ?? []
}

/** 子系统组织授权配置回显项 */
export interface SubsystemOrgGrantItem {
  orgRoleId?: number | null
  roleName?: string | null
  rolePath?: string | null
  scope?: OrgGrantScopeType | null
}

/** 查询子系统组织授权配置（仅 root），用于编辑回显 */
export async function getSubsystemOrgGrants(subsystemId: string): Promise<SubsystemOrgGrantItem[]> {
  const client = getApiClient()
  const res = await client.api.subsystem.byId(subsystemId).orgGrants.get()
  return (res?.items ?? []) as SubsystemOrgGrantItem[]
}

/** 整体覆盖子系统组织授权集合（仅 root），空列表=清空组织授权 */
export async function setSubsystemOrgGrants(subsystemId: string, grants: Array<{ orgRoleId: number; scope: OrgGrantScopeType }>): Promise<void> {
  const client = getApiClient()
  await client.api.subsystem.byId(subsystemId).orgGrants.put({
    // 子系统 id 由路由回填，请求体仅携带授权项
    grants: grants.map((grant) => ({ orgRoleId: grant.orgRoleId, scope: grant.scope })),
  })
}

/** 委托 SSO：登录用户为子系统签发一次性委托码（redirectUri 指定本次回跳入口，须在注册列表内；缺省取第一个） */
export async function subsystemSsoAuthorizeCode(subsystemKey: string, state?: string, redirectUri?: string): Promise<SubsystemSsoAuthorizeResult> {
  const client = getApiClient()
  const res = await client.api.subsystem.sso.code.post({
    subsystemKey,
    state: state || undefined,
    redirectUri: redirectUri || undefined,
  })
  return { code: res?.code, callbackUrl: res?.callbackUrl, name: res?.name }
}
