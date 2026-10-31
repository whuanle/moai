import { getApiClient } from '@/api/kiota'
import type { OrgRoleItem, OrgRoleUserItem } from './org'
import { uploadImageWithKey } from '@/utils/storage'

/** 与生成客户端 TeamRoleObject 一致（后端枚举按字符串序列化）：0=Member 1=Admin 2=Owner */
const ROLE_STR: Record<number, 'owner' | 'admin' | 'member'> = {
  0: 'member',
  1: 'admin',
  2: 'owner',
}

export interface TeamItem {
  /** 后端 long 序列化为字符串 */
  teamId?: string | number | null
  name?: string | null
  description?: string | null
  avatar?: string | null
  isDisable?: boolean | null
  /** 0=Owner 1=Admin 2=Member */
  myRole?: number | null
  memberCount?: number | null
  createTime?: string | null
  /** 团队负责人（Owner） */
  ownerUserId?: string | number | null
  ownerUserName?: string | null
  ownerNickName?: string | null
  ownerAvatar?: string | null
}

export interface TeamUserItem {
  userId?: string | number | null
  userName?: string | null
  nickName?: string | null
  avatar?: string | null
  role?: number | null
  joinTime?: string | null
}

/** 可邀请的候选用户（与生成客户端 TeamCandidateItem 的字段对齐） */
export interface TeamCandidateItem {
  userId?: string | number | null
  userName?: string | null
  nickName?: string | null
  avatar?: string | null
}

export async function createTeam(payload: { name: string; description?: string }): Promise<number> {
  const client = getApiClient()
  const res = await client.api.team.post({ name: payload.name, description: payload.description })
  return Number(res?.value ?? 0)
}

export async function getMyTeams(): Promise<TeamItem[]> {
  const client = getApiClient()
  const res = await client.api.team.list.get()
  return res?.items ?? []
}

export async function getTeamDetail(teamId: number) {
  const client = getApiClient()
  return client.api.team.byId(String(teamId)).get()
}

export async function updateTeam(teamId: number, payload: { name?: string; description?: string }): Promise<void> {
  const client = getApiClient()
  await client.api.team.byId(String(teamId)).put({ name: payload.name, description: payload.description })
}

export async function getTeamUsers(teamId: number): Promise<TeamUserItem[]> {
  const client = getApiClient()
  const res = await client.api.team.byId(String(teamId)).users.get()
  return res?.items ?? []
}

export async function getTeamCandidates(teamId: number, keyword: string): Promise<TeamCandidateItem[]> {
  const client = getApiClient()
  const res = await client.api.team
    .byId(String(teamId))
    .candidates.get({ queryParameters: { keyword: keyword || undefined } })
  return res?.items ?? []
}

export async function addTeamUser(teamId: number, payload: { userId: number; role: number }): Promise<void> {
  const client = getApiClient()
  await client.api.team
    .byId(String(teamId))
    .users.post({ userId: String(payload.userId), role: ROLE_STR[payload.role] })
}

/** 批量添加成员结果（与生成客户端 AddTeamUsersCommandResponse 字段对齐，userId 为字符串化 long） */
export interface AddTeamUsersResult {
  /** 成功加入团队的成员数 */
  addedCount: number
  /** 已是团队成员被跳过的用户 id 集合 */
  skippedUserIds: string[]
  /** 不存在被跳过的用户 id 集合 */
  notFoundUserIds: string[]
}

/** 批量添加团队成员（仅 Owner/Admin，授予 Admin 需 Owner），自动跳过已是成员与不存在的用户 */
export async function addTeamUsers(teamId: number, userIds: number[], role = 0): Promise<AddTeamUsersResult> {
  const client = getApiClient()
  const res = await client.api.team
    .byId(String(teamId))
    .users.batch.post({ userIds: userIds.map(String), role: ROLE_STR[role] })
  return {
    addedCount: res?.addedCount ?? 0,
    skippedUserIds: res?.skippedUserIds ?? [],
    notFoundUserIds: res?.notFoundUserIds ?? [],
  }
}

/** 团队侧组织角色平铺列表（复用组织查询的只读视图，仅团队 Owner/Admin，按 path 排序，前端按 parentId 组树） */
export async function getTeamOrgRoles(teamId: number, search?: string): Promise<OrgRoleItem[]> {
  const client = getApiClient()
  const res = await client.api.team
    .byId(String(teamId))
    .org.role.list.get({ queryParameters: { search: search?.trim() || undefined } })
  return res?.items ?? []
}

