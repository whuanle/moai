/**
 * 沙箱出站网络策略目标格式校验，与后端 SandboxNetworkTarget 语义一致：
 * 目标支持域名（含 *. 通配符）、IPv4/IPv6 地址或 CIDR 网段；端口 1~65535、单条不超过 16 个、去重。
 */

const MAX_TARGET_LENGTH = 253
const MAX_PORTS_PER_RULE = 16
export const MAX_RULES_PER_POLICY = 100

const DOMAIN_PATTERN = /^(\*\.)?[a-z0-9]([a-z0-9-]*[a-z0-9])?(\.[a-z0-9]([a-z0-9-]*[a-z0-9])?)+$/i

/** 粗检 IPv4：四段 0~255 */
function isValidIpv4(text: string): boolean {
  const parts = text.split('.')
  if (parts.length !== 4) return false
  return parts.every((part) => {
    if (!/^\d{1,3}$/.test(part)) return false
    const n = Number(part)
    return n >= 0 && n <= 255
  })
}

/** IPv6 前端只做字符集与分组粗检，完整展开规则由后端 SandboxNetworkTarget 复核 */
function isValidIpv6(text: string): boolean {
  if (!/^[0-9a-f:]+$/i.test(text)) return false
  return text.split(':').length >= 2
}

/** 校验规则目标（域名 / IP / CIDR）；返回 isIpOrCidr 标记是否为 IP / CIDR 目标（仅此类目标允许端口限制） */
export function parseNetworkTarget(value: string | null | undefined): { valid: boolean; isIpOrCidr: boolean } {
  const text = (value ?? '').trim()
  if (!text || text.length > MAX_TARGET_LENGTH) return { valid: false, isIpOrCidr: false }

  // CIDR：ip/前缀（字符串切分，避免与域名正则混淆）
  const slash = text.indexOf('/')
  if (slash >= 0) {
    if (text.indexOf('/', slash + 1) >= 0) return { valid: false, isIpOrCidr: true }
    const ip = text.slice(0, slash)
    const prefix = Number(text.slice(slash + 1))
    const isIpv6 = ip.includes(':')
    const maxPrefix = isIpv6 ? 128 : 32
    const valid =
      isValidIpv4(ip) ||
      (isIpv6 && isValidIpv6(ip))
    return { valid: valid && Number.isInteger(prefix) && prefix >= 0 && prefix <= maxPrefix, isIpOrCidr: true }
  }

  if (text.includes('.') || text.includes(':')) {
    if (isValidIpv4(text)) return { valid: true, isIpOrCidr: true }
    if (text.includes(':') && isValidIpv6(text)) return { valid: true, isIpOrCidr: true }
    if (text.includes(':')) return { valid: false, isIpOrCidr: false }
  }

  return { valid: DOMAIN_PATTERN.test(text), isIpOrCidr: false }
}

/** 校验单条规则的端口列表：1~65535、去重、数量不超过 16 */
export function isValidRulePorts(ports: number[]): boolean {
  if (ports.length > MAX_PORTS_PER_RULE) return false
  const seen = new Set<number>()
  for (const port of ports) {
    if (!Number.isInteger(port) || port < 1 || port > 65535 || seen.has(port)) return false
    seen.add(port)
  }
  return true
}

/** 校验出站网络管控模式取值（与后端 NetworkModes 一致） */
export function isValidNetworkMode(value: string | null | undefined): boolean {
  const normalized = value?.trim().toLowerCase()
  return normalized === 'app' || normalized === 'deny' || normalized === 'intranet'
}

/** 校验逗号分隔的内网网段设置：全部为合法 IP / CIDR 且非空 */
export function isValidIntranetCidrs(value: string): boolean {
  const parts = value
    .split(',')
    .map((x) => x.trim())
    .filter(Boolean)
  if (parts.length === 0) return false
  return parts.every((part) => parseNetworkTarget(part).isIpOrCidr)
}
