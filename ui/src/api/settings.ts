import { getApiClient } from '@/api/kiota'
import { uploadImageWithKey } from '@/utils/storage'

export const SettingKeys = {
  kgEnabled: 'KG_ENABLED',
  kgUri: 'KG_URI',
  kgUsername: 'KG_USERNAME',
  kgPassword: 'KG_PASSWORD',
  kgDialect: 'KG_DIALECT',
  wikiMaxFileSize: 'WIKI_MAX_FILE_SIZE_MB',
  sandboxMaxTtl: 'SANDBOX_MAX_TTL_SECONDS',
  sandboxMaxCpu: 'SANDBOX_MAX_CPU',
  sandboxMaxMemory: 'SANDBOX_MAX_MEMORY',
  systemLogo: 'SYSTEM_LOGO',
  systemName: 'SYSTEM_NAME',
} as const

/** 沙箱上限内置默认值（与后端 SettingDefinitions 一致），设置项缺失/非法时回退 */
export const SandboxLimitDefaults = {
  maxTtlSeconds: 86400,
  maxCpu: '4',
  maxMemory: '8Gi',
} as const

/** 沙箱存活时间上限范围（秒），与后端 SandboxSettingsService 常量一致 */
export const SANDBOX_TTL_LIMITS = { min: 60, max: 604800 } as const

export async function getSettings() {
  const client = getApiClient()
  return client.api.settings.get()
}

export async function saveSetting(key: string, value: string): Promise<void> {
  const client = getApiClient()
  await client.api.settings.put({ key, value })
}

/** 上传图片并替换全局网站 Logo，返回 ObjectKey（上传即生效，无需单独保存） */
export async function updateSystemLogo(file: File): Promise<string> {
  const client = getApiClient()
  const { objectKey } = await uploadImageWithKey(file)
  await client.api.settings.logo.post({ objectKey })
  return objectKey
}

/** 清空网站 Logo，恢复前端默认 Logo */
export async function resetSystemLogo(): Promise<void> {
  const client = getApiClient()
  await client.api.settings.logo.post({ objectKey: '' })
}
