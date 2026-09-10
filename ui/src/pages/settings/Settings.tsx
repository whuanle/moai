import { useEffect, useState } from 'react'
import { Button, Input, Switch, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { Navigate } from 'react-router'
import { getSettings, saveSetting, SettingKeys } from '@/api/settings'
import { Card, feedback, Page, spacing } from '@/design-system'
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
      {children}
    </div>
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
  const [graphDirty, setGraphDirty] = useState(false)

  useEffect(() => {
    void load()
  }, [])

  async function load() {
    setLoading(true)
    try {
      const res = await getSettings()
      const items: SettingItem[] = res?.items ?? []
      const valueOf = (key: string) => items.find((s) => s.key === key)?.value ?? ''
      setGraphEnabled(valueOf(SettingKeys.neo4jEnabled) === 'true')
      setGraphUri(valueOf(SettingKeys.neo4jUri))
      setGraphUsername(valueOf(SettingKeys.neo4jUsername))
      setGraphPassword(valueOf(SettingKeys.neo4jPassword))
      setGraphDirty(false)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }

  async function handleSaveGraph() {
    setSavingGraph(true)
    try {
      await saveSetting(SettingKeys.neo4jEnabled, graphEnabled ? 'true' : 'false')
      if (graphEnabled) {
        await saveSetting(SettingKeys.neo4jUri, graphUri)
        await saveSetting(SettingKeys.neo4jUsername, graphUsername)
        await saveSetting(SettingKeys.neo4jPassword, graphPassword)
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

  if (!isRoot) {
    return <Navigate to="/dashboard" replace />
  }

  return (
    <Page>
      <Card title={t('settings.knowledgeGraph.title')}>
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
      </Card>
    </Page>
  )
}
