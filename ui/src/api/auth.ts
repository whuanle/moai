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
  mfaPending?: boolean | null
}): UserInfo {
  return {
    accessToken: res.accessToken,
    expiresIn: res.expiresIn,
    refreshToken: res.refreshToken,
    tokenType: res.tokenType,
    userId: res.userId,
    userName: res.userName,
    mfaPending: res.mfaPending ?? false,
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

/** 登录二次验证挑战：密码校验通过后服务端要求第二步（认证器动态码 / 邮箱验证码） */
export interface LoginChallenge {
  secondFactor: 'totp' | 'emailCode'
  loginTicket: string
}

export function isLoginChallenge(res: unknown): res is LoginChallenge {
  return typeof res === 'object' && res !== null && 'loginTicket' in res && 'secondFactor' in res
}

/**
 * 用户名密码登录：返回 UserInfo 表示登录完成；返回 LoginChallenge 表示需二次验证
 * （secondFactor=emailCode 时服务端已向用户邮箱发送验证码，secondFactor=totp 时输入认证器动态码）.
 */
export async function login(userName: string, password: string): Promise<UserInfo | LoginChallenge | null> {
  const info = await getServerInfo()
  const encryptedPassword = rsaEncrypt(info.rsaPublic, password)
  const client = getAnonymousClient()
  const res = await client.api.auth.login.post({ userName, password: encryptedPassword })
  if (!res) return null
  if (res.secondFactor && res.secondFactor !== 'none') {
    return { secondFactor: res.secondFactor as 'totp' | 'emailCode', loginTicket: res.loginTicket ?? '' }
  }
  const userInfo = toUserInfo(res)
  useAppStore.getState().setUserInfo(userInfo)
  return userInfo
}

/** 登录二次验证：凭一次性登录票据提交 6 位动态码，通过后写入会话 */
export async function verifyLogin(loginTicket: string, code: string): Promise<UserInfo | null> {
  const client = getAnonymousClient()
  const res = await client.api.auth.login.verify.post({ loginTicket, code })
  if (!res) return null
  const userInfo = toUserInfo(res)
  useAppStore.getState().setUserInfo(userInfo)
  return userInfo
}

/** 重发登录邮箱验证码（凭一次性登录票据，60 秒冷却） */
export async function sendLoginCode(loginTicket: string): Promise<void> {
  const client = getAnonymousClient()
  await client.api.auth.login_code.send.post({ loginTicket })
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

/** 两步验证（2FA）状态 */
export interface TwoFactorStatus {
  enabled: boolean
  forced: boolean
}

/** 查询当前用户两步验证状态 */
export async function getTwoFactorStatus(): Promise<TwoFactorStatus> {
  const client = getApiClient()
  const res = await client.api.auth.two_factor.status.get()
  return { enabled: res?.enabled ?? false, forced: res?.forced ?? false }
}

/** 两步验证绑定密钥：secret 手动输入备用，otpauthUri 由前端渲染二维码 */
export interface TwoFactorSetup {
  secret: string
  otpauthUri: string
}

/** 生成两步验证绑定密钥（未激活，需随后用认证器动态码调 enableTwoFactor） */
export async function setupTwoFactor(): Promise<TwoFactorSetup> {
  const client = getApiClient()
  const res = await client.api.auth.two_factor.setup.post()
  return { secret: res?.secret ?? '', otpauthUri: res?.otpauthUri ?? '' }
}

/** 启用两步验证：校验认证器当前动态码后激活绑定 */
export async function enableTwoFactor(code: string): Promise<void> {
  const client = getApiClient()
  await client.api.auth.two_factor.enable.post({ code })
}

/** 关闭两步验证：校验当前动态码后解绑；系统强制开启时被拒 */
export async function disableTwoFactor(code: string): Promise<void> {
  const client = getApiClient()
  await client.api.auth.two_factor.disable.post({ code })
}
