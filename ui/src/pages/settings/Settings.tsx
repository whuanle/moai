import { useEffect, useState } from 'react'
import { Button, Input, InputNumber, Popconfirm, Progress, Select, Spin, Switch, Typography, Upload, theme as antdTheme } from 'antd'
import { CaretRightOutlined, MailOutlined, UploadOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { Navigate } from 'react-router'
import { refreshServerInfo } from '@/api/auth'
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
  MEMORY_DEFAULT_DIMENSIONS,
  normalizeWikiFileTypes,
  parseWikiFileTypes,
  SANDBOX_TTL_LIMITS,
  saveSetting,
  sendTestEmail,
  SandboxLimitDefaults,
  SettingKeys,
  SMTP_DEFAULTS,
  SMTP_SECURITY_OPTIONS,
  WIKI_DEFAULT_FILE_TYPES,
  resetLoginBackground,
  resetSystemLogo,
  updateLoginBackground,
  updateSystemLogo,
  type SmtpSecurity,
} from '@/api/settings'
import { AvatarUpload, Card, feedback, useNeutralColors, Page, spacing } from '@/design-system'
import { radius } from '@/design-system/theme'
import { DEFAULT_LOGO_SRC } from '@/layouts/useSystemLogo'
import { useAppStore } from '@/store/app'
import { parseCpuMillicores, parseMemoryBytes } from '@/utils/sandboxQuantity'
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
  const [sandboxMaxTtl, setSandboxMaxTtl] = useState<number>(SandboxLimitDefaults.maxTtlSeconds)
  const [sandboxMaxCpu, setSandboxMaxCpu] = useState<string>(SandboxLimitDefaults.maxCpu)
  const [sandboxMaxMemory, setSandboxMaxMemory] = useState<string>(SandboxLimitDefaults.maxMemory)
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
  const [emailHost, setEmailHost] = useState('')
  const [emailPort, setEmailPort] = useState<number>(SMTP_DEFAULTS.port)
  const [emailSecurity, setEmailSecurity] = useState<SmtpSecurity>(SMTP_DEFAULTS.security)
  const [emailUsername, setEmailUsername] = useState('')
  const [emailPassword, setEmailPassword] = useState('')
  const [emailFromAddress, setEmailFromAddress] = useState('')
  const [emailFromName, setEmailFromName] = useState('')
  const [registerCodeEnabled, setRegisterCodeEnabled] = useState(false)
  const [registerCodeSubject, setRegisterCodeSubject] = useState('')
  const [registerCodeBody, setRegisterCodeBody] = useState('')
  const [resetCodeSubject, setResetCodeSubject] = useState('')
  const [resetCodeBody, setResetCodeBody] = useState('')
  const [emailDirty, setEmailDirty] = useState(false)
  const [testTo, setTestTo] = useState('')
  const [sendingTest, setSendingTest] = useState(false)
  const [savingMemory, setSavingMemory] = useState(false)
  const [clearingMemory, setClearingMemory] = useState(false)
  const [memoryEnabled, setMemoryEnabled] = useState(false)
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
      setSandboxDirty(false)
      setLogoPath(valueOf(SettingKeys.systemLogo).trim())
      setLoginBgPath(valueOf(SettingKeys.loginBackground).trim())
      setSiteName(valueOf(SettingKeys.systemName).trim())
      setSiteNameDirty(false)
      setEmailEnabled(valueOf(SettingKeys.smtpEnabled) === 'true')
      setEmailHost(valueOf(SettingKeys.smtpHost).trim())
      const parsedPort = Number.parseInt(valueOf(SettingKeys.smtpPort), 10)
      setEmailPort(
        Number.isFinite(parsedPort) && parsedPort >= 1 && parsedPort <= 65535 ? parsedPort : SMTP_DEFAULTS.port,
      )
      const security = valueOf(SettingKeys.smtpSecurity).trim().toLowerCase()
      setEmailSecurity((SMTP_SECURITY_OPTIONS as readonly string[]).includes(security) ? (security as SmtpSecurity) : SMTP_DEFAULTS.security)
      setEmailUsername(valueOf(SettingKeys.smtpUsername).trim())
      setEmailPassword(valueOf(SettingKeys.smtpPassword))
      setEmailFromAddress(valueOf(SettingKeys.smtpFromAddress).trim())
      setEmailFromName(valueOf(SettingKeys.smtpFromName).trim())
      setRegisterCodeEnabled(valueOf(SettingKeys.registerEmailCodeEnabled) === 'true')
      setRegisterCodeSubject(valueOf(SettingKeys.smtpRegisterCodeSubject))
      setRegisterCodeBody(valueOf(SettingKeys.smtpRegisterCodeBody))
      setResetCodeSubject(valueOf(SettingKeys.smtpResetCodeSubject))
      setResetCodeBody(valueOf(SettingKeys.smtpResetCodeBody))
      setEmailDirty(false)
      setTestTo('')
      setMemoryEnabled(valueOf(SettingKeys.memoryEnabled) === 'true')
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
    setSavingSandbox(true)
    try {
      await saveSetting(SettingKeys.sandboxMaxTtl, String(sandboxMaxTtl))
      await saveSetting(SettingKeys.sandboxMaxCpu, sandboxMaxCpu.trim())
      await saveSetting(SettingKeys.sandboxMaxMemory, sandboxMaxMemory.trim())
      feedback.success(t('settings.saveSuccess'))
      setSandboxDirty(false)
    } catch {
      // 保存失败时重新加载，恢复为数据库中的真实值（错误已由全局请求中间件统一提示）
      void load()
    } finally {
      setSavingSandbox(false)
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
    if (
      !Number.isInteger(emailPort) ||
      emailPort < 1 ||
      emailPort > 65535
    ) {
      feedback.error(t('settings.email.invalidPort'))
      return
    }
    setSavingEmail(true)
    try {
      // 先写开关再写连接项，开启状态下连接项随后覆盖生效
      await saveSetting(SettingKeys.smtpEnabled, emailEnabled ? 'true' : 'false')
      await saveSetting(SettingKeys.smtpHost, emailHost.trim())
      await saveSetting(SettingKeys.smtpPort, String(emailPort))
      await saveSetting(SettingKeys.smtpSecurity, emailSecurity)
      await saveSetting(SettingKeys.smtpUsername, emailUsername.trim())
      await saveSetting(SettingKeys.smtpPassword, emailPassword)
      await saveSetting(SettingKeys.smtpFromAddress, emailFromAddress.trim())
      await saveSetting(SettingKeys.smtpFromName, emailFromName.trim())
      await saveSetting(SettingKeys.registerEmailCodeEnabled, registerCodeEnabled ? 'true' : 'false')
      await saveSetting(SettingKeys.smtpRegisterCodeSubject, registerCodeSubject.trim())
      await saveSetting(SettingKeys.smtpRegisterCodeBody, registerCodeBody)
      await saveSetting(SettingKeys.smtpResetCodeSubject, resetCodeSubject.trim())
      await saveSetting(SettingKeys.smtpResetCodeBody, resetCodeBody)
      feedback.success(t('settings.saveSuccess'))
      setEmailDirty(false)
    } catch {
      // 保存失败时重新加载，恢复为数据库中的真实值（错误已由全局请求中间件统一提示）
      void load()
    } finally {
      setSavingEmail(false)
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
      if (memoryLocked) {
        // 已有记忆时模型/维度被锁定（更换须走迁移重建），保存仅提交开关
        await saveSetting(SettingKeys.memoryEnabled, memoryEnabled ? 'true' : 'false')
      } else {
        // 先写模型与维度，再写开关：开启校验要求模型已保存
        await saveSetting(SettingKeys.memoryEmbeddingModel, memoryModelId)
        await saveSetting(SettingKeys.memoryEmbeddingDimensions, String(memoryDimensions))
        await saveSetting(SettingKeys.memoryEnabled, memoryEnabled ? 'true' : 'false')
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

  async function handleSendTestEmail() {
    // 测试邮件使用已保存的服务端配置，本地未保存的修改不参与发送
    if (emailDirty) {
      feedback.error(t('settings.email.testSaveFirst'))
      return
    }
    const to = testTo.trim()
    if (!to) {
      feedback.error(t('settings.email.testToRequired'))
      return
    }
    setSendingTest(true)
    try {
      await sendTestEmail(to)
      feedback.success(t('settings.email.testSuccess'))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSendingTest(false)
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

          <div style={{ marginTop: spacing.lg, textAlign: 'right' }}>
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

          <div style={{ marginTop: spacing.lg, textAlign: 'right' }}>
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

          <div style={{ marginTop: spacing.lg, textAlign: 'right' }}>
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
              <div style={{ display: 'flex', gap: spacing.md }}>
                <div style={{ flex: 2, minWidth: 0 }}>
                  <Field label={t('settings.email.host.name')}>
                    <Input
                      value={emailHost}
                      placeholder={t('settings.email.host.placeholder')}
                      aria-label={t('settings.email.host.name')}
                      onChange={(e) => {
                        setEmailHost(e.target.value)
                        setEmailDirty(true)
                      }}
                    />
                  </Field>
                </div>
                <div style={{ width: 160, flexShrink: 0 }}>
                  <Field label={t('settings.email.port.name')}>
                    <InputNumber
                      value={emailPort}
                      min={1}
                      max={65535}
                      precision={0}
                      aria-label={t('settings.email.port.name')}
                      onChange={(value) => {
                        setEmailPort(typeof value === 'number' ? value : SMTP_DEFAULTS.port)
                        setEmailDirty(true)
                      }}
                      style={{ width: '100%' }}
                    />
                  </Field>
                </div>
                <div style={{ width: 260, flexShrink: 0 }}>
                  <Field label={t('settings.email.security.name')}>
                    <Select
                      value={emailSecurity}
                      aria-label={t('settings.email.security.name')}
                      onChange={(value) => {
                        setEmailSecurity(value)
                        setEmailDirty(true)
                      }}
                      options={[
                        { value: 'ssl', label: t('settings.email.securitySsl') },
                        { value: 'starttls', label: t('settings.email.securityStarttls') },
                        { value: 'none', label: t('settings.email.securityNone') },
                      ]}
                      style={{ width: '100%' }}
                    />
                  </Field>
                </div>
              </div>
              <div style={{ display: 'flex', gap: spacing.md }}>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <Field label={t('settings.email.username.name')}>
                    <Input
                      value={emailUsername}
                      placeholder={t('settings.email.username.placeholder')}
                      aria-label={t('settings.email.username.name')}
                      onChange={(e) => {
                        setEmailUsername(e.target.value)
                        setEmailDirty(true)
                      }}
                    />
                  </Field>
                </div>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <Field label={t('settings.email.password.name')}>
                    <Input.Password
                      value={emailPassword}
                      placeholder={t('settings.email.password.placeholder')}
                      aria-label={t('settings.email.password.name')}
                      onChange={(e) => {
                        setEmailPassword(e.target.value)
                        setEmailDirty(true)
                      }}
                    />
                  </Field>
                </div>
              </div>
              <div style={{ display: 'flex', gap: spacing.md }}>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <Field label={t('settings.email.fromAddress.name')}>
                    <Input
                      value={emailFromAddress}
                      placeholder={t('settings.email.fromAddress.placeholder')}
                      aria-label={t('settings.email.fromAddress.name')}
                      onChange={(e) => {
                        setEmailFromAddress(e.target.value)
                        setEmailDirty(true)
                      }}
                    />
                  </Field>
                </div>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <Field label={t('settings.email.fromName.name')}>
                    <Input
                      value={emailFromName}
                      placeholder={t('settings.email.fromName.placeholder')}
                      aria-label={t('settings.email.fromName.name')}
                      onChange={(e) => {
                        setEmailFromName(e.target.value)
                        setEmailDirty(true)
                      }}
                    />
                  </Field>
                </div>
              </div>
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
                  onChange={(checked) => {
                    setRegisterCodeEnabled(checked)
                    setEmailDirty(true)
                  }}
                />
              </div>
              <Field label={t('settings.email.registerCodeSubject.name')}>
                <Input
                  value={registerCodeSubject}
                  placeholder={t('settings.email.registerCodeSubject.placeholder')}
                  aria-label={t('settings.email.registerCodeSubject.name')}
                  maxLength={500}
                  onChange={(e) => {
                    setRegisterCodeSubject(e.target.value)
                    setEmailDirty(true)
                  }}
                />
              </Field>
              <Field label={t('settings.email.registerCodeBody.name')}>
                <Input.TextArea
                  value={registerCodeBody}
                  placeholder={t('settings.email.registerCodeBody.placeholder')}
                  aria-label={t('settings.email.registerCodeBody.name')}
                  rows={3}
                  maxLength={2000}
                  onChange={(e) => {
                    setRegisterCodeBody(e.target.value)
                    setEmailDirty(true)
                  }}
                />
              </Field>
              <Field label={t('settings.email.resetCodeSubject.name')}>
                <Input
                  value={resetCodeSubject}
                  placeholder={t('settings.email.resetCodeSubject.placeholder')}
                  aria-label={t('settings.email.resetCodeSubject.name')}
                  maxLength={500}
                  onChange={(e) => {
                    setResetCodeSubject(e.target.value)
                    setEmailDirty(true)
                  }}
                />
              </Field>
              <Field label={t('settings.email.resetCodeBody.name')}>
                <Input.TextArea
                  value={resetCodeBody}
                  placeholder={t('settings.email.resetCodeBody.placeholder')}
                  aria-label={t('settings.email.resetCodeBody.name')}
                  rows={3}
                  maxLength={2000}
                  onChange={(e) => {
                    setResetCodeBody(e.target.value)
                    setEmailDirty(true)
                  }}
                />
                <Text type="secondary">{t('settings.email.templateTip')}</Text>
              </Field>
            </div>
          )}

          {emailEnabled && (
            <Field label={t('settings.email.testTo.name')}>
              <div style={{ display: 'flex', gap: spacing.md, alignItems: 'center' }}>
                <Input
                  value={testTo}
                  placeholder={t('settings.email.testTo.placeholder')}
                  aria-label={t('settings.email.testTo.name')}
                  onChange={(e) => setTestTo(e.target.value)}
                  style={{ maxWidth: 360 }}
                />
                <Button
                  icon={<MailOutlined />}
                  loading={sendingTest}
                  disabled={loading}
                  onClick={handleSendTestEmail}
                >
                  {t('settings.email.testSend')}
                </Button>
              </div>
              {emailDirty && <Text type="secondary">{t('settings.email.testSaveFirst')}</Text>}
            </Field>
          )}

          <div style={{ marginTop: spacing.lg, textAlign: 'right' }}>
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

          <div style={{ marginTop: spacing.lg, textAlign: 'right' }}>
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

          <div style={{ marginTop: spacing.lg, textAlign: 'right' }}>
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
