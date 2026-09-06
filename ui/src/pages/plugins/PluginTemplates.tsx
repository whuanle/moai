import { useCallback, useEffect, useState } from 'react'
import { Button, Col, Empty, Row, Spin, Tag, Typography } from 'antd'
import { ArrowLeftOutlined, ReloadOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { Navigate, useNavigate } from 'react-router'
import { pluginApi, type DynamicPluginTemplate } from '@/api/plugin'
import { Card, Page } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { useAppStore } from '@/store/app'

const { Text, Paragraph } = Typography

export function PluginTemplates() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const isAdmin = useAppStore((state) => state.userInfo?.isAdmin === true)

  const [templates, setTemplates] = useState<DynamicPluginTemplate[]>([])
  const [loading, setLoading] = useState(true)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      setTemplates(await pluginApi.getDynamicTemplates())
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    if (!isAdmin) return
    void load()
  }, [isAdmin, load])

  if (!isAdmin) {
    return <Navigate to="/dashboard" replace />
  }

  return (
    <Page
      title={t('plugins.templateList')}
      subtitle={t('plugins.templateListSubtitle')}
      breadcrumb={[
        { title: t('plugins.tabDynamic') },
        { title: t('plugins.templateList') },
      ]}
      extra={
        <Button icon={<ArrowLeftOutlined />} onClick={() => navigate('/plugin?tab=dynamic')}>
          {t('plugins.backToPlugins')}
        </Button>
      }
    >
      <div style={{ marginBottom: spacing.md }}>
        <Button icon={<ReloadOutlined />} onClick={load} loading={loading}>
          {t('plugins.refresh')}
        </Button>
      </div>
      {loading ? (
        <div style={{ textAlign: 'center', padding: spacing.xxl * 2 }}>
          <Spin />
        </div>
      ) : templates.length === 0 ? (
        <Empty description={t('plugins.templateEmpty')} />
      ) : (
        <Row gutter={[16, 16]}>
          {templates.map((tp) => (
            <Col xs={24} sm={12} lg={8} xl={6} key={tp.key}>
              <Card>
                <div style={{ marginBottom: spacing.xs, display: 'flex', alignItems: 'center', gap: spacing.sm }}>
                  <Tag color="blue">{tp.key}</Tag>
                </div>
                <Text strong style={{ display: 'block', fontSize: 16 }}>
                  {tp.name || '-'}
                </Text>
                <Paragraph type="secondary" style={{ marginTop: spacing.xs, marginBottom: 0, minHeight: 44 }}>
                  {tp.description || t('plugins.templateNoDescription')}
                </Paragraph>
              </Card>
            </Col>
          ))}
        </Row>
      )}
    </Page>
  )
}
