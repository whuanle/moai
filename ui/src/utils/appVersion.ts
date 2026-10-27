/**
 * 应用版本号工具：int 编码（aaa*10000 + bb*100 + cc，可直接比较大小）与 aaa.bb.cc 文本互转。
 * 与后端 AppVersionHelper 保持同一规则：首位 1~3 位数字，后两段各 1~2 位数字。
 */

/** 版本号文本格式：首位 1~3 位数字，后两段各 1~2 位数字（如 1.0.3、10.20.30） */
export const APP_VERSION_PATTERN = /^\d{1,3}\.\d{1,2}\.\d{1,2}$/

/** 应用默认版本号编码：1.0.0 */
export const APP_DEFAULT_VERSION = 10000

/** 把 int 编码格式化为 aaa.bb.cc 文本；空值返回空串 */
export function formatAppVersion(version?: number | null): string {
  if (version === null || version === undefined || !Number.isFinite(version)) return ''
  const aaa = Math.floor(version / 10000)
  const bb = Math.floor(version / 100) % 100
  const cc = version % 100
  return `${aaa}.${bb}.${cc}`
}

/** 解析 aaa.bb.cc 文本为 int 编码；格式非法返回 null */
export function parseAppVersion(text: string | null | undefined): number | null {
  if (!text || !APP_VERSION_PATTERN.test(text)) return null
  const [aaa, bb, cc] = text.split('.').map((x) => Number.parseInt(x, 10))
  if (!Number.isFinite(aaa) || !Number.isFinite(bb) || !Number.isFinite(cc)) return null
  return aaa * 10000 + bb * 100 + cc
}

/**
 * 计算严格递增的下一版本号编码：patch 段 +1，段满进位（如 0.0.1→0.0.2、0.0.99→0.1.0）.
 * 已到上限（999.99.99）返回 null（需用户手填有效更大版本）.
 */
export function nextAppVersion(version: number): number | null {
  const aaa = Math.floor(version / 10000)
  const bb = Math.floor(version / 100) % 100
  const cc = version % 100
  if (cc < 99) return version + 1
  if (bb < 99) return version + 100
  if (aaa < 999) return version + 10000
  return null
}
