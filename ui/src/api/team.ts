import { getApiClient } from '@/api/kiota'
import { uploadImageWithKey } from '@/utils/storage'

/** 与生成客户端 TeamRoleObject 一致（后端枚举按字符串序列化） */
const ROLE_STR: Record<number, 'owner' | 'admin' | 'member'> = {
  0: 'owner',
  1: 'admin',
  2: 'member',
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
}

export interface TeamUserItem {
  userId?: string | number | null
  userName?: string | null
  nickName?: string | null
  avatar?: string | null
  role?: number | null
  joinTime?: string | null
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

export async function dissolveTeam(teamId: number): Promise<void> {
  const client = getApiClient()
  await client.api.team.byId(String(teamId)).delete()
}

export async function getTeamUsers(teamId: number): Promise<TeamUserItem[]> {
  const client = getApiClient()
  const res = await client.api.team.byId(String(teamId)).users.get()
  return res?.items ?? []
}

export async function addTeamUser(teamId: number, payload: { userId: number; role: number }): Promise<void> {
  const client = getApiClient()
  await client.api.team
    .byId(String(teamId))
    .users.post({ userId: String(payload.userId), role: ROLE_STR[payload.role] })
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

export async function transferTeamOwner(teamId: number, userId: number): Promise<void> {
  const client = getApiClient()
  await client.api.team.byId(String(teamId)).owner.put({ userId: String(userId) })
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
