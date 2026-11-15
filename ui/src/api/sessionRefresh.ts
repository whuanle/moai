/**
 * 会话静默续期注册表。
 *
 * 请求层（`api/kiota.ts`）在鉴权接口返回 401 时需要先续期再重试，而续期实现依赖 Kiota 生成的
 * 匿名客户端（`api/auth.ts`）。两者直接互相 import 会形成循环依赖，故把「注册/调用」拆到本模块：
 * `api/auth.ts` 模块加载时注册实现，`api/kiota.ts` 在 401 时调用。
 */
type SessionRefreshHandler = () => Promise<boolean>

let handler: SessionRefreshHandler | null = null

/** 注册静默续期实现；传 null 注销（测试或登出场景可用） */
export function registerSessionRefreshHandler(next: SessionRefreshHandler | null): void {
  handler = next
}

/** 尝试静默续期；未注册实现时返回 false（调用方按原逻辑处理 401） */
export async function trySessionRefresh(): Promise<boolean> {
  if (!handler) return false
  return handler()
}
