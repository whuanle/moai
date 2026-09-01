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

/**
 * 字段级校验错误：`{ name, errors: string[] }` 的列表。
 * 与后端 BusinessValidationResult.errors 结构一致。
 */
export interface FieldError {
  name?: string | null
  errors?: string[] | null
}

/** 归一化后的 API 错误对象（同时携带状态码与业务错误信息）。 */
export type NormalizedApiError = Error & {
  status?: number
  responseStatusCode?: number
  detail?: string
  errors?: FieldError[] | null
}

function firstFieldError(err: unknown): string | undefined {
  if (!err || typeof err !== 'object') return undefined
  const e = err as { errors?: unknown }
  if (!Array.isArray(e.errors)) return undefined
  for (const item of e.errors) {
    if (!item || typeof item !== 'object') continue
    const errors = (item as { errors?: unknown }).errors
    if (!Array.isArray(errors) || errors.length === 0) continue
    const first = errors[0]
    if (typeof first === 'string' && first.trim() !== '') return first
  }
  return undefined
}

/**
 * 提取更适合展示的错误文案，优先级：业务 `detail` > 首个字段校验错误 > `message`。
 * 用于统一拦截后向用户展示后端真实返回的错误信息（等价旧版 proxyRequestError）。
 */
export function resolveErrorMessage(err: unknown): string | undefined {
  if (!err || typeof err !== 'object') return undefined
  const e = err as { detail?: unknown }
  if (typeof e.detail === 'string' && e.detail.trim() !== '') return e.detail
  const field = firstFieldError(err)
  if (field) return field
  return extractErrorMessage(err)
}

function tryParseJson(text: string): unknown {
  try {
    return JSON.parse(text)
  } catch {
    return undefined
  }
}

/**
 * 读取非 2xx 响应体并归一化为标准错误对象（带 status/detail/errors/message）。
 * 响应体被消费后无法再被 Kiota 重新解析，因此调用方应直接抛出该错误。
 */
export async function parseApiErrorResponse(response: Response): Promise<NormalizedApiError> {
  const status = response.status
  let body: unknown
  try {
    const text = await response.text()
    body = text ? tryParseJson(text) : undefined
  } catch {
    body = undefined
  }

  const record = body && typeof body === 'object' ? (body as Record<string, unknown>) : undefined
  const detail = typeof record?.detail === 'string' ? record.detail : undefined
  const errors = Array.isArray(record?.errors) ? (record.errors as FieldError[]) : undefined
  const message = detail ?? extractErrorMessage(body)

  const error = new Error(message ?? '') as NormalizedApiError
  error.status = status
  error.responseStatusCode = status
  error.detail = detail
  error.errors = errors ?? null
  return error
}
