import { getApiClient } from '@/api/kiota'

/** 组织角色列表项（与生成客户端 QueryOrgRolesCommandResponseItem 字段对齐） */
export interface OrgRoleItem {
  orgRoleId?: number | null
  name?: string | null
  /** 上级角色 id，null=根角色 */
  parentId?: number | null
  /** 物化路径（/{id}/ 链，前缀匹配即得子树） */
  path?: string | null
  userCount?: number | null
  hasChildren?: boolean | null
  /** 是否为组织默认角色（新注册用户自动获得） */
  isDefault?: boolean | null
  createTime?: string | null
  createUserName?: string | null
  updateTime?: string | null
  updateUserName?: string | null
}

/** 角色下用户项（与生成客户端 QueryOrgRoleUsersCommandResponseItem 字段对齐，userId 为字符串化 long；用户资料字段与用户管理列表对齐） */
export interface OrgRoleUserItem {
  userId?: string | null
  userName?: string | null
  nickName?: string | null
  email?: string | null
  phone?: string | null
  avatar?: string | null
  isAdmin?: boolean | null
  isRoot?: boolean | null
  isDisable?: boolean | null
  /** 加入该组织的时间（绑定行创建时间） */
  createTime?: string | null
  createUserName?: string | null
}

/** 用户持有的组织角色项（与生成客户端 QueryUserOrgRolesCommandResponseItem 字段对齐） */
export interface UserOrgRoleItem {
  orgRoleId?: number | null
  name?: string | null
  path?: string | null
}

/** 某用户的角色集合回显 */
export interface UserOrgRoles {
  userId?: number | null
  userName?: string | null
  nickName?: string | null
  items: UserOrgRoleItem[]
}

/** 查询组织角色全量平铺列表（仅管理员），按 path 排序，前端按 parentId 组树 */
export async function getOrgRoles(search?: string): Promise<OrgRoleItem[]> {
  const client = getApiClient()
  const res = await client.api.org.role.list.get({
    queryParameters: { search: search?.trim() || undefined },
  })
  return res?.items ?? []
}

/** 创建组织角色（仅管理员），parentId 为空表示根角色 */
export async function createOrgRole(payload: { name: string; parentId?: number | null }): Promise<{ orgRoleId?: number | null; path?: string | null }> {
  const client = getApiClient()
  const res = await client.api.org.role.create.post({
    name: payload.name,
    parentId: payload.parentId ?? null,
  })
  return { orgRoleId: res?.orgRoleId, path: res?.path }
}

/** 更新组织角色（仅管理员）：改名与调整上级（null=调整为根角色），调整上级时整体重写子树路径 */
export async function updateOrgRole(roleId: number, payload: { name: string; parentId?: number | null }): Promise<void> {
  const client = getApiClient()
  await client.api.org.role.byId(roleId).put({
    name: payload.name,
    parentId: payload.parentId ?? null,
  })
}

/** 删除组织角色（仅管理员）：存在子角色时后端拒绝；同时解绑该角色下的全部用户；默认角色被删时设置自动清空 */
export async function deleteOrgRole(roleId: number): Promise<void> {
  const client = getApiClient()
  await client.api.org.role.byId(roleId).delete()
}

/** 设置/清空组织默认角色（仅管理员）：roleId 为空表示清空；全局唯一后设覆盖，只影响新注册用户 */
export async function updateOrgRoleDefault(roleId: number | null): Promise<void> {
  const client = getApiClient()
  await client.api.org.role.defaultEscaped.put({ orgRoleId: roleId })
}

/** 查询某组织角色下的用户清单（仅管理员） */
export async function getOrgRoleUsers(roleId: number, search?: string): Promise<OrgRoleUserItem[]> {
  const client = getApiClient()
  const res = await client.api.org.role.byId(roleId).users.get({
    queryParameters: { search: search?.trim() || undefined },
  })
  return res?.items ?? []
}

/** 查询某用户持有的组织角色清单（仅管理员） */
export async function getUserOrgRoles(userId: number): Promise<UserOrgRoles> {
  const client = getApiClient()
  const res = await client.api.org.user.byUserId(String(userId)).roles.get()
  return {
    userId: res?.userId ? Number(res.userId) : userId,
    userName: res?.userName,
    nickName: res?.nickName,
    items: res?.items ?? [],
  }
}

/** 整体覆盖某用户的组织角色集合（仅管理员），空列表=清空 */
export async function updateUserOrgRoles(userId: number, roleIds: number[]): Promise<void> {
  const client = getApiClient()
  await client.api.org.user.byUserId(String(userId)).roles.put({ roleIds })
}
