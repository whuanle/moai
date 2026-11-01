import { getApiClient } from '@/api/kiota'
import { uploadImageWithKey } from '@/utils/storage'

export const SettingKeys = {
  kgEnabled: 'KG_ENABLED',
  kgUri: 'KG_URI',
  kgUsername: 'KG_USERNAME',
  kgPassword: 'KG_PASSWORD',
  kgDialect: 'KG_DIALECT',
  wikiMaxFileSize: 'WIKI_MAX_FILE_SIZE_MB',
  wikiAllowedFileTypes: 'WIKI_ALLOWED_FILE_TYPES',
  sandboxMaxTtl: 'SANDBOX_MAX_TTL_SECONDS',
  sandboxMaxCpu: 'SANDBOX_MAX_CPU',
  sandboxMaxMemory: 'SANDBOX_MAX_MEMORY',
  systemLogo: 'SYSTEM_LOGO',
  systemName: 'SYSTEM_NAME',
  loginBackground: 'LOGIN_BACKGROUND',
  smtpEnabled: 'SMTP_ENABLED',
  smtpHost: 'SMTP_HOST',
  smtpPort: 'SMTP_PORT',
  smtpSecurity: 'SMTP_SECURITY',
  smtpUsername: 'SMTP_USERNAME',
  smtpPassword: 'SMTP_PASSWORD',
  smtpFromAddress: 'SMTP_FROM_ADDRESS',
  smtpFromName: 'SMTP_FROM_NAME',
  registerEmailCodeEnabled: 'REGISTER_EMAIL_CODE_ENABLED',
  smtpRegisterCodeSubject: 'SMTP_REGISTER_CODE_SUBJECT',
  smtpRegisterCodeBody: 'SMTP_REGISTER_CODE_BODY',
  smtpResetCodeSubject: 'SMTP_RESET_CODE_SUBJECT',
  smtpResetCodeBody: 'SMTP_RESET_CODE_BODY',
  loginForced2fa: 'LOGIN_FORCED_2FA',
  loginEmailCodeMode: 'LOGIN_EMAIL_CODE_MODE',
  smtpLoginCodeSubject: 'SMTP_LOGIN_CODE_SUBJECT',
  smtpLoginCodeBody: 'SMTP_LOGIN_CODE_BODY',
  memoryEnabled: 'MEMORY_ENABLED',
  memoryEmbeddingModel: 'MEMORY_EMBEDDING_MODEL',
  memoryEmbeddingDimensions: 'MEMORY_EMBEDDING_DIMENSIONS',
  statisticsGranularity: 'STATISTICS_GRANULARITY',
  lsEnabled: 'LS_ENABLED',
  lsNotifyUserEnabled: 'LS_NOTIFY_USER_ENABLED',
  lsNotifyAdminEnabled: 'LS_NOTIFY_ADMIN_ENABLED',
  lsAdminRecipients: 'LS_ADMIN_ALERT_RECIPIENTS',
  lsIpFailThreshold: 'LS_IP_FAIL_THRESHOLD',
  lsIpFailWindowMinutes: 'LS_IP_FAIL_WINDOW_MINUTES',
  lsIpBanMinutes: 'LS_IP_BAN_MINUTES',
  lsDistinctUserThreshold: 'LS_DISTINCT_USER_THRESHOLD',
  lsNewIpDays: 'LS_NEW_IP_DAYS',
  lsTravelWindowMinutes: 'LS_TRAVEL_WINDOW_MINUTES',
  lsUserAlertSubject: 'LS_USER_ALERT_SUBJECT',
  lsUserAlertBody: 'LS_USER_ALERT_BODY',
  lsAdminAlertSubject: 'LS_ADMIN_ALERT_SUBJECT',
  lsAdminAlertBody: 'LS_ADMIN_ALERT_BODY',
} as const

/** 记忆向量维度内置默认值（与后端 SettingDefinitions 一致），设置项缺失时回退 */
export const MEMORY_DEFAULT_DIMENSIONS = 1024

/** SMTP 邮件服务内置默认值（与后端 SettingDefinitions 一致），设置项缺失时回退 */
export const SMTP_DEFAULTS = {
  port: 465,
  security: 'ssl',
} as const

/** SMTP 加密方式可选项（与后端校验一致） */
export const SMTP_SECURITY_OPTIONS = ['ssl', 'starttls', 'none'] as const
export type SmtpSecurity = (typeof SMTP_SECURITY_OPTIONS)[number]

/** 登录邮箱验证码策略可选项（与后端校验一致）：off=关闭 / always=始终需要 / after_fail=密码错误一次后需要 */
export const LOGIN_EMAIL_CODE_MODE_OPTIONS = ['off', 'always', 'after_fail'] as const
export type LoginEmailCodeMode = (typeof LOGIN_EMAIL_CODE_MODE_OPTIONS)[number]

/** 知识库允许上传文件类型的内置默认值（与后端 SettingDefinitions 一致），设置项缺失/非法时回退 */
export const WIKI_DEFAULT_FILE_TYPES = ['.pdf', '.docx', '.xlsx', '.pptx', '.html', '.htm', '.md', '.markdown', '.txt', '.json']

/** 单个扩展名合法形态：点号开头 + 1~16 位字母/数字（与后端 WikiFileTypes 校验一致） */
const WIKI_FILE_TYPE_PATTERN = /^\.[a-z0-9]{1,16}$/

/** 最多允许配置的扩展名条数（与后端一致） */
const WIKI_FILE_TYPE_MAX_COUNT = 100

/** 解析逗号分隔的扩展名配置为归一列表（小写、去重、剔除非法项）；为空或全部非法时回退内置默认 */
export function parseWikiFileTypes(value: string | null | undefined): string[] {
  if (!value || !value.trim()) {
    return [...WIKI_DEFAULT_FILE_TYPES]
  }
  const types: string[] = []
  for (const token of value.split(',')) {
    const ext = token.trim().toLowerCase()
    if (WIKI_FILE_TYPE_PATTERN.test(ext) && !types.includes(ext)) {
      types.push(ext)
    }
  }
  return types.length > 0 ? types : [...WIKI_DEFAULT_FILE_TYPES]
}

/** 校验并归一待提交的扩展名列表；为空/超量/存在非法或重复项时返回 null（前端拦截不提交） */
export function normalizeWikiFileTypes(types: string[]): string[] | null {
  if (types.length === 0 || types.length > WIKI_FILE_TYPE_MAX_COUNT) {
    return null
  }
  const result: string[] = []
  for (const token of types) {
    const ext = token.trim().toLowerCase()
    if (!WIKI_FILE_TYPE_PATTERN.test(ext) || result.includes(ext)) {
      return null
    }
    result.push(ext)
  }
  return result
}

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

/** 上传图片并替换登录页背景图，返回 ObjectKey（上传即生效，无需单独保存） */
export async function updateLoginBackground(file: File): Promise<string> {
  const client = getApiClient()
  const { objectKey } = await uploadImageWithKey(file)
  await client.api.settings.loginBackground.post({ objectKey })
  return objectKey
}

/** 清空登录页背景图，恢复前端默认背景 */
export async function resetLoginBackground(): Promise<void> {
  const client = getApiClient()
  await client.api.settings.loginBackground.post({ objectKey: '' })
}

/** 发送测试邮件（使用已保存的 SMTP 配置），验证邮件服务配置是否可用 */
export async function sendTestEmail(to: string): Promise<void> {
  const client = getApiClient()
  await client.api.settings.email.test.post({ to })
}
