import { jwtDecode } from 'jwt-decode'

interface JwtPayload {
  exp: number
  iat: number
}

/**
 * 判断 token 是否过期或即将过期。
 * @param token JWT.
 * @param skewSeconds 提前量（秒）：剩余有效期不足该值时也视为"需要续期"，用于在令牌真正过期前静默换新；传 0 只判断真实过期。
 * @returns 解码失败一律视为过期（宁可多刷一次，也不带着坏 token 发请求）.
 */
export function isTokenExpired(token: string, skewSeconds = 0): boolean {
  try {
    const decoded = jwtDecode<JwtPayload>(token)
    const now = Math.floor(Date.now() / 1000)
    return decoded.exp - now <= skewSeconds
  } catch {
    return true
  }
}
