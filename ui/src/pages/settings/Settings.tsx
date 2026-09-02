import { useEffect, useState } from 'react'
import { Button, Switch, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { Navigate } from 'react-router'
import { getSettings, saveSetting, SettingKeys } from '@/api/settings'
import { Card, feedback, Page } from '@/design-system'
import { useAppStore } from '@/store/app'

const { Text } = Typography

export function Settings() {
  const { t } = useTranslation()
  const isAdmin = useAppStore((state) => state.userInfo?.isAdmin === true)

  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [autoRegister, setAutoRegister] = useState(false)
  const [dirty, setDirty] = useState(false)

  useEffect(() => {
    void load()
  }, [])

  async function load() {
    setLoading(true)
    try {
      const res = await getSettings()
      const item = res?.items?.find((s: { key?: string | null }) => s.key === SettingKeys.oauthAutoRegister)
      setAutoRegister(item?.value === 'true')
      setDirty(false)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }

  async function handleSave() {
    setSaving(true)
    try {
      await saveSetting(SettingKeys.oauthAutoRegister, autoRegister ? 'true' : 'false')
      feedback.success(t('settings.saveSuccess'))
      setDirty(false)
    } catch {
      // 保存失败时重新加载，恢复为数据库中的真实值（错误已由全局请求中间件统一提示）
      void load()
    } finally {
      setSaving(false)
    }
  }

  if (!isAdmin) {
    return <Navigate to="/dashboard" replace />
  }

  return (
    <Page>
      <Card title={t('settings.generalTitle')}>
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            gap: 16,
          }}
        >
          <div style={{ minWidth: 0 }}>
            <Text strong>{t('settings.oauthAutoRegister.name')}</Text>
            <br />
            <Text type="secondary">{t('settings.oauthAutoRegister.desc')}</Text>
          </div>
          <Switch
            checked={autoRegister}
            loading={loading}
            onChange={(checked) => {
              setAutoRegister(checked)
              setDirty(true)
            }}
          />
        </div>

        <div style={{ marginTop: 24, textAlign: 'right' }}>
          <Button type="primary" loading={saving} disabled={!dirty || loading} onClick={handleSave}>
            {t('settings.save')}
          </Button>
        </div>
      </Card>
    </Page>
  )
}