/** 团队侧某组织角色下的用户清单（复用组织查询的只读视图，仅团队 Owner/Admin） */
export async function getTeamOrgRoleUsers(teamId: number, roleId: number, search?: string): Promise<OrgRoleUserItem[]> {
  const client = getApiClient()
  const res = await client.api.team
    .byId(String(teamId))
    .org.role.byRoleId(roleId)
    .users.get({ queryParameters: { search: search?.trim() || undefined } })
  return res?.items ?? []
}

export async function updateTeamUserRole(teamId: number, userId: number, role: number): Promise<void> {
  const client = getApiClient()
  await client.api.team
    .byId(String(teamId))
    .user.byUserId(String(userId))
    .role.put({ role: ROLE_STR[role] })
}

export async function removeTeamUser(teamId: number, userId: number): Promise<void> {
  const client = getApiClient()
  await client.api.team.byId(String(teamId)).user.byUserId(String(userId)).delete()
}

/** 全部团队列表项（管理员，与生成客户端 QueryTeamAllCommandResponseItem 字段对齐） */
export interface AllTeamItem {
  /** 后端 long 序列化为字符串 */
  teamId?: string | number | null
  name?: string | null
  isDisable?: boolean | null
  memberCount?: number | null
}

/** 查询系统内全部团队（仅管理员），供模型授权等管理场景选择团队使用 */
export async function getAllTeams(): Promise<AllTeamItem[]> {
  const client = getApiClient()
  const res = await client.api.team.all.get()
  return res?.items ?? []
}

export async function transferTeamOwner(teamId: number, userId: number): Promise<void> {
  const client = getApiClient()
  await client.api.team.byId(String(teamId)).owner.put({ userId: String(userId) })
}

/** 管理员团队列表项（与生成客户端 QueryAdminTeamListCommandResponseItem 字段对齐） */
export interface AdminTeamItem {
  /** 后端 long 序列化为字符串 */
  teamId?: string | number | null
  name?: string | null
  description?: string | null
  avatar?: string | null
  isDisable?: boolean | null
  memberCount?: number | null
  ownerUserId?: string | number | null
  ownerUserName?: string | null
  ownerNickName?: string | null
  ownerAvatar?: string | null
  createUserId?: number | null
  createUserName?: string | null
  createTime?: string | null
  updateUserName?: string | null
  updateTime?: string | null
}

export interface AdminTeamListResult {
  totalCount?: number | null
  items?: AdminTeamItem[] | null
}

export interface GetAdminTeamsParams {
  pageNo?: number
  pageSize?: number
  searchText?: string
  /** 禁用状态筛选：不传=全部 true=仅已禁用 false=仅正常 */
  isDisable?: boolean
}

/** 分页查询系统内全部团队（仅管理员） */
export async function getAdminTeams(params: GetAdminTeamsParams): Promise<AdminTeamListResult> {
  const client = getApiClient()
  const res = await client.api.admin.team.list.get({
    queryParameters: {
      pageNo: params.pageNo,
      pageSize: params.pageSize,
      searchText: params.searchText,
      isDisable: params.isDisable,
    },
  })
  return { totalCount: res?.totalCount ?? 0, items: res?.items ?? [] }
}

/** 禁用/启用团队（仅管理员） */
export async function setTeamDisable(teamId: number, isDisable: boolean): Promise<void> {
  const client = getApiClient()
  await client.api.admin.team.byId(String(teamId)).disable.put({ isDisable })
}

/** 转让团队负责人（仅管理员），目标用户可为系统内任意用户 */
export async function adminTransferTeamOwner(teamId: number, userId: number): Promise<void> {
  const client = getApiClient()
  await client.api.admin.team.byId(String(teamId)).owner.put({ userId: String(userId) })
}

export async function setTeamAvatar(teamId: number, objectKey: string): Promise<void> {
  const client = getApiClient()
  await client.api.team.byId(String(teamId)).avatar.post({ objectKey })
}

/** 上传图片并设为团队头像（走存储直传管线），返回公开访问地址 */
export async function uploadTeamAvatar(teamId: number, file: File): Promise<string> {
  const { objectKey, url } = await uploadImageWithKey(file)
  await setTeamAvatar(teamId, objectKey)
  return url
}
