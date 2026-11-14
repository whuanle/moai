import { getApiClient } from '@/api/kiota'

/** 组织列表项（与生成客户端 QueryOrgRolesCommandResponseItem 字段对齐） */
export interface OrgRoleItem {
  orgRoleId?: number | null
  name?: string | null
  /** 上级角色 id，null=根角色 */
  parentId?: number | null
  /** 物化路径（/{id}/ 链，前缀匹配即得子树） */
  path?: string | null
  userCount?: number | null
  hasChildren?: boolean | null
  /** 同级展示顺序，从小到大 */
  sortOrder?: number | null
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

/** 用户持有的组织项（与生成客户端 QueryUserOrgRolesCommandResponseItem 字段对齐） */
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

/** 查询组织全量平铺列表（仅管理员），树序输出（父先于子、同级按 sortOrder），前端按 parentId 组树 */
export async function getOrgRoles(search?: string): Promise<OrgRoleItem[]> {
  const client = getApiClient()
  const res = await client.api.org.role.list.get({
    queryParameters: { search: search?.trim() || undefined },
  })
  return res?.items ?? []
}

/** 创建组织（仅管理员），parentId 为空表示根角色 */
export async function createOrgRole(payload: { name: string; parentId?: number | null }): Promise<{ orgRoleId?: number | null; path?: string | null }> {
  const client = getApiClient()
  const res = await client.api.org.role.create.post({
    name: payload.name,
    parentId: payload.parentId ?? null,
  })
  return { orgRoleId: res?.orgRoleId, path: res?.path }
}

/** 更新组织（仅管理员）：改名与调整上级（null=调整为根角色），调整上级时整体重写子树路径，落位为新同级末尾 */
export async function updateOrgRole(roleId: number, payload: { name: string; parentId?: number | null }): Promise<void> {
  const client = getApiClient()
  await client.api.org.role.byId(roleId).put({
    name: payload.name,
    parentId: payload.parentId ?? null,
  })
}

/** 调整组织顺序（仅管理员）：挂在 parentId 之下、插到 prevOrgRoleId 之后（null=置于首位）；跨级移动时整体重写子树路径 */
export async function updateOrgRoleOrder(roleId: number, payload: { parentId: number | null; prevOrgRoleId: number | null }): Promise<void> {
  const client = getApiClient()
  await client.api.org.role.byId(roleId).order.put({
    parentId: payload.parentId,
    prevOrgRoleId: payload.prevOrgRoleId,
  })
}

/** 删除组织（仅管理员）：存在子组织时后端拒绝；同时解绑该组织下的全部用户 */
export async function deleteOrgRole(roleId: number): Promise<void> {
  const client = getApiClient()
  await client.api.org.role.byId(roleId).delete()
}

/** 查询某组织下的用户清单（仅管理员） */
export async function getOrgRoleUsers(roleId: number, search?: string): Promise<OrgRoleUserItem[]> {
  const client = getApiClient()
  const res = await client.api.org.role.byId(roleId).users.get({
    queryParameters: { search: search?.trim() || undefined },
  })
  return res?.items ?? []
}

/** 查询某用户持有的组织清单（仅管理员） */
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

/** 整体覆盖某用户的组织集合（仅管理员），空列表=清空 */
export async function updateUserOrgRoles(userId: number, roleIds: number[]): Promise<void> {
  const client = getApiClient()
  await client.api.org.user.byUserId(String(userId)).roles.put({ roleIds })
}
