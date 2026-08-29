import { jwtDecode } from 'jwt-decode'

interface JwtPayload {
  exp: number
  iat: number
}

export function isTokenExpired(token: string, graceSeconds = 60): boolean {
  try {
    const decoded = jwtDecode<JwtPayload>(token)
    const now = Math.floor(Date.now() / 1000)
    return decoded.exp < now - graceSeconds
  } catch {
    return true
  }
}
