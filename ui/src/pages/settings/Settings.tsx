import { useEffect, useState } from 'react'
import { Button, Input, InputNumber, Select, Switch, Typography } from 'antd'
import { CaretRightOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { Navigate } from 'react-router'
import { getSettings, saveSetting, SettingKeys } from '@/api/settings'
import { Card, feedback, neutralColors, Page, spacing } from '@/design-system'
import { useAppStore } from '@/store/app'

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

  if (!isRoot) {
    return <Navigate to="/dashboard" replace />
  }

  return (
    <Page>
      <div style={{ display: 'flex', flexDirection: 'column', gap: spacing.md }}>
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
      </div>
    </Page>
  )
}
