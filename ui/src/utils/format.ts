const UNITS = ['B', 'KB', 'MB', 'GB', 'TB', 'PB']
const BASE = 1024

/** 将字节转换为人类可读的字符串，如 1.5 MB */
export function formatFileSize(bytes: number, decimals = 2): string {
  if (!bytes || bytes <= 0) return '0 B'
  const i = Math.min(Math.floor(Math.log(bytes) / Math.log(BASE)), UNITS.length - 1)
  const value = bytes / Math.pow(BASE, i)
  return `${parseFloat(value.toFixed(decimals))} ${UNITS[i]}`
}

/** 将 token 数量转为紧凑展示：980 / 12.4k / 128k / 1.2M（用于上下文占用等窄空间场景） */
export function formatTokenCount(tokens: number): string {
  if (!tokens || tokens <= 0) return '0'
  if (tokens >= 1_000_000) return `${parseFloat((tokens / 1_000_000).toFixed(1))}M`
  if (tokens >= 10_000) return `${Math.round(tokens / 1_000)}k`
  if (tokens >= 1_000) return `${parseFloat((tokens / 1_000).toFixed(1))}k`
  return String(tokens)
}

