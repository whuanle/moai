import { useCallback, useEffect, useState } from 'react'
import { Button, Col, Empty, Row, Space, Spin, Tag, Typography } from 'antd'
import { ArrowLeftOutlined, PlusOutlined, ReloadOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { Navigate, useNavigate, useSearchParams } from 'react-router'
import { classifyApi, type PluginClassify } from '@/api/classify'
import { pluginApi, type DynamicPluginTemplate } from '@/api/plugin'
import { getTeamDynamicTemplates, getTeamPlugins } from '@/api/team-plugin'
import { Card, Page } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { useAppStore } from '@/store/app'
import { DynamicPluginInstanceModal } from './components/DynamicPluginInstanceModal'

const { Text, Paragraph } = Typography

/** 模板列表页：无 teamId 为系统模式（管理员）；带 teamId 为团队模式（可管理成员），实例数分别按全站/本团队统计. */
export function PluginTemplates() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const teamIdParam = searchParams.get('teamId')
  const parsedTeamId = teamIdParam != null ? Number(teamIdParam) : NaN
  const isTeamMode = Number.isInteger(parsedTeamId) && parsedTeamId > 0
  const teamId = isTeamMode ? parsedTeamId : undefined
  const isAdmin = useAppStore((state) => state.userInfo?.isAdmin === true)

  const [templates, setTemplates] = useState<DynamicPluginTemplate[]>([])
  const [instanceCounts, setInstanceCounts] = useState<Record<string, number>>({})
  const [existingKeys, setExistingKeys] = useState<string[]>([])
  const [teamDenied, setTeamDenied] = useState(false)
  const [loading, setLoading] = useState(true)
  const [classifies, setClassifies] = useState<PluginClassify[]>([])
  const [createTemplateKey, setCreateTemplateKey] = useState<string | null>(null)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      if (isTeamMode && teamId != null) {
        const [tplRes, teamRes] = await Promise.all([getTeamDynamicTemplates(teamId), getTeamPlugins(teamId)])
        if (teamRes.canManage !== true) {
          setTeamDenied(true)
          return
        }
        const items = teamRes.items ?? []
        const instances = items.filter((i) => i.kind === 'dynamic' && i.isTeamOwned === true)
        setInstanceCounts(countByTemplate(instances.map((i) => ({ key: i.templeteKey }))))
        // 团队侧查重含系统插件 key（后端全局保留语义），与团队插件面板口径一致
        setExistingKeys(items.map((i) => (i.instanceKey ?? i.pluginName) ?? '').filter(Boolean))
        setTemplates((tplRes.items ?? []).filter((i) => i.isDynamic === true))
      } else {
        const [tpl, instances] = await Promise.all([
          pluginApi.getDynamicTemplates(),
          pluginApi.getManagePlugins('dynamic'),
        ])
        setInstanceCounts(countByTemplate(instances.map((i) => ({ key: i.templeteKey }))))
        setExistingKeys(instances.map((i) => i.pluginName ?? '').filter(Boolean))
        setTemplates(tpl)
      }
    } catch {
      if (isTeamMode) setTeamDenied(true)
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [isTeamMode, teamId])

  useEffect(() => {
    void load()
  }, [load])

  // 分类选项仅站点管理员可拉取（与插件管理页口径一致）
  useEffect(() => {
    if (!isAdmin) return
    classifyApi
      .getPluginClassifies()
      .then((list) => setClassifies(list))
      .catch(() => {
        // 错误已由全局请求中间件统一提示
      })
  }, [isAdmin])

  if (!isTeamMode && !isAdmin) {
    return <Navigate to="/apps" replace />
  }

  if (isTeamMode && teamDenied) {
    return <Navigate to={`/team/${teamId}/plugins`} replace />
  }

  const backPath = isTeamMode ? `/team/${teamId}/plugins` : '/plugin?tab=dynamic'

  return (
    <Page
      title={t('plugins.templateList')}
      subtitle={t('plugins.templateListSubtitle')}
      breadcrumb={[
        { title: t('plugins.tabDynamic') },
        { title: t('plugins.templateList') },
      ]}
      extra={
        <Space>
          <Button icon={<ReloadOutlined />} onClick={load} loading={loading}>
            {t('plugins.refresh')}
          </Button>
          <Button icon={<ArrowLeftOutlined />} onClick={() => navigate(backPath)}>
            {t('plugins.backToPlugins')}
          </Button>
        </Space>
      }
    >
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
              <Card style={{ height: '100%' }}>
                <div style={{ marginBottom: spacing.xs, display: 'flex', alignItems: 'center', gap: spacing.sm }}>
                  <Tag color="blue">{tp.key}</Tag>
                </div>
                <Text strong style={{ display: 'block', fontSize: 16 }}>
                  {tp.name || '-'}
                </Text>
                <Paragraph type="secondary" style={{ marginTop: spacing.xs, marginBottom: 0, minHeight: 44 }}>
                  {tp.description || t('plugins.templateNoDescription')}
                </Paragraph>
                <div style={{ display: 'flex', alignItems: 'center', gap: spacing.sm, marginTop: spacing.sm }}>
                  <Text type="secondary">
                    {t('plugins.templateInstanceCount', { total: instanceCounts[tp.key ?? ''] ?? 0 })}
                  </Text>
                  <Button
                    type="primary"
                    size="small"
                    icon={<PlusOutlined />}
                    style={{ marginLeft: 'auto' }}
                    onClick={() => setCreateTemplateKey(tp.key ?? null)}
                  >
                    {t('plugins.templateCreate')}
                  </Button>
                </div>
              </Card>
            </Col>
          ))}
        </Row>
      )}

      <DynamicPluginInstanceModal
        open={createTemplateKey != null}
        scope={isTeamMode ? 'team' : 'system'}
        teamId={teamId}
        templates={templates}
        classifies={classifies}
        existingKeys={existingKeys}
        presetTemplateKey={createTemplateKey}
        onSaved={load}
        onClose={() => setCreateTemplateKey(null)}
      />
    </Page>
  )
}

function countByTemplate(instances: { key: string | null | undefined }[]): Record<string, number> {
  const counts: Record<string, number> = {}
  for (const item of instances) {
    if (!item.key) continue
    counts[item.key] = (counts[item.key] ?? 0) + 1
  }
  return counts
}
