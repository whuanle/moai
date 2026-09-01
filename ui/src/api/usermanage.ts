import { getApiClient } from '@/api/kiota'
import { getServerInfo } from '@/api/auth'
import { rsaEncrypt } from '@/utils/rsa'

export interface UserListItem {
  /** Kiota 将 64 位整数映射为字符串 */
  id?: string | number | null
  userName?: string | null
  nickName?: string | null
  email?: string | null
  phone?: string | null
  avatar?: string | null
  isAdmin?: boolean | null
  isRoot?: boolean | null
  isDisable?: boolean | null
  createTime?: string | null
}

export interface UserListResult {
  totalCount?: number | null
  items?: UserListItem[] | null
}

export interface GetUsersParams {
  pageNo?: number
  pageSize?: number
  searchText?: string
}

export async function getUsers(params: GetUsersParams): Promise<UserListResult> {
  const client = getApiClient()
  const res = await client.api.usermanage.users.get({
    queryParameters: {
      pageNo: params.pageNo,
      pageSize: params.pageSize,
      searchText: params.searchText,
    },
  })
  return { totalCount: res?.totalCount ?? 0, items: res?.items ?? [] }
}

export async function getUserDetail(id: number) {
  const client = getApiClient()
  return client.api.usermanage.user.byId(String(id)).get()
}

export async function setUserAdmin(id: number, isAdmin: boolean): Promise<void> {
  const client = getApiClient()
  await client.api.usermanage.user.byId(String(id)).isadmin.put({ isAdmin })
}

export async function setUserDisable(id: number, isDisable: boolean): Promise<void> {
  const client = getApiClient()
  await client.api.usermanage.user.byId(String(id)).isdisable.put({ isDisable })
}

export async function resetUserPassword(id: number, newPassword: string): Promise<void> {
  const info = await getServerInfo()
  const client = getApiClient()
  await client.api.usermanage.user
    .byId(String(id))
    .password.put({ newPassword: rsaEncrypt(info.rsaPublic, newPassword) })
}
