import { getAnonymousClient, getApiClient } from '@/api/kiota'
import { useAppStore, type ServerInfo, type UserInfo } from '@/store/app'
import { isTokenExpired } from '@/utils/jwt'
import { rsaEncrypt } from '@/utils/rsa'

function toUserInfo(res: {
  accessToken?: string | null
  expiresIn?: string | null
  refreshToken?: string | null
  tokenType?: string | null
  userId?: number | null
  userName?: string | null
}): UserInfo {
  return {
    accessToken: res.accessToken,
    expiresIn: res.expiresIn,
    refreshToken: res.refreshToken,
    tokenType: res.tokenType,
    userId: res.userId,
    userName: res.userName,
  }
}

export async function refreshServerInfo(): Promise<ServerInfo> {
  const client = getAnonymousClient()
  const res = await client.api.common.serverinfo.get()
  const info: ServerInfo = {
    serviceUrl: res?.serviceUrl ?? '',
    publicStoreUrl: res?.publicStoreUrl ?? '',
    rsaPublic: res?.rsaPublic ?? '',
  }
  useAppStore.getState().setServerInfo(info)
  return info
}

export async function getServerInfo(): Promise<ServerInfo> {
  const current = useAppStore.getState().serverInfo
  if (current) return current
  return refreshServerInfo()
}

export async function login(userName: string, password: string): Promise<UserInfo | null> {
  const info = await getServerInfo()
  const encryptedPassword = rsaEncrypt(info.rsaPublic, password)
  const client = getAnonymousClient()
  const res = await client.api.account.login.post({ userName, password: encryptedPassword })
  if (!res) return null
  const userInfo = toUserInfo(res)
  useAppStore.getState().setUserInfo(userInfo)
  return userInfo
}

export async function register(payload: {
  userName: string
  email: string
  nickName?: string
  phone?: string
  password: string
}): Promise<void> {
  const info = await getServerInfo()
  const encryptedPassword = rsaEncrypt(info.rsaPublic, payload.password)
  const client = getAnonymousClient()
  await client.api.account.register.post({
    userName: payload.userName,
    email: payload.email,
    nickName: payload.nickName,
    phone: payload.phone,
    password: encryptedPassword,
  })
}

export async function refreshAccessToken(refreshToken: string): Promise<UserInfo | null> {
  const client = getAnonymousClient()
  const res = await client.api.account.refresh_token.post({ refreshToken })
  if (!res) return null
  const userInfo = toUserInfo(res)
  useAppStore.getState().setUserInfo(userInfo)
  return userInfo
}

export async function getUserDetailInfo() {
  const client = getApiClient()
  return client.api.common.userinfo.get()
}

export async function checkToken(): Promise<boolean> {
  const userInfo = useAppStore.getState().userInfo
  if (!userInfo?.accessToken) return false
  if (!isTokenExpired(userInfo.accessToken)) return true
  if (!userInfo.refreshToken || isTokenExpired(userInfo.refreshToken)) return false
  const refreshed = await refreshAccessToken(userInfo.refreshToken)
  return refreshed !== null
}
