const UNITS = ['B', 'KB', 'MB', 'GB', 'TB', 'PB']
const BASE = 1024

/** 将字节转换为人类可读的字符串，如 1.5 MB */
export function formatFileSize(bytes: number, decimals = 2): string {
  if (!bytes || bytes <= 0) return '0 B'
  const i = Math.min(Math.floor(Math.log(bytes) / Math.log(BASE)), UNITS.length - 1)
  const value = bytes / Math.pow(BASE, i)
  return `${parseFloat(value.toFixed(decimals))} ${UNITS[i]}`
}
