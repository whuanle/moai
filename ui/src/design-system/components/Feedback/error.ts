const KIOTA_GENERIC_PATTERN = /the server returned an unexpected status code/i
const NETWORK_PATTERN = /failed to fetch|networkerror|load failed|fetch failed/i

/** 从抛出对象或 Response 中提取 HTTP 状态码。 */
export function getHttpStatus(err: unknown): number | undefined {
  if (err instanceof Response) return err.status
  if (!err || typeof err !== 'object') return undefined
  const e = err as Record<string, unknown>
  const candidates = [e.responseStatusCode, e.responseStatus, e.status]
  for (const code of candidates) if (typeof code === 'number' && code >= 100) return code
  return undefined
}

/** 是否为纯网络异常（无 HTTP 状态码时判定）。 */
export function isNetworkError(err: unknown): boolean {
  if (getHttpStatus(err) !== undefined) return false
  if (!err || typeof err !== 'object') return false
  const e = err as { name?: unknown; message?: unknown }
  if (e.name === 'TypeError') return true
  const msg = typeof e.message === 'string' ? e.message : ''
  return NETWORK_PATTERN.test(msg)
}

/** 提取服务端返回的错误文案，过滤掉 Kiota 的默认错误描述与网络错误文案。 */
export function extractErrorMessage(err: unknown): string | undefined {
  if (!err || typeof err !== 'object') return undefined
  const msg = (err as { message?: unknown }).message
  if (typeof msg !== 'string' || msg.trim() === '') return undefined
  if (KIOTA_GENERIC_PATTERN.test(msg)) return undefined
  if (NETWORK_PATTERN.test(msg)) return undefined
  return msg
}
