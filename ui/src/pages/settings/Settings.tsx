import { useEffect, useState } from 'react'
import { Button, Divider, Input, InputNumber, Popconfirm, Progress, Radio, Select, Spin, Switch, Typography, Upload, theme as antdTheme } from 'antd'
import { CaretRightOutlined, UploadOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { Navigate } from 'react-router'
import { refreshServerInfo } from '@/api/auth'
import { getNotificationChannels, getNotificationTemplates } from '@/api/notification'
import {
  clearAllMemory,
  getGlobalMemoryModelOptions,
  getMemoryModelMigrationStatus,
  startMemoryModelMigration,
  type GlobalMemoryModelOption,
  type MemoryMigrationStatus,
} from '@/api/memory'
import {
  getSettings,
  LOGIN_CAPTCHA_MODE_OPTIONS,
  LOGIN_EMAIL_CODE_MODE_OPTIONS,
  MEMORY_DEFAULT_DIMENSIONS,
  normalizeWikiFileTypes,
  parseWikiFileTypes,
  SANDBOX_NETWORK_MODE_OPTIONS,
  SANDBOX_TTL_LIMITS,
  saveSetting,
  SandboxLimitDefaults,
  SettingKeys,
  WIKI_DEFAULT_FILE_TYPES,
  resetLoginBackground,
  resetSystemLogo,
  updateLoginBackground,
  updateSystemLogo,
  type LoginCaptchaMode,
  type LoginEmailCodeMode,
} from '@/api/settings'
import { AvatarUpload, Card, feedback, useNeutralColors, Page, spacing } from '@/design-system'
import { radius } from '@/design-system/theme'
import { DEFAULT_LOGO_SRC } from '@/layouts/useSystemLogo'
import { useAppStore } from '@/store/app'
import { parseCpuMillicores, parseMemoryBytes } from '@/utils/sandboxQuantity'
import { isValidIntranetCidrs, isValidNetworkMode } from '@/utils/sandboxNetwork'
import { isValidCommandPatternList } from '@/utils/sandboxCommand'
import { resolveStorageUrl } from '@/utils/storage'

const { Text } = Typography

interface SettingItem {
  key?: string | null
  value?: string | null
}

interface FieldProps {
  label: string
  children: React.ReactNode
}

function Field({ label, children }: FieldProps) {
  return (
    <div>
      <div style={{ marginBottom: spacing.xs }}>
        <Text strong>{label}</Text>
      </div>
      <div style={{ display: 'flex', flexDirection: 'column', gap: spacing.xs }}>{children}</div>
    </div>
  )
}

interface CollapsibleCardProps {
  title: string
  defaultExpanded?: boolean
  children: React.ReactNode
}

function CollapsibleCard({ title, defaultExpanded = false, children }: CollapsibleCardProps) {
  const [expanded, setExpanded] = useState(defaultExpanded)
  const neutral = useNeutralColors()

  return (
    <Card
      title={
        <div
          role="button"
          tabIndex={0}
          aria-expanded={expanded}
          style={{ display: 'flex', alignItems: 'center', gap: spacing.xs, cursor: 'pointer' }}
          onClick={() => setExpanded((v) => !v)}
          onKeyDown={(e) => {
            if (e.key === 'Enter' || e.key === ' ') {
              e.preventDefault()
              setExpanded((v) => !v)
            }
          }}
        >
          <CaretRightOutlined
            rotate={expanded ? 90 : 0}
            style={{ color: neutral.textTertiary, fontSize: 12 }}
          />
          <span>{title}</span>
        </div>
      }
      styles={expanded ? undefined : { body: { display: 'none' } }}
    >
      {expanded && children}
    </Card>
  )
}

interface BackgroundUploadBoxProps {
  src: string
  uploading: boolean
  placeholder: string
  uploadTip: string
  onSelect: (file: File) => void
}

/** 登录页背景图上传入口：宽幅预览图即点击上传区，悬停显示遮罩，上传中显示加载. */
function BackgroundUploadBox({ src, uploading, placeholder, uploadTip, onSelect }: BackgroundUploadBoxProps) {
  const { token } = antdTheme.useToken()
  const [hovering, setHovering] = useState(false)
  const showMask = hovering || uploading

  return (
    <Upload
      beforeUpload={(file) => {
        onSelect(file)
        return Upload.LIST_IGNORE
      }}
      showUploadList={false}
      accept="image/*"
      disabled={uploading}
    >
      <div
        role="button"
        aria-label={uploadTip}
        title={uploadTip}
        onMouseEnter={() => setHovering(true)}
        onMouseLeave={() => setHovering(false)}
        style={{
          position: 'relative',
          width: 216,
          height: 120,
          flexShrink: 0,
          borderRadius: radius.lg,
          overflow: 'hidden',
          cursor: uploading ? 'not-allowed' : 'pointer',
          border: `1px solid ${token.colorBorderSecondary}`,
          background: src
            ? undefined
            : `linear-gradient(180deg, ${token.colorBgLayout} 0%, ${token.colorPrimaryBg} 100%)`,
        }}
      >
        {src ? (
          <img src={src} alt="" style={{ width: '100%', height: '100%', objectFit: 'cover', display: 'block' }} />
        ) : (
          <span
            style={{
              position: 'absolute',
              inset: 0,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: token.colorTextTertiary,
              fontSize: 12,
            }}
          >
            {placeholder}
          </span>
        )}
        {showMask && (
          <div
            style={{
              position: 'absolute',
              inset: 0,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: spacing.xs,
              background: token.colorBgMask,
              color: token.colorTextLightSolid,
              fontSize: 12,
            }}
          >
            {uploading ? <Spin size="small" /> : <UploadOutlined />}
            <span>{uploadTip}</span>
          </div>
        )}
      </div>
    </Upload>
  )
}

export function Settings() {
  const { t } = useTranslation()
  const isRoot = useAppStore((state) => state.userInfo?.isRoot === true)

  const [loading, setLoading] = useState(true)
  const [savingGraph, setSavingGraph] = useState(false)
  const [graphEnabled, setGraphEnabled] = useState(false)
  const [graphUri, setGraphUri] = useState('')
  const [graphUsername, setGraphUsername] = useState('')
  const [graphPassword, setGraphPassword] = useState('')
  const [graphDialect, setGraphDialect] = useState<'memgraph' | 'neo4j'>('memgraph')
  const [graphDirty, setGraphDirty] = useState(false)
  const [savingWiki, setSavingWiki] = useState(false)
  const [wikiMaxFileSize, setWikiMaxFileSize] = useState(0)
  const [wikiFileTypes, setWikiFileTypes] = useState<string[]>(WIKI_DEFAULT_FILE_TYPES)
  const [wikiDirty, setWikiDirty] = useState(false)
  const [savingSandbox, setSavingSandbox] = useState(false)
  const [statisticsGranularity, setStatisticsGranularity] = useState<'day' | 'hour'>('day')
  const [statisticsDirty, setStatisticsDirty] = useState(false)
  const [savingStatistics, setSavingStatistics] = useState(false)
  const [sandboxMaxTtl, setSandboxMaxTtl] = useState<number>(SandboxLimitDefaults.maxTtlSeconds)
  const [sandboxMaxCpu, setSandboxMaxCpu] = useState<string>(SandboxLimitDefaults.maxCpu)
  const [sandboxMaxMemory, setSandboxMaxMemory] = useState<string>(SandboxLimitDefaults.maxMemory)
  const [sandboxNetworkMode, setSandboxNetworkMode] = useState<string>(SandboxLimitDefaults.networkMode)
  const [sandboxIntranetCidrs, setSandboxIntranetCidrs] = useState<string>(SandboxLimitDefaults.intranetCidrs)
  const [sandboxDeniedCommands, setSandboxDeniedCommands] = useState<string>(SandboxLimitDefaults.deniedCommands)
  const [sandboxDirty, setSandboxDirty] = useState(false)
  const [logoPath, setLogoPath] = useState('')
  const [logoUploading, setLogoUploading] = useState(false)
  const [resettingLogo, setResettingLogo] = useState(false)
  const [loginBgPath, setLoginBgPath] = useState('')
  const [loginBgUploading, setLoginBgUploading] = useState(false)
  const [resettingLoginBg, setResettingLoginBg] = useState(false)
  const [siteName, setSiteName] = useState('')
  const [siteNameDirty, setSiteNameDirty] = useState(false)
  const [savingSiteName, setSavingSiteName] = useState(false)
  const [savingEmail, setSavingEmail] = useState(false)
  const [emailEnabled, setEmailEnabled] = useState(false)
  const [emailChannelId, setEmailChannelId] = useState('')
  const [registerCodeEnabled, setRegisterCodeEnabled] = useState(false)
  const [registerCodeTemplateId, setRegisterCodeTemplateId] = useState('')
  const [resetCodeTemplateId, setResetCodeTemplateId] = useState('')
  const [loginCodeTemplateId, setLoginCodeTemplateId] = useState('')
  const [emailDirty, setEmailDirty] = useState(false)
  const [smtpChannels, setSmtpChannels] = useState<{ id: string, name: string, enabled: boolean }[]>([])
  const [emailTemplates, setEmailTemplates] = useState<{ id: string, name: string, isBuiltIn: boolean }[]>([])
  const [savingTwofactor, setSavingTwofactor] = useState(false)
  const [twofaForced, setTwofaForced] = useState(false)
  const [loginCodeMode, setLoginCodeMode] = useState<LoginEmailCodeMode>('off')
  const [captchaMode, setCaptchaMode] = useState<LoginCaptchaMode>('off')
  const [emailCodeFailThreshold, setEmailCodeFailThreshold] = useState(3)
  const [twofaDirty, setTwofaDirty] = useState(false)
  const [savingLoginSafety, setSavingLoginSafety] = useState(false)
  const [lsEnabled, setLsEnabled] = useState(true)
  const [lsNotifyChannelIds, setLsNotifyChannelIds] = useState<string[]>([])
  const [lsNotifyEmails, setLsNotifyEmails] = useState<string[]>([])
  const [lsChannelOptions, setLsChannelOptions] = useState<{ value: string, label: string }[]>([])
  const [lsIpFailThreshold, setLsIpFailThreshold] = useState(20)
  const [lsIpFailWindow, setLsIpFailWindow] = useState(10)
  const [lsIpBanMinutes, setLsIpBanMinutes] = useState(30)
  const [lsDistinctUsers, setLsDistinctUsers] = useState(5)
  const [lsNewIpDays, setLsNewIpDays] = useState(90)
  const [lsTravelWindow, setLsTravelWindow] = useState(120)
  const [loginSafetyDirty, setLoginSafetyDirty] = useState(false)
  const [savingMemory, setSavingMemory] = useState(false)
  const [clearingMemory, setClearingMemory] = useState(false)
  const [memoryEnabled, setMemoryEnabled] = useState(false)
  const [memoryGlobalEnabled, setMemoryGlobalEnabled] = useState(false)
  const [memoryModelId, setMemoryModelId] = useState('')
  const [memoryDimensions, setMemoryDimensions] = useState<number>(MEMORY_DEFAULT_DIMENSIONS)
  const [memoryModels, setMemoryModels] = useState<GlobalMemoryModelOption[]>([])
  const [memoryDirty, setMemoryDirty] = useState(false)
  const [savedMemoryModelId, setSavedMemoryModelId] = useState('')
  const [savedMemoryDimensions, setSavedMemoryDimensions] = useState<number>(MEMORY_DEFAULT_DIMENSIONS)
  const [memoryMigration, setMemoryMigration] = useState<MemoryMigrationStatus | null>(null)
  const [startingMigration, setStartingMigration] = useState(false)

  // 已有记忆即锁定模型/维度（totalMemoryCount 来自迁移状态接口），更换须走迁移重建
  const memoryLocked = (memoryMigration?.totalMemoryCount ?? 0) > 0
  const migrationTask = memoryMigration?.activeMigration ?? null
  // 进行中 = Wait/Processing；终态（成功/失败）保留展示但不轮询
  const memoryMigrating = migrationTask?.state === 1 || migrationTask?.state === 2
  const memoryModelChanged = memoryModelId !== savedMemoryModelId || memoryDimensions !== savedMemoryDimensions
  const migrationPercent =
    migrationTask?.state === 4
      ? 100
      : migrationTask && (migrationTask.totalCount ?? 0) > 0
        ? Math.min(100, Math.round(((migrationTask.migratedCount ?? 0) / (migrationTask.totalCount ?? 1)) * 100))
        : 0

  useEffect(() => {
    void load()
    // 通知渠道下拉：高风险事件推送目标取启用中的全部渠道；邮箱验证绑定下拉取全部 SMTP 渠道（含停用，标记展示）
    getNotificationChannels()
      .then((channels) => {
        setLsChannelOptions(
          channels
            .filter(c => c.enabled !== false)
            .map(c => ({ value: String(c.id ?? ''), label: c.name ?? '' }))
            .filter(o => o.value.length > 0),
        )
        setSmtpChannels(
          channels
            .filter(c => c.type === 'smtp')
            .map(c => ({ id: String(c.id ?? ''), name: c.name ?? '', enabled: c.enabled !== false }))
            .filter(c => c.id.length > 0),
        )
      })
      .catch(() => {
        // 渠道列表加载失败不阻塞设置页，下拉为空即可
        setLsChannelOptions([])
        setSmtpChannels([])
      })
    // 验证码通知模板下拉：绑定用，含内置模板（注册/登录/找回密码验证码）
    getNotificationTemplates()
      .then((templates) => {
        setEmailTemplates(
          templates
            .map(t => ({ id: String(t.id ?? ''), name: t.name ?? '', isBuiltIn: t.isBuiltIn === true }))
            .filter(t => t.id.length > 0),
        )
      })
      .catch(() => {
        // 模板列表加载失败不阻塞设置页，下拉为空即可
        setEmailTemplates([])
      })
  }, [])

  async function load() {
    setLoading(true)
    try {
      const res = await getSettings()
      const items: SettingItem[] = res?.items ?? []
      const valueOf = (key: string) => items.find((s) => s.key === key)?.value ?? ''
      setGraphEnabled(valueOf(SettingKeys.kgEnabled) === 'true')
      setGraphUri(valueOf(SettingKeys.kgUri))
      setGraphUsername(valueOf(SettingKeys.kgUsername))
      setGraphPassword(valueOf(SettingKeys.kgPassword))
      setGraphDialect(valueOf(SettingKeys.kgDialect) === 'neo4j' ? 'neo4j' : 'memgraph')
      setGraphDirty(false)
      const parsedMaxFileSize = Number.parseInt(valueOf(SettingKeys.wikiMaxFileSize), 10)
      setWikiMaxFileSize(Number.isFinite(parsedMaxFileSize) && parsedMaxFileSize >= 0 ? parsedMaxFileSize : 50)
      setWikiFileTypes(parseWikiFileTypes(valueOf(SettingKeys.wikiAllowedFileTypes)))
      setWikiDirty(false)
      const parsedSandboxTtl = Number.parseInt(valueOf(SettingKeys.sandboxMaxTtl), 10)
      setSandboxMaxTtl(
        Number.isFinite(parsedSandboxTtl) && parsedSandboxTtl >= SANDBOX_TTL_LIMITS.min
          ? parsedSandboxTtl
          : SandboxLimitDefaults.maxTtlSeconds,
      )
      const sandboxCpu = valueOf(SettingKeys.sandboxMaxCpu).trim()
      setSandboxMaxCpu(sandboxCpu || SandboxLimitDefaults.maxCpu)
      const sandboxMemory = valueOf(SettingKeys.sandboxMaxMemory).trim()
      setSandboxMaxMemory(sandboxMemory || SandboxLimitDefaults.maxMemory)
      const parsedNetworkMode = valueOf(SettingKeys.sandboxNetworkMode).trim().toLowerCase()
      setSandboxNetworkMode(
        (SANDBOX_NETWORK_MODE_OPTIONS as readonly string[]).includes(parsedNetworkMode)
          ? parsedNetworkMode
          : SandboxLimitDefaults.networkMode,
      )
      const intranetCidrs = valueOf(SettingKeys.sandboxIntranetCidrs).trim()
      setSandboxIntranetCidrs(intranetCidrs || SandboxLimitDefaults.intranetCidrs)
      // 禁用命令清单：显式空串 = 平台级关闭（保持空串回显），行缺失时接口回退内置默认清单
      setSandboxDeniedCommands(valueOf(SettingKeys.sandboxDeniedCommands))
      setSandboxDirty(false)
      setStatisticsGranularity(valueOf(SettingKeys.statisticsGranularity).trim() === 'hour' ? 'hour' : 'day')
      setStatisticsDirty(false)
      setLogoPath(valueOf(SettingKeys.systemLogo).trim())
      setLoginBgPath(valueOf(SettingKeys.loginBackground).trim())
      setSiteName(valueOf(SettingKeys.systemName).trim())
      setSiteNameDirty(false)
      setEmailEnabled(valueOf(SettingKeys.smtpEnabled) === 'true')
      setEmailChannelId(valueOf(SettingKeys.smtpChannelId).trim())
      setRegisterCodeEnabled(valueOf(SettingKeys.registerEmailCodeEnabled) === 'true')
      setRegisterCodeTemplateId(valueOf(SettingKeys.smtpRegisterCodeTemplateId).trim())
      setResetCodeTemplateId(valueOf(SettingKeys.smtpResetCodeTemplateId).trim())
      setLoginCodeTemplateId(valueOf(SettingKeys.smtpLoginCodeTemplateId).trim())
      setEmailDirty(false)
      setTwofaForced(valueOf(SettingKeys.loginForced2fa) === 'true')
      const parsedLoginCodeMode = valueOf(SettingKeys.loginEmailCodeMode).trim().toLowerCase()
      setLoginCodeMode(
        (LOGIN_EMAIL_CODE_MODE_OPTIONS as readonly string[]).includes(parsedLoginCodeMode)
          ? (parsedLoginCodeMode as LoginEmailCodeMode)
          : 'off',
      )
      const parsedCaptchaMode = valueOf(SettingKeys.loginCaptchaMode).trim().toLowerCase()
      setCaptchaMode(
        (LOGIN_CAPTCHA_MODE_OPTIONS as readonly string[]).includes(parsedCaptchaMode)
          ? (parsedCaptchaMode as LoginCaptchaMode)
          : 'off',
      )
      const parsedThreshold = Number.parseInt(valueOf(SettingKeys.loginEmailCodeFailThreshold), 10)
      setEmailCodeFailThreshold(Number.isFinite(parsedThreshold) && parsedThreshold >= 1 && parsedThreshold <= 10 ? parsedThreshold : 3)
      setTwofaDirty(false)
      setLsEnabled(valueOf(SettingKeys.lsEnabled) !== 'false')
      setLsNotifyChannelIds(
        valueOf(SettingKeys.lsNotifyChannelIds)
          .trim()
          .split(',')
          .map(id => id.trim())
          .filter(id => /^\d+$/.test(id)),
      )
      setLsNotifyEmails(
        valueOf(SettingKeys.lsNotifyEmails)
          .trim()
          .split(',')
          .map(email => email.trim())
          .filter(email => email.length > 0),
      )
      const lsInt = (key: string, fallback: number, min: number, max: number) => {
        const parsed = Number.parseInt(valueOf(key), 10)
        return Number.isFinite(parsed) && parsed >= min && parsed <= max ? parsed : fallback
      }
      setLsIpFailThreshold(lsInt(SettingKeys.lsIpFailThreshold, 20, 1, 10000))
      setLsIpFailWindow(lsInt(SettingKeys.lsIpFailWindowMinutes, 10, 1, 525600))
      setLsIpBanMinutes(lsInt(SettingKeys.lsIpBanMinutes, 30, 1, 525600))
      setLsDistinctUsers(lsInt(SettingKeys.lsDistinctUserThreshold, 5, 1, 10000))
      setLsNewIpDays(lsInt(SettingKeys.lsNewIpDays, 90, 0, 3650))
      setLsTravelWindow(lsInt(SettingKeys.lsTravelWindowMinutes, 120, 1, 525600))
      setLoginSafetyDirty(false)
      setMemoryEnabled(valueOf(SettingKeys.memoryEnabled) === 'true')
      setMemoryGlobalEnabled(valueOf(SettingKeys.memoryGlobalEnabled) === 'true')
      setMemoryModelId(valueOf(SettingKeys.memoryEmbeddingModel).trim())
      const parsedMemoryDimensions = Number.parseInt(valueOf(SettingKeys.memoryEmbeddingDimensions), 10)
      const effectiveMemoryDimensions =
        Number.isFinite(parsedMemoryDimensions) && parsedMemoryDimensions >= 1 && parsedMemoryDimensions <= 2000
          ? parsedMemoryDimensions
          : MEMORY_DEFAULT_DIMENSIONS
      setMemoryDimensions(effectiveMemoryDimensions)
      setSavedMemoryModelId(valueOf(SettingKeys.memoryEmbeddingModel).trim())
      setSavedMemoryDimensions(effectiveMemoryDimensions)
      setMemoryDirty(false)
      void loadMemoryMigrationStatus()
      try {
        setMemoryModels(await getGlobalMemoryModelOptions())
      } catch {
        // 模型选项加载失败不影响其余设置展示
      }
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }

  async function handleSaveGraph() {
    setSavingGraph(true)
    try {
      await saveSetting(SettingKeys.kgEnabled, graphEnabled ? 'true' : 'false')
      if (graphEnabled) {
        await saveSetting(SettingKeys.kgUri, graphUri)
        await saveSetting(SettingKeys.kgUsername, graphUsername)
        await saveSetting(SettingKeys.kgPassword, graphPassword)
        await saveSetting(SettingKeys.kgDialect, graphDialect)
      }
      feedback.success(t('settings.saveSuccess'))
      setGraphDirty(false)
    } catch {
      // 保存失败时重新加载，恢复为数据库中的真实值（错误已由全局请求中间件统一提示）
      void load()
    } finally {
      setSavingGraph(false)
    }
  }

  async function handleSaveWiki() {
    // 扩展名归一失败（为空/非法/重复）时前端拦截，不发起任何保存请求
    const normalizedFileTypes = normalizeWikiFileTypes(wikiFileTypes)
    if (!normalizedFileTypes) {
      feedback.error(t('settings.wikiUpload.invalidFileTypes'))
      return
    }
    setSavingWiki(true)
    try {
      await saveSetting(SettingKeys.wikiMaxFileSize, String(wikiMaxFileSize ?? 0))
      await saveSetting(SettingKeys.wikiAllowedFileTypes, normalizedFileTypes.join(','))
      feedback.success(t('settings.saveSuccess'))
      setWikiDirty(false)
    } catch {
      // 保存失败时重新加载，恢复为数据库中的真实值（错误已由全局请求中间件统一提示）
      void load()
    } finally {
      setSavingWiki(false)
    }
  }

  async function handleSaveSandbox() {
    if (
      !Number.isInteger(sandboxMaxTtl) ||
      sandboxMaxTtl < SANDBOX_TTL_LIMITS.min ||
      sandboxMaxTtl > SANDBOX_TTL_LIMITS.max
    ) {
      feedback.error(t('settings.sandbox.invalidTtl'))
      return
    }
    if (parseCpuMillicores(sandboxMaxCpu) == null || parseMemoryBytes(sandboxMaxMemory) == null) {
      feedback.error(t('settings.sandbox.invalidFormat'))
      return
    }
    if (!isValidNetworkMode(sandboxNetworkMode)) {
      feedback.error(t('settings.sandbox.invalidNetworkMode'))
      return
    }
    if (!isValidIntranetCidrs(sandboxIntranetCidrs)) {
      feedback.error(t('settings.sandbox.invalidIntranetCidrs'))
      return
    }
    if (!isValidCommandPatternList(sandboxDeniedCommands)) {
      feedback.error(t('settings.sandbox.invalidDeniedCommands'))
      return
    }
    setSavingSandbox(true)
    try {
      await saveSetting(SettingKeys.sandboxMaxTtl, String(sandboxMaxTtl))
      await saveSetting(SettingKeys.sandboxMaxCpu, sandboxMaxCpu.trim())
      await saveSetting(SettingKeys.sandboxMaxMemory, sandboxMaxMemory.trim())
      await saveSetting(SettingKeys.sandboxNetworkMode, sandboxNetworkMode.trim().toLowerCase())
      await saveSetting(SettingKeys.sandboxIntranetCidrs, sandboxIntranetCidrs.trim())
      // 空清单保存空串 = 平台级禁用清单关闭（后端行存在空串时不再回退内置默认）
      await saveSetting(
        SettingKeys.sandboxDeniedCommands,
        sandboxDeniedCommands.split('\n').map((x) => x.trim()).filter(Boolean).join('\n'),
      )
      feedback.success(t('settings.saveSuccess'))
      setSandboxDirty(false)
    } catch {
      // 保存失败时重新加载，恢复为数据库中的真实值（错误已由全局请求中间件统一提示）
      void load()
    } finally {
      setSavingSandbox(false)
    }
  }

  async function handleSaveLoginSafety() {
    setSavingLoginSafety(true)
    try {
      await saveSetting(SettingKeys.lsEnabled, lsEnabled ? 'true' : 'false')
      await saveSetting(SettingKeys.lsNotifyChannelIds, lsNotifyChannelIds.join(','))
      await saveSetting(SettingKeys.lsNotifyEmails, lsNotifyEmails.join(','))
      await saveSetting(SettingKeys.lsIpFailThreshold, String(lsIpFailThreshold))
      await saveSetting(SettingKeys.lsIpFailWindowMinutes, String(lsIpFailWindow))
      await saveSetting(SettingKeys.lsIpBanMinutes, String(lsIpBanMinutes))
      await saveSetting(SettingKeys.lsDistinctUserThreshold, String(lsDistinctUsers))
      await saveSetting(SettingKeys.lsNewIpDays, String(lsNewIpDays))
      await saveSetting(SettingKeys.lsTravelWindowMinutes, String(lsTravelWindow))
      feedback.success(t('settings.saveSuccess'))
      setLoginSafetyDirty(false)
    } catch {
      // 保存失败时重新加载，恢复为数据库中的真实值（错误已由全局请求中间件统一提示）
      void load()
    } finally {
      setSavingLoginSafety(false)
    }
  }

  async function handleSaveStatistics() {
    setSavingStatistics(true)
    try {
      await saveSetting(SettingKeys.statisticsGranularity, statisticsGranularity)
      feedback.success(t('settings.saveSuccess'))
      setStatisticsDirty(false)
    } catch {
      // 保存失败时重新加载，恢复为数据库中的真实值（错误已由全局请求中间件统一提示）
      void load()
    } finally {
      setSavingStatistics(false)
    }
  }

  async function handleSaveSiteName() {
    setSavingSiteName(true)
    try {
      // 后端以空值回退默认名称，提交前统一去首尾空白
      await saveSetting(SettingKeys.systemName, siteName.trim())
      // 刷新全局 serverInfo，侧边栏标题与浏览器标签页立即生效
      await refreshServerInfo()
      feedback.success(t('settings.saveSuccess'))
      setSiteNameDirty(false)
    } catch {
      void load()
    } finally {
      setSavingSiteName(false)
    }
  }

  async function handleSaveEmail() {
    setSavingEmail(true)
    try {
      // 先写开关再写绑定项；SMTP 连接配置与验证码邮件模板统一在通知中心维护，此处仅绑定 id
      await saveSetting(SettingKeys.smtpEnabled, emailEnabled ? 'true' : 'false')
      await saveSetting(SettingKeys.smtpChannelId, emailChannelId.trim())
      await saveSetting(SettingKeys.registerEmailCodeEnabled, registerCodeEnabled ? 'true' : 'false')
      await saveSetting(SettingKeys.smtpRegisterCodeTemplateId, registerCodeTemplateId.trim())
      await saveSetting(SettingKeys.smtpResetCodeTemplateId, resetCodeTemplateId.trim())
      await saveSetting(SettingKeys.smtpLoginCodeTemplateId, loginCodeTemplateId.trim())
      feedback.success(t('settings.saveSuccess'))
      setEmailDirty(false)
    } catch {
      // 保存失败时重新加载，恢复为数据库中的真实值（错误已由全局请求中间件统一提示）
      void load()
    } finally {
      setSavingEmail(false)
    }
  }

  /** 渲染验证码通知模板绑定下拉（选项来自通知中心模板列表，内置模板带标识；清空即回退内置模板） */
  function renderCodeTemplateField(prefix: string, value: string, onChange: (v: string) => void) {
    return (
      <Field label={t(`${prefix}.name`)}>
        <Select
          value={value || undefined}
          placeholder={t(`${prefix}.placeholder`)}
          aria-label={t(`${prefix}.name`)}
          showSearch
          optionFilterProp="label"
          allowClear
          options={emailTemplates.map(tpl => ({
            value: tpl.id,
            label: tpl.isBuiltIn ? `${tpl.name}（${t('settings.email.template.builtinTag')}）` : tpl.name,
          }))}
          onChange={(v) => {
            onChange(typeof v === 'string' ? v : '')
            setEmailDirty(true)
          }}
          notFoundContent={t('settings.email.template.empty')}
          style={{ maxWidth: 480 }}
        />
        <Text type="secondary">{t(`${prefix}.desc`)}</Text>
      </Field>
    )
  }

  async function handleSaveTwofactor() {
    setSavingTwofactor(true)
    try {
      await saveSetting(SettingKeys.loginForced2fa, twofaForced ? 'true' : 'false')
      await saveSetting(SettingKeys.loginEmailCodeMode, loginCodeMode)
      await saveSetting(SettingKeys.loginCaptchaMode, captchaMode)
      await saveSetting(SettingKeys.loginEmailCodeFailThreshold, String(emailCodeFailThreshold))
      feedback.success(t('settings.saveSuccess'))
      setTwofaDirty(false)
      // 强制 2FA 影响登录后行为，登录页/网关侧按实时设置读取，无需刷新缓存
    } catch {
      // 保存失败时重新加载，恢复为数据库中的真实值（错误已由全局请求中间件统一提示）
      void load()
    } finally {
      setSavingTwofactor(false)
    }
  }

  async function loadMemoryMigrationStatus() {
    try {
      setMemoryMigration(await getMemoryModelMigrationStatus())
    } catch {
      // 状态加载失败不影响其余设置展示（按未锁定处理）
      setMemoryMigration(null)
    }
  }

  // 迁移进行中每 3s 轮询一次进度，终态提示；迁移只改模型/维度两项设置，
  // 成功后直接以状态接口回传的当前口径刷新本地值（与 settings 读取同源），无需整页重载
  useEffect(() => {
    if (!memoryMigrating) {
      return
    }
    const timer = window.setInterval(async () => {
      try {
        const status = await getMemoryModelMigrationStatus()
        setMemoryMigration(status)
        const state = status.activeMigration?.state
        if (state === 4 || state === 5) {
          if (state === 4) {
            const modelId = status.currentModelId ?? ''
            const dims =
              typeof status.currentDimensions === 'number' && status.currentDimensions >= 1 && status.currentDimensions <= 2000
                ? status.currentDimensions
                : MEMORY_DEFAULT_DIMENSIONS
            setMemoryModelId(modelId)
            setMemoryDimensions(dims)
            setSavedMemoryModelId(modelId)
            setSavedMemoryDimensions(dims)
            feedback.success(t('settings.memory.migrate.success'))
          } else {
            feedback.error(t('settings.memory.migrate.failed'))
          }
        }
      } catch {
        // 轮询失败忽略，下一轮重试
      }
    }, 3000)
    return () => window.clearInterval(timer)
  }, [memoryMigrating, t])

  async function handleStartMigration() {
    if (!memoryModelId) {
      feedback.error(t('settings.memory.modelRequired'))
      return
    }
    if (!Number.isInteger(memoryDimensions) || memoryDimensions < 1 || memoryDimensions > 2000) {
      feedback.error(t('settings.memory.invalidDimensions'))
      return
    }
    setStartingMigration(true)
    try {
      const taskId = await startMemoryModelMigration(memoryModelId, memoryDimensions)
      if (!taskId) {
        feedback.error(t('settings.memory.migrate.failed'))
        return
      }
      feedback.success(t('settings.memory.migrate.started'))
      // 立即拉取一次状态进入轮询
      await loadMemoryMigrationStatus()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setStartingMigration(false)
    }
  }

  async function handleSaveMemory() {
    if (memoryEnabled && !memoryModelId) {
      feedback.error(t('settings.memory.modelRequired'))
      return
    }
    if (!Number.isInteger(memoryDimensions) || memoryDimensions < 1 || memoryDimensions > 2000) {
      feedback.error(t('settings.memory.invalidDimensions'))
      return
    }
    setSavingMemory(true)
    try {
      // 跨应用记忆随总开关级联：总开关关闭时强制关闭（先写总开关再写全局，开启校验要求总开关已开启）
      const effectiveGlobalEnabled = memoryEnabled && memoryGlobalEnabled
      if (memoryLocked) {
        // 已有记忆时模型/维度被锁定（更换须走迁移重建），保存仅提交开关
        await saveSetting(SettingKeys.memoryEnabled, memoryEnabled ? 'true' : 'false')
        await saveSetting(SettingKeys.memoryGlobalEnabled, effectiveGlobalEnabled ? 'true' : 'false')
      } else {
        // 先写模型与维度，再写开关：开启校验要求模型已保存
        await saveSetting(SettingKeys.memoryEmbeddingModel, memoryModelId)
        await saveSetting(SettingKeys.memoryEmbeddingDimensions, String(memoryDimensions))
        await saveSetting(SettingKeys.memoryEnabled, memoryEnabled ? 'true' : 'false')
        await saveSetting(SettingKeys.memoryGlobalEnabled, effectiveGlobalEnabled ? 'true' : 'false')
      }
      feedback.success(t('settings.saveSuccess'))
      setMemoryDirty(false)
      void loadMemoryMigrationStatus()
    } catch {
      // 保存失败时重新加载，恢复为数据库中的真实值（错误已由全局请求中间件统一提示）
      void load()
    } finally {
      setSavingMemory(false)
    }
  }

  async function handleClearAllMemory() {
    setClearingMemory(true)
    try {
      await clearAllMemory()
      feedback.success(t('settings.memory.clearSuccess'))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setClearingMemory(false)
    }
  }

  async function handleLogoUpload(file: File) {
    if (!file.type.startsWith('image/')) {
      feedback.error(t('settings.logo.typeError'))
      return
    }
    if (file.size > 5 * 1024 * 1024) {
      feedback.error(t('settings.logo.sizeError'))
      return
    }
    setLogoUploading(true)
    try {
      const objectKey = await updateSystemLogo(file)
      setLogoPath(objectKey)
      // 刷新全局 serverInfo，侧边栏/登录/注册页 Logo 立即生效
      await refreshServerInfo()
      feedback.success(t('settings.logo.uploadSuccess'))
    } catch {
      void load()
    } finally {
      setLogoUploading(false)
    }
  }

  async function handleLogoReset() {
    setResettingLogo(true)
    try {
      await resetSystemLogo()
      setLogoPath('')
      await refreshServerInfo()
      feedback.success(t('settings.logo.resetSuccess'))
    } catch {
      void load()
    } finally {
      setResettingLogo(false)
    }
  }

  async function handleLoginBgUpload(file: File) {
    if (!file.type.startsWith('image/')) {
      feedback.error(t('settings.loginBg.typeError'))
      return
    }
    if (file.size > 5 * 1024 * 1024) {
      feedback.error(t('settings.loginBg.sizeError'))
      return
    }
    setLoginBgUploading(true)
    try {
      const objectKey = await updateLoginBackground(file)
      setLoginBgPath(objectKey)
      // 刷新全局 serverInfo，登录页背景立即生效
      await refreshServerInfo()
      feedback.success(t('settings.loginBg.uploadSuccess'))
    } catch {
      void load()
    } finally {
      setLoginBgUploading(false)
    }
  }

  async function handleLoginBgReset() {
    setResettingLoginBg(true)
    try {
      await resetLoginBackground()
      setLoginBgPath('')
      await refreshServerInfo()
      feedback.success(t('settings.loginBg.resetSuccess'))
    } catch {
      void load()
    } finally {
      setResettingLoginBg(false)
    }
  }

  if (!isRoot) {
    return <Navigate to="/apps" replace />
  }

  return (
    <Page>
      <div style={{ display: 'flex', flexDirection: 'column', gap: spacing.md }}>
        <CollapsibleCard title={t('settings.site.title')} defaultExpanded>
          <div style={{ display: 'flex', flexDirection: 'column', gap: spacing.lg }}>
            <Field label={t('settings.siteName.name')}>
              <Input
                value={siteName}
                maxLength={50}
                showCount
                placeholder={t('settings.siteName.placeholder')}
                aria-label={t('settings.siteName.name')}
                onChange={(e) => {
                  setSiteName(e.target.value)
                  setSiteNameDirty(true)
                }}
                style={{ maxWidth: 360 }}
              />
              <Text type="secondary">{t('settings.siteName.desc')}</Text>
            </Field>

            <div style={{ display: 'flex', alignItems: 'center', gap: spacing.md }}>
              <AvatarUpload
                src={resolveStorageUrl(logoPath) || DEFAULT_LOGO_SRC}
                shape="square"
                size={96}
                uploading={logoUploading}
                onSelect={handleLogoUpload}
              />
              <div style={{ flex: 1, minWidth: 0 }}>
                <Text strong>{t('settings.logo.name')}</Text>
                <br />
                <Text type="secondary">{t('settings.logo.desc')}</Text>
              </div>
              {logoPath && (
                <Popconfirm
                  title={t('settings.logo.resetConfirm')}
                  onConfirm={handleLogoReset}
                  disabled={resettingLogo}
                >
                  <Button danger loading={resettingLogo}>
                    {t('settings.logo.reset')}
                  </Button>
                </Popconfirm>
              )}
            </div>

            <div style={{ display: 'flex', alignItems: 'center', gap: spacing.md }}>
              <BackgroundUploadBox
                src={resolveStorageUrl(loginBgPath)}
                uploading={loginBgUploading}
                placeholder={t('settings.loginBg.placeholder')}
                uploadTip={t('settings.loginBg.upload')}
                onSelect={handleLoginBgUpload}
              />
              <div style={{ flex: 1, minWidth: 0 }}>
                <Text strong>{t('settings.loginBg.name')}</Text>
                <br />
                <Text type="secondary">{t('settings.loginBg.desc')}</Text>
              </div>
              {loginBgPath && (
                <Popconfirm
                  title={t('settings.loginBg.resetConfirm')}
                  onConfirm={handleLoginBgReset}
                  disabled={resettingLoginBg}
                >
                  <Button danger loading={resettingLoginBg}>
                    {t('settings.loginBg.reset')}
                  </Button>
                </Popconfirm>
              )}
            </div>
          </div>

          <div style={{ marginTop: spacing.lg, textAlign: 'left' }}>
            <Button
              type="primary"
              loading={savingSiteName}
              disabled={!siteNameDirty || loading}
              onClick={handleSaveSiteName}
            >
              {t('settings.save')}
            </Button>
          </div>
        </CollapsibleCard>

        <CollapsibleCard title={t('settings.wikiUpload.title')}>
          <Field label={t('settings.wikiUpload.maxFileSize.name')}>
            <InputNumber
              value={wikiMaxFileSize}
              min={0}
              max={1024}
              precision={0}
              addonAfter="MB"
              aria-label={t('settings.wikiUpload.maxFileSize.name')}
              onChange={(value) => {
                setWikiMaxFileSize(typeof value === 'number' ? value : 0)
                setWikiDirty(true)
              }}
              style={{ width: 200 }}
            />
            <Text type="secondary">{t('settings.wikiUpload.maxFileSize.desc')}</Text>
          </Field>
          <Field label={t('settings.wikiUpload.fileTypes.name')}>
            <Select
              mode="tags"
              open={false}
              tokenSeparators={[',']}
              value={wikiFileTypes}
              aria-label={t('settings.wikiUpload.fileTypes.name')}
              placeholder={t('settings.wikiUpload.fileTypes.placeholder')}
              onChange={(value) => {
                setWikiFileTypes(value)
                setWikiDirty(true)
              }}
              style={{ maxWidth: 560 }}
            />
            <Text type="secondary">{t('settings.wikiUpload.fileTypes.desc')}</Text>
          </Field>

          <div style={{ marginTop: spacing.lg, textAlign: 'left' }}>
            <Button
              type="primary"
              loading={savingWiki}
              disabled={!wikiDirty || loading}
              onClick={handleSaveWiki}
            >
              {t('settings.save')}
            </Button>
          </div>
        </CollapsibleCard>

        <CollapsibleCard title={t('settings.knowledgeGraph.title')} defaultExpanded>
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              gap: spacing.md,
            }}
          >
            <div style={{ minWidth: 0 }}>
              <Text strong>{t('settings.knowledgeGraph.enable.name')}</Text>
              <br />
              <Text type="secondary">{t('settings.knowledgeGraph.enable.desc')}</Text>
            </div>
            <Switch
              checked={graphEnabled}
              loading={loading}
              onChange={(checked) => {
                setGraphEnabled(checked)
                setGraphDirty(true)
              }}
            />
          </div>

          {graphEnabled && (
            <div
              style={{
                display: 'flex',
                flexDirection: 'column',
                gap: spacing.md,
                marginTop: spacing.lg,
              }}
            >
              <Field label={t('settings.knowledgeGraph.dialect.name')}>
                <Select
                  value={graphDialect}
                  aria-label={t('settings.knowledgeGraph.dialect.name')}
                  onChange={(value) => {
                    setGraphDialect(value)
                    setGraphDirty(true)
                  }}
                  options={[
                    { value: 'memgraph', label: t('settings.knowledgeGraph.dialectMemgraph') },
                    { value: 'neo4j', label: t('settings.knowledgeGraph.dialectNeo4j') },
                  ]}
                  style={{ width: 200 }}
                />
                <Text type="secondary">{t('settings.knowledgeGraph.dialect.desc')}</Text>
              </Field>
              <Field label={t('settings.knowledgeGraph.uri.name')}>
                <Input
                  value={graphUri}
                  placeholder={t('settings.knowledgeGraph.uri.placeholder')}
                  aria-label={t('settings.knowledgeGraph.uri.name')}
                  onChange={(e) => {
                    setGraphUri(e.target.value)
                    setGraphDirty(true)
                  }}
                />
              </Field>
              <div style={{ display: 'flex', gap: spacing.md }}>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <Field label={t('settings.knowledgeGraph.username.name')}>
                    <Input
                      value={graphUsername}
                      placeholder={t('settings.knowledgeGraph.username.placeholder')}
                      aria-label={t('settings.knowledgeGraph.username.name')}
                      onChange={(e) => {
                        setGraphUsername(e.target.value)
                        setGraphDirty(true)
                      }}
                    />
                  </Field>
                </div>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <Field label={t('settings.knowledgeGraph.password.name')}>
                    <Input.Password
                      value={graphPassword}
                      placeholder={t('settings.knowledgeGraph.password.placeholder')}
                      aria-label={t('settings.knowledgeGraph.password.name')}
                      onChange={(e) => {
                        setGraphPassword(e.target.value)
                        setGraphDirty(true)
                      }}
                    />
                  </Field>
                </div>
              </div>
            </div>
          )}

          <div style={{ marginTop: spacing.lg, textAlign: 'left' }}>
            <Button
              type="primary"
              loading={savingGraph}
              disabled={!graphDirty || loading}
              onClick={handleSaveGraph}
            >
              {t('settings.save')}
            </Button>
          </div>
        </CollapsibleCard>

        <CollapsibleCard title={t('settings.email.title')}>
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              gap: spacing.md,
            }}
          >
            <div style={{ minWidth: 0 }}>
              <Text strong>{t('settings.email.enable.name')}</Text>
              <br />
              <Text type="secondary">{t('settings.email.enable.desc')}</Text>
            </div>
            <Switch
              checked={emailEnabled}
              loading={loading}
              onChange={(checked) => {
                setEmailEnabled(checked)
                setEmailDirty(true)
              }}
            />
          </div>

          {emailEnabled && (
            <div
              style={{
                display: 'flex',
                flexDirection: 'column',
                gap: spacing.md,
                marginTop: spacing.lg,
              }}
            >
              <Field label={t('settings.email.channel.name')}>
                <Select
                  value={emailChannelId || undefined}
                  placeholder={t('settings.email.channel.placeholder')}
                  aria-label={t('settings.email.channel.name')}
                  showSearch
                  optionFilterProp="label"
                  allowClear
                  options={smtpChannels.map(c => ({ value: c.id, label: c.enabled ? c.name : `${c.name}（${t('notification.status.disabled')}）` }))}
                  onChange={(value) => {
                    setEmailChannelId(typeof value === 'string' ? value : '')
                    setEmailDirty(true)
                  }}
                  notFoundContent={t('settings.email.channel.empty')}
                  style={{ maxWidth: 480 }}
                />
                <Text type="secondary">{t('settings.email.channel.desc')}</Text>
              </Field>
              <div
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  gap: spacing.md,
                  marginTop: spacing.sm,
                }}
              >
                <div style={{ minWidth: 0 }}>
                  <Text strong>{t('settings.email.registerCode.name')}</Text>
                  <br />
                  <Text type="secondary">{t('settings.email.registerCode.desc')}</Text>
                </div>
                <Switch
                  checked={registerCodeEnabled}
                  aria-label={t('settings.email.registerCode.name')}
                  onChange={(checked) => {
                    setRegisterCodeEnabled(checked)
                    setEmailDirty(true)
                  }}
                />
              </div>
              {renderCodeTemplateField('settings.email.registerTemplate', registerCodeTemplateId, setRegisterCodeTemplateId)}
              {renderCodeTemplateField('settings.email.resetTemplate', resetCodeTemplateId, setResetCodeTemplateId)}
              {renderCodeTemplateField('settings.email.loginTemplate', loginCodeTemplateId, setLoginCodeTemplateId)}
              <Text type="secondary" style={{ marginTop: `-${spacing.sm}` }}>{t('settings.email.templateTip')}</Text>
            </div>
          )}

          <div style={{ marginTop: spacing.lg, textAlign: 'left' }}>
            <Button
              type="primary"
              loading={savingEmail}
              disabled={!emailDirty || loading}
              onClick={handleSaveEmail}
            >
              {t('settings.save')}
            </Button>
          </div>
        </CollapsibleCard>

        <CollapsibleCard title={t('settings.sandbox.title')}>
          <Field label={t('settings.sandbox.maxTtl.name')}>
            <InputNumber
              value={sandboxMaxTtl}
              min={SANDBOX_TTL_LIMITS.min}
              max={SANDBOX_TTL_LIMITS.max}
              precision={0}
              addonAfter={t('appManage.sandboxSeconds')}
              aria-label={t('settings.sandbox.maxTtl.name')}
              onChange={(value) => {
                setSandboxMaxTtl(typeof value === 'number' ? value : SandboxLimitDefaults.maxTtlSeconds)
                setSandboxDirty(true)
              }}
              style={{ width: 220 }}
            />
            <Text type="secondary">{t('settings.sandbox.maxTtl.desc')}</Text>
          </Field>
          <div style={{ display: 'flex', gap: spacing.md }}>
            <div style={{ flex: 1, minWidth: 0 }}>
              <Field label={t('settings.sandbox.maxCpu.name')}>
                <Input
                  value={sandboxMaxCpu}
                  placeholder={SandboxLimitDefaults.maxCpu}
                  aria-label={t('settings.sandbox.maxCpu.name')}
                  onChange={(e) => {
                    setSandboxMaxCpu(e.target.value)
                    setSandboxDirty(true)
                  }}
                />
                <Text type="secondary">{t('settings.sandbox.maxCpu.desc')}</Text>
              </Field>
            </div>
            <div style={{ flex: 1, minWidth: 0 }}>
              <Field label={t('settings.sandbox.maxMemory.name')}>
                <Input
                  value={sandboxMaxMemory}
                  placeholder={SandboxLimitDefaults.maxMemory}
                  aria-label={t('settings.sandbox.maxMemory.name')}
                  onChange={(e) => {
                    setSandboxMaxMemory(e.target.value)
                    setSandboxDirty(true)
                  }}
                />
                <Text type="secondary">{t('settings.sandbox.maxMemory.desc')}</Text>
              </Field>
            </div>
          </div>
          <Field label={t('settings.sandbox.networkMode.name')}>
            <Select
              value={sandboxNetworkMode}
              aria-label={t('settings.sandbox.networkMode.name')}
              style={{ width: 320 }}
              onChange={(value) => {
                setSandboxNetworkMode(value)
                setSandboxDirty(true)
              }}
              options={[
                { value: 'app', label: t('settings.sandbox.networkMode.app') },
                { value: 'deny', label: t('settings.sandbox.networkMode.deny') },
                { value: 'intranet', label: t('settings.sandbox.networkMode.intranet') },
              ]}
            />
            <Text type="secondary">{t('settings.sandbox.networkMode.desc')}</Text>
          </Field>
          <Field label={t('settings.sandbox.intranetCidrs.name')}>
            <Input
              value={sandboxIntranetCidrs}
              placeholder={SandboxLimitDefaults.intranetCidrs}
              aria-label={t('settings.sandbox.intranetCidrs.name')}
              onChange={(e) => {
                setSandboxIntranetCidrs(e.target.value)
                setSandboxDirty(true)
              }}
            />
            <Text type="secondary">{t('settings.sandbox.intranetCidrs.desc')}</Text>
          </Field>
          <Field label={t('settings.sandbox.deniedCommands.name')}>
            <Input.TextArea
              value={sandboxDeniedCommands}
              placeholder={SandboxLimitDefaults.deniedCommands}
              aria-label={t('settings.sandbox.deniedCommands.name')}
              onChange={(e) => {
                setSandboxDeniedCommands(e.target.value)
                setSandboxDirty(true)
              }}
              autoSize={{ minRows: 4, maxRows: 10 }}
            />
            <Text type="secondary">{t('settings.sandbox.deniedCommands.desc')}</Text>
          </Field>

          <div style={{ marginTop: spacing.lg, textAlign: 'left' }}>
            <Button
              type="primary"
              loading={savingSandbox}
              disabled={!sandboxDirty || loading}
              onClick={handleSaveSandbox}
            >
              {t('settings.save')}
            </Button>
          </div>
        </CollapsibleCard>

        <CollapsibleCard title={t('settings.loginSafety.title')}>
          <div style={{ display: 'flex', flexDirection: 'column', gap: spacing.lg }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: spacing.md }}>
              <div>
                <Text strong style={{ display: 'block' }}>{t('settings.twofactor.forced.name')}</Text>
                <Text type="secondary">{t('settings.twofactor.forced.desc')}</Text>
              </div>
              <Switch
                aria-label={t('settings.twofactor.forced.name')}
                checked={twofaForced}
                disabled={loading}
                onChange={(checked) => {
                  setTwofaForced(checked)
                  setTwofaDirty(true)
                }}
              />
            </div>

            <Field label={t('settings.twofactor.loginCodeMode.name')}>
              <Radio.Group
                aria-label={t('settings.twofactor.loginCodeMode.name')}
                value={loginCodeMode}
                disabled={loading}
                onChange={(e) => {
                  setLoginCodeMode(e.target.value as LoginEmailCodeMode)
                  setTwofaDirty(true)
                }}
              >
                <Radio value="off">{t('settings.twofactor.loginCodeMode.off')}</Radio>
                <Radio value="always">{t('settings.twofactor.loginCodeMode.always')}</Radio>
                <Radio value="after_fail">{t('settings.twofactor.loginCodeMode.afterFail')}</Radio>
              </Radio.Group>
              <Text type="secondary" style={{ display: 'block', marginTop: 4 }}>
                {t('settings.twofactor.loginCodeMode.desc')}
              </Text>
            </Field>

            {loginCodeMode === 'after_fail' && (
              <Field label={t('settings.twofactor.emailCodeFailThreshold.name')}>
                <InputNumber
                  aria-label={t('settings.twofactor.emailCodeFailThreshold.name')}
                  min={1}
                  max={10}
                  precision={0}
                  value={emailCodeFailThreshold}
                  disabled={loading}
                  style={{ width: 160 }}
                  onChange={(v) => {
                    if (typeof v === 'number') {
                      setEmailCodeFailThreshold(v)
                      setTwofaDirty(true)
                    }
                  }}
                />
                <Text type="secondary" style={{ display: 'block', marginTop: 4 }}>
                  {t('settings.twofactor.emailCodeFailThreshold.desc')}
                </Text>
              </Field>
            )}

            <Field label={t('settings.twofactor.captchaMode.name')}>
              <Radio.Group
                aria-label={t('settings.twofactor.captchaMode.name')}
                value={captchaMode}
                disabled={loading}
                onChange={(e) => {
                  setCaptchaMode(e.target.value as LoginCaptchaMode)
                  setTwofaDirty(true)
                }}
              >
                <Radio value="off">{t('settings.twofactor.captchaMode.off')}</Radio>
                <Radio value="always">{t('settings.twofactor.captchaMode.always')}</Radio>
                <Radio value="after_fail">{t('settings.twofactor.captchaMode.afterFail')}</Radio>
              </Radio.Group>
              <Text type="secondary" style={{ display: 'block', marginTop: 4 }}>
                {t('settings.twofactor.captchaMode.desc')}
              </Text>
            </Field>

            <div style={{ textAlign: 'left' }}>
              <Button type="primary" loading={savingTwofactor} disabled={!twofaDirty || loading} onClick={handleSaveTwofactor}>
                {t('settings.save')}
              </Button>
            </div>
          </div>

          <Divider style={{ margin: `${spacing.lg}px 0` }} />

          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              gap: spacing.md,
            }}
          >
            <div style={{ minWidth: 0 }}>
              <Text strong>{t('settings.loginSafety.enable.name')}</Text>
              <br />
              <Text type="secondary">{t('settings.loginSafety.enable.desc')}</Text>
            </div>
            <Switch
              checked={lsEnabled}
              onChange={(checked) => {
                setLsEnabled(checked)
                setLoginSafetyDirty(true)
              }}
            />
          </div>

          <Field label={t('settings.loginSafety.notifyChannels.name')}>
            <Select
              mode="multiple"
              allowClear
              showSearch
              optionFilterProp="label"
              value={lsNotifyChannelIds}
              options={lsChannelOptions}
              placeholder={t('settings.loginSafety.notifyChannels.placeholder')}
              aria-label={t('settings.loginSafety.notifyChannels.name')}
              onChange={(value) => {
                setLsNotifyChannelIds(value)
                setLoginSafetyDirty(true)
              }}
            />
            <Text type="secondary">{t('settings.loginSafety.notifyChannels.desc')}</Text>
          </Field>

          <Field label={t('settings.loginSafety.notifyEmails.name')}>
            <Select
              mode="tags"
              open={false}
              tokenSeparators={[',', '，', ' ']}
              value={lsNotifyEmails}
              placeholder={t('settings.loginSafety.notifyEmails.placeholder')}
              aria-label={t('settings.loginSafety.notifyEmails.name')}
              onChange={(value) => {
                setLsNotifyEmails(value)
                setLoginSafetyDirty(true)
              }}
            />
            <Text type="secondary">{t('settings.loginSafety.notifyEmails.desc')}</Text>
          </Field>

          <div style={{ display: 'flex', gap: spacing.md }}>
            <div style={{ flex: 1, minWidth: 0 }}>
              <Field label={t('settings.loginSafety.ipFailThreshold.name')}>
                <InputNumber
                  value={lsIpFailThreshold}
                  min={1}
                  max={10000}
                  precision={0}
                  aria-label={t('settings.loginSafety.ipFailThreshold.name')}
                  onChange={(value) => {
                    setLsIpFailThreshold(typeof value === 'number' ? value : 20)
                    setLoginSafetyDirty(true)
                  }}
                  style={{ width: 160 }}
                />
                <Text type="secondary">{t('settings.loginSafety.ipFailThreshold.desc')}</Text>
              </Field>
            </div>
            <div style={{ flex: 1, minWidth: 0 }}>
              <Field label={t('settings.loginSafety.ipFailWindow.name')}>
                <InputNumber
                  value={lsIpFailWindow}
                  min={1}
                  max={525600}
                  precision={0}
                  addonAfter={t('settings.loginSafety.minutesUnit')}
                  aria-label={t('settings.loginSafety.ipFailWindow.name')}
                  onChange={(value) => {
                    setLsIpFailWindow(typeof value === 'number' ? value : 10)
                    setLoginSafetyDirty(true)
                  }}
                  style={{ width: 160 }}
                />
              </Field>
            </div>
            <div style={{ flex: 1, minWidth: 0 }}>
              <Field label={t('settings.loginSafety.ipBanMinutes.name')}>
                <InputNumber
                  value={lsIpBanMinutes}
                  min={1}
                  max={525600}
                  precision={0}
                  addonAfter={t('settings.loginSafety.minutesUnit')}
                  aria-label={t('settings.loginSafety.ipBanMinutes.name')}
                  onChange={(value) => {
                    setLsIpBanMinutes(typeof value === 'number' ? value : 30)
                    setLoginSafetyDirty(true)
                  }}
                  style={{ width: 160 }}
                />
              </Field>
            </div>
          </div>

          <div style={{ display: 'flex', gap: spacing.md }}>
            <div style={{ flex: 1, minWidth: 0 }}>
              <Field label={t('settings.loginSafety.distinctUsers.name')}>
                <InputNumber
                  value={lsDistinctUsers}
                  min={1}
                  max={10000}
                  precision={0}
                  aria-label={t('settings.loginSafety.distinctUsers.name')}
                  onChange={(value) => {
                    setLsDistinctUsers(typeof value === 'number' ? value : 5)
                    setLoginSafetyDirty(true)
                  }}
                  style={{ width: 160 }}
                />
                <Text type="secondary">{t('settings.loginSafety.distinctUsers.desc')}</Text>
              </Field>
            </div>
            <div style={{ flex: 1, minWidth: 0 }}>
              <Field label={t('settings.loginSafety.newIpDays.name')}>
                <InputNumber
                  value={lsNewIpDays}
                  min={0}
                  max={3650}
                  precision={0}
                  addonAfter={t('settings.loginSafety.daysUnit')}
                  aria-label={t('settings.loginSafety.newIpDays.name')}
                  onChange={(value) => {
                    setLsNewIpDays(typeof value === 'number' ? value : 90)
                    setLoginSafetyDirty(true)
                  }}
                  style={{ width: 160 }}
                />
                <Text type="secondary">{t('settings.loginSafety.newIpDays.desc')}</Text>
              </Field>
            </div>
            <div style={{ flex: 1, minWidth: 0 }}>
              <Field label={t('settings.loginSafety.travelWindow.name')}>
                <InputNumber
                  value={lsTravelWindow}
                  min={1}
                  max={525600}
                  precision={0}
                  addonAfter={t('settings.loginSafety.minutesUnit')}
                  aria-label={t('settings.loginSafety.travelWindow.name')}
                  onChange={(value) => {
                    setLsTravelWindow(typeof value === 'number' ? value : 120)
                    setLoginSafetyDirty(true)
                  }}
                  style={{ width: 160 }}
                />
              </Field>
            </div>
          </div>

          <div style={{ marginTop: spacing.lg, textAlign: 'left' }}>
            <Button
              type="primary"
              loading={savingLoginSafety}
              disabled={!loginSafetyDirty || loading}
              onClick={handleSaveLoginSafety}
            >
              {t('settings.save')}
            </Button>
          </div>
        </CollapsibleCard>

        <CollapsibleCard title={t('settings.statistics.title')}>
          <Field label={t('settings.statistics.granularity.name')}>
            <Select
              value={statisticsGranularity}
              style={{ width: 220 }}
              aria-label={t('settings.statistics.granularity.name')}
              onChange={(value) => {
                setStatisticsGranularity(value)
                setStatisticsDirty(true)
              }}
              options={[
                { value: 'day', label: t('settings.statistics.granularity.day') },
                { value: 'hour', label: t('settings.statistics.granularity.hour') },
              ]}
            />
            <Text type="secondary">{t('settings.statistics.granularity.desc')}</Text>
          </Field>

          <div style={{ marginTop: spacing.lg, textAlign: 'left' }}>
            <Button
              type="primary"
              loading={savingStatistics}
              disabled={!statisticsDirty || loading}
              onClick={handleSaveStatistics}
            >
              {t('settings.save')}
            </Button>
          </div>
        </CollapsibleCard>

        <CollapsibleCard title={t('settings.memory.title')}>
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              gap: spacing.md,
            }}
          >
            <div style={{ minWidth: 0 }}>
              <Text strong>{t('settings.memory.enable.name')}</Text>
              <br />
              <Text type="secondary">{t('settings.memory.enable.desc')}</Text>
            </div>
            <Switch
              checked={memoryEnabled}
              loading={loading}
              onChange={(checked) => {
                setMemoryEnabled(checked)
                setMemoryDirty(true)
              }}
            />
          </div>

          {memoryEnabled && (
            <div
              style={{
                display: 'flex',
                flexDirection: 'column',
                gap: spacing.md,
                marginTop: spacing.lg,
              }}
            >
              <div
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  gap: spacing.md,
                }}
              >
                <div style={{ minWidth: 0 }}>
                  <Text strong>{t('settings.memory.globalEnable.name')}</Text>
                  <br />
                  <Text type="secondary">{t('settings.memory.globalEnable.desc')}</Text>
                </div>
                <Switch
                  checked={memoryGlobalEnabled}
                  onChange={(checked) => {
                    setMemoryGlobalEnabled(checked)
                    setMemoryDirty(true)
                  }}
                />
              </div>

              <Field label={t('settings.memory.model.name')}>
                <Select
                  value={memoryModelId || undefined}
                  aria-label={t('settings.memory.model.name')}
                  placeholder={t('settings.memory.model.placeholder')}
                  disabled={memoryMigrating}
                  onChange={(value) => {
                    setMemoryModelId(value)
                    setMemoryDirty(true)
                  }}
                  options={memoryModels
                    .filter((item) => item.id)
                    .map((item) => ({ value: String(item.id), label: item.name || '-' }))}
                  style={{ maxWidth: 420 }}
                />
                <Text type="secondary">{t('settings.memory.model.desc')}</Text>
                {memoryLocked && (
                  <Text type="warning">
                    {t('settings.memory.lockedHint', { count: memoryMigration?.totalMemoryCount ?? 0 })}
                  </Text>
                )}
              </Field>
              <Field label={t('settings.memory.dimensions.name')}>
                <InputNumber
                  value={memoryDimensions}
                  min={1}
                  max={2000}
                  precision={0}
                  disabled={memoryMigrating}
                  aria-label={t('settings.memory.dimensions.name')}
                  onChange={(value) => {
                    setMemoryDimensions(typeof value === 'number' ? value : MEMORY_DEFAULT_DIMENSIONS)
                    setMemoryDirty(true)
                  }}
                  style={{ width: 200 }}
                />
                <Text type="secondary">{t('settings.memory.dimensions.desc')}</Text>
              </Field>
              {memoryLocked && (
                <Field label={t('settings.memory.migrate.name')}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: spacing.md }}>
                    <Popconfirm
                      title={t('settings.memory.migrate.confirm')}
                      okText={t('common.ok')}
                      cancelText={t('common.cancel')}
                      onConfirm={handleStartMigration}
                      disabled={startingMigration || memoryMigrating || !memoryModelChanged}
                    >
                      <Button
                        danger
                        loading={startingMigration}
                        disabled={memoryMigrating || !memoryModelChanged}
                      >
                        {t('settings.memory.migrate.start')}
                      </Button>
                    </Popconfirm>
                    {migrationTask && (memoryMigrating || migrationTask.state === 4 || migrationTask.state === 5) && (
                      <div style={{ flex: 1, minWidth: 0 }}>
                        <Progress percent={migrationPercent} size="small" status={memoryMigrating ? 'active' : migrationTask.state === 5 ? 'exception' : 'success'} />
                        <Text type="secondary">{migrationTask.message}</Text>
                      </div>
                    )}
                  </div>
                  <Text type="secondary">{t('settings.memory.migrate.desc')}</Text>
                </Field>
              )}
              <div style={{ display: 'flex', justifyContent: 'flex-start' }}>
                <Popconfirm
                  title={t('settings.memory.clearConfirm')}
                  okText={t('common.ok')}
                  cancelText={t('common.cancel')}
                  onConfirm={handleClearAllMemory}
                  disabled={clearingMemory || memoryMigrating}
                >
                  <Button danger loading={clearingMemory} disabled={memoryMigrating}>
                    {t('settings.memory.clear')}
                  </Button>
                </Popconfirm>
              </div>
            </div>
          )}

          <div style={{ marginTop: spacing.lg, textAlign: 'left' }}>
            <Button
              type="primary"
              loading={savingMemory}
              disabled={!memoryDirty || loading}
              onClick={handleSaveMemory}
            >
              {t('settings.save')}
            </Button>
          </div>
        </CollapsibleCard>
      </div>
    </Page>
  )
}
