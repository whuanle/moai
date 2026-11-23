/**
 * 第三方绑定弹窗与主页面之间的跨窗口消息类型.
 */
export type OAuthBindMessage =
  | { type: 'oauth_bind_success'; oAuthId?: string | null }
  | { type: 'oauth_bind_error'; oAuthId?: string | null; message?: string | null }
  | { type: 'oauth_bind_cancel'; oAuthId?: string | null }

/**
 * 绑定弹窗特征常量（已加载 OAuth state 时附加的标志）.
 */
export const OAUTH_BIND_STATE_FLAG = 'bind'

/**
 * 解析回调地址中的 OAuth state.
 * <para>
 * 后端默认返回 `state={OAuthId}`；
 * 绑定弹窗在跳转前会把 state 重写为 `state={OAuthId}:bind`，
 * 以便回调时明确走「绑定当前登录账号并关闭弹窗」的分支。
 * </para>
 */
export function parseOAuthState(state: string | null): {
  oAuthId: string
  bind: boolean
} {
  if (!state) return { oAuthId: '', bind: false }
  const separator = state.lastIndexOf(':')
  if (separator === -1) {
    return { oAuthId: state, bind: state === OAUTH_BIND_STATE_FLAG }
  }
  const oAuthId = state.slice(0, separator)
  const flag = state.slice(separator + 1)
  return { oAuthId, bind: flag === OAUTH_BIND_STATE_FLAG }
}

/**
 * 将后端返回的授权地址重写为绑定地址（在 state 上附加 `:bind` 标志）.
 * <para>
 * 仅在从「账号设置页」打开绑定弹窗时使用；登录页仍使用原始授权地址，互不影响。
 * </para>
 */
export function toBindAuthorizeUrl(url: string | null | undefined): string {
  if (!url) return ''
  try {
    const parsed = new URL(url)
    const state = parsed.searchParams.get('state')
    if (state) {
      parsed.searchParams.set('state', `${state}:${OAUTH_BIND_STATE_FLAG}`)
    }
    return parsed.toString()
  } catch {
    return url
  }
}

/**
 * 登录后回跳目标暂存键（sessionStorage）.
 * <para>
 * 登录页携带 `redirect` 参数时，走第三方 OAuth 登录会离开本站再回跳，
 * URL 参数无法跨第三方授权流程保留，因此在离开前暂存，
 * 回调页登录/注册成功后消费并回跳（与密码登录读 URL 参数同口径）。
 * </para>
 */
export const PENDING_REDIRECT_STORAGE_KEY = 'moai:pendingRedirect'

/**
 * 校验登录后回跳目标：仅允许本站相对路径（`/` 开头且非 `//` 协议相对地址），防止开放重定向.
 */
export function isSafeRedirectTarget(target: string | null | undefined): target is string {
  return !!target && target.startsWith('/') && !target.startsWith('//')
}

/**
 * 暂存登录后回跳目标（仅接受本站相对路径）.
 */
export function stashPendingRedirect(target: string | null | undefined): void {
  if (!isSafeRedirectTarget(target)) return
  try {
    sessionStorage.setItem(PENDING_REDIRECT_STORAGE_KEY, target)
  } catch {
    // 隐私模式等场景下 sessionStorage 不可用：放弃暂存，登录后回应用广场
  }
}

/**
 * 消费暂存的登录后回跳目标：读取即清除，仅返回安全目标（不安全或不存在返回 null）.
 */
export function consumePendingRedirect(): string | null {
  try {
    const target = sessionStorage.getItem(PENDING_REDIRECT_STORAGE_KEY)
    sessionStorage.removeItem(PENDING_REDIRECT_STORAGE_KEY)
    return isSafeRedirectTarget(target) ? target : null
  } catch {
    return null
  }
}

/**
 * 清除暂存的登录后回跳目标（不读取）.
 */
export function clearPendingRedirect(): void {
  try {
    sessionStorage.removeItem(PENDING_REDIRECT_STORAGE_KEY)
  } catch {
    // 忽略存储不可用
  }
}

/**
 * 向主页面通知绑定结果.
 * <para>
 * 仅在弹窗模式（存在 opener 且同源）下有效；否则不发送。
 * </para>
 */
export function notifyOAuthBindResult(
  message: OAuthBindMessage,
): { delivered: boolean; popup: boolean } {
  const popup = window.opener != null
  if (popup) {
    try {
      const openerOrigin = (window.opener as Window).origin
      if (openerOrigin === window.location.origin) {
        window.opener.postMessage(message, window.location.origin)
        return { delivered: true, popup }
      }
    } catch {
      // 跨源或已关闭时忽略
    }
    return { delivered: false, popup }
  }
  return { delivered: false, popup }
}
