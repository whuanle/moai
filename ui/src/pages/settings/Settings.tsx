import { useEffect, useState } from 'react'
import { Button, Input, InputNumber, Popconfirm, Select, Switch, Typography } from 'antd'
import { CaretRightOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { Navigate } from 'react-router'
import { refreshServerInfo } from '@/api/auth'
import {
  getSettings,
  SANDBOX_TTL_LIMITS,
  saveSetting,
  SandboxLimitDefaults,
  SettingKeys,
  resetSystemLogo,
  updateSystemLogo,
} from '@/api/settings'
import { AvatarUpload, Card, feedback, neutralColors, Page, spacing } from '@/design-system'
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
            style={{ color: neutralColors.textTertiary, fontSize: 12 }}
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
  const [wikiDirty, setWikiDirty] = useState(false)
  const [savingSandbox, setSavingSandbox] = useState(false)
  const [sandboxMaxTtl, setSandboxMaxTtl] = useState<number>(SandboxLimitDefaults.maxTtlSeconds)
  const [sandboxMaxCpu, setSandboxMaxCpu] = useState<string>(SandboxLimitDefaults.maxCpu)
  const [sandboxMaxMemory, setSandboxMaxMemory] = useState<string>(SandboxLimitDefaults.maxMemory)
  const [sandboxDirty, setSandboxDirty] = useState(false)
  const [logoPath, setLogoPath] = useState('')
  const [logoUploading, setLogoUploading] = useState(false)
  const [resettingLogo, setResettingLogo] = useState(false)
  const [siteName, setSiteName] = useState('')
  const [siteNameDirty, setSiteNameDirty] = useState(false)
  const [savingSiteName, setSavingSiteName] = useState(false)

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
      setSiteName(valueOf(SettingKeys.systemName).trim())
      setSiteNameDirty(false)
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
    setSavingWiki(true)
    try {
      await saveSetting(SettingKeys.wikiMaxFileSize, String(wikiMaxFileSize ?? 0))
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

  if (!isRoot) {
    return <Navigate to="/dashboard" replace />
  }

  return (
    <Page>
      <div style={{ display: 'flex', flexDirection: 'column', gap: spacing.md }}>
        <CollapsibleCard title={t('settings.siteName.title')} defaultExpanded>
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

        <CollapsibleCard title={t('settings.logo.title')} defaultExpanded>
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
      </div>
    </Page>
  )
}
