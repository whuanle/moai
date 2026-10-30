import { getAnonymousClient, getApiClient } from '@/api/kiota'
import { useAppStore, type ServerInfo, type UserInfo } from '@/store/app'
import { isTokenExpired } from '@/utils/jwt'
import { rsaEncrypt } from '@/utils/rsa'

function toUserInfo(res: {
  accessToken?: string | null
  expiresIn?: string | null
  refreshToken?: string | null
  tokenType?: string | null
  userId?: string | null | undefined
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
    logoPath: res?.logoPath ?? '',
    loginBackgroundPath: res?.loginBackgroundPath ?? '',
    name: res?.name ?? '',
    registerEmailCodeRequired: res?.registerEmailCodeRequired ?? false,
    passwordResetEnabled: res?.passwordResetEnabled ?? false,
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
  const res = await client.api.auth.login.post({ userName, password: encryptedPassword })
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
  /** 邮箱验证码（服务端开启注册验证码时必填） */
  emailCode?: string
}): Promise<void> {
  const info = await getServerInfo()
  const encryptedPassword = rsaEncrypt(info.rsaPublic, payload.password)
  const client = getAnonymousClient()
  await client.api.auth.register.post({
    userName: payload.userName,
    email: payload.email,
    nickName: payload.nickName,
    phone: payload.phone,
    password: encryptedPassword,
    emailCode: payload.emailCode,
  })
}

/** 发送邮箱验证码（注册 / 找回密码；服务端校验开关与 SMTP 配置） */
export async function sendEmailCode(email: string, purpose: 'register' | 'resetPassword'): Promise<void> {
  const client = getAnonymousClient()
  await client.api.auth.email_code.send.post({ email, purpose })
}

/** 通过邮箱验证码重置密码（找回密码；需 SMTP 邮件服务已启用） */
export async function resetPasswordByEmail(email: string, code: string, newPassword: string): Promise<void> {
  const info = await getServerInfo()
  const encryptedPassword = rsaEncrypt(info.rsaPublic, newPassword)
  const client = getAnonymousClient()
  await client.api.auth.reset_password.post({ email, code, newPassword: encryptedPassword })
}

export async function refreshAccessToken(refreshToken: string): Promise<UserInfo | null> {
  const client = getAnonymousClient()
  const res = await client.api.auth.refresh_token.post({ refreshToken })
  if (!res) return null
  const userInfo = toUserInfo(res)
  useAppStore.getState().setUserInfo(userInfo)
  return userInfo
}

export function applyLoginResponse(
  res: {
    accessToken?: string | null
    expiresIn?: string | null
    refreshToken?: string | null
    tokenType?: string | null
    userId?: string | null | undefined
    userName?: string | null
  },
): UserInfo {
  const userInfo = toUserInfo(res)
  useAppStore.getState().setUserInfo(userInfo)
  return userInfo
}

export async function oauthLogin(payload: { code: string; oAuthId: string; redirectUrl?: string }) {
  const client = getAnonymousClient()
  return client.api.auth.oauth_login.post({ code: payload.code, oAuthId: payload.oAuthId, redirectUrl: payload.redirectUrl })
}

export async function oauthRegister(tempOAuthBindId: string): Promise<UserInfo | null> {
  const client = getAnonymousClient()
  const res = await client.api.auth.oauth_register.post({ tempOAuthBindId })
  if (!res) return null
  return applyLoginResponse(res)
}

export interface OAuthProviderItem {
  oAuthId?: string | null
  name?: string | null
  iconUrl?: string | null
  provider?: string | null
  redirectUrl?: string | null
}

export async function getOAuthProviders(redirectUrl?: string): Promise<OAuthProviderItem[]> {
  const client = getAnonymousClient()
  // 回调地址跟随登录页实际访问来源（内网穿透等场景与后端 WebUI 配置不同）
  const res = await client.api.auth.oauth_prividers.get({ queryParameters: { redirectUrl } })
  return res?.items ?? []
}

export async function getUserDetailInfo() {
  const client = getApiClient()
  return client.api.account.userinfo.get()
}

export async function oauthBindAccount(tempOAuthBindId: string): Promise<void> {
  const client = getApiClient()
  await client.api.account.oauth_bind_account.post({ tempOAuthBindId })
}

export async function refreshUserProfile(): Promise<UserInfo | null> {
  const res = await getUserDetailInfo()
  if (!res) return null
  const current = useAppStore.getState().userInfo ?? {}
  const merged: UserInfo = { ...current, ...res }
  useAppStore.getState().setUserInfo(merged)
  return merged
}

export async function checkToken(): Promise<boolean> {
  const userInfo = useAppStore.getState().userInfo
  if (!userInfo?.accessToken) return false
  if (!isTokenExpired(userInfo.accessToken)) return true
  if (!userInfo.refreshToken || isTokenExpired(userInfo.refreshToken)) return false
  const refreshed = await refreshAccessToken(userInfo.refreshToken)
  return refreshed !== null
}
