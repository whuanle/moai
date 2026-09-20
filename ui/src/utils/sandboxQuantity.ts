/**
 * K8s 数量格式解析（沙箱 CPU / 内存限制），与后端 SandboxQuantity 语义一致：
 * CPU 解析为毫核（1 核 = 1000m），内存解析为字节（Ki/Mi/Gi 二进制 1024 进制，K/M/G 十进制 1000 进制）。
 */

const CPU_PATTERN = /^(\d+(?:\.\d+)?)(m)?$/
const MEMORY_PATTERN = /^(\d+(?:\.\d+)?)(Ki|Mi|Gi|Ti|Pi|Ei|K|M|G|T|P|E|B)?$/

const MEMORY_MULTIPLIERS: Record<string, number> = {
  '': 1,
  B: 1,
  Ki: 1024,
  Mi: 1024 ** 2,
  Gi: 1024 ** 3,
  Ti: 1024 ** 4,
  Pi: 1024 ** 5,
  Ei: 1024 ** 6,
  K: 1e3,
  M: 1e6,
  G: 1e9,
  T: 1e12,
  P: 1e15,
  E: 1e18,
}

/** 解析 CPU 数量为毫核；格式非法返回 null */
export function parseCpuMillicores(value: string): number | null {
  const text = value.trim()
  const match = CPU_PATTERN.exec(text)
  if (!match) return null
  const amount = Number(match[1])
  if (!Number.isFinite(amount) || amount < 0) return null
  return match[2] === 'm' ? amount : amount * 1000
}

/** 解析内存数量为字节（后缀区分大小写）；格式非法返回 null */
export function parseMemoryBytes(value: string): number | null {
  const match = MEMORY_PATTERN.exec(value.trim())
  if (!match) return null
  const multiplier = MEMORY_MULTIPLIERS[match[2] ?? '']
  const amount = Number(match[1])
  if (multiplier == null || !Number.isFinite(amount) || amount < 0) return null
  return amount * multiplier
}
