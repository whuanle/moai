import { Card, Col, Empty, Row, Typography, theme } from 'antd'
import { ApiOutlined, AppstoreOutlined, FunctionOutlined, ThunderboltOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import type { PluginStatsData } from '@/api/plugin'
import { StatCard } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { PluginAvatar } from './PluginAvatarUpload'

const { Text } = Typography

interface PluginStatsViewProps {
  /** 统计数据（管理端=全团队汇总；团队端=本团队自有插件）. */
  data: PluginStatsData | null
  loading: boolean
  /** 调用量 TOP 名称附加团队名后缀 `{名称}-({团队名})`（仅管理端全团队口径）. */
  showTeamName?: boolean
}

/**
 * 插件统计视图：概览卡 + 调用趋势（统一统计日表按日聚合）+ 类型构成 + 调用量 TOP5 + 分类分布；管理端与团队端共用.
 */
export function PluginStatsView({ data, loading, showTeamName = false }: PluginStatsViewProps) {
  const { t } = useTranslation()
  const { token } = theme.useToken()

  const totalPlugins = data?.totalPlugins ?? 0
  const mcpCount = data?.mcpCount ?? 0
  const openApiCount = data?.openApiCount ?? 0
  const totalCalls = data?.totalCalls ?? 0
  const totalFunctions = data?.totalFunctions ?? 0
  const top = data?.top ?? []
  const trend = data?.trend ?? null

  const typeSegments = [
    { key: 'mcp', label: t('plugins.typeMcp'), count: mcpCount, color: token.colorSuccess },
    { key: 'openApi', label: t('plugins.typeOpenapi'), count: openApiCount, color: token.colorWarning },
    {
      key: 'other',
      label: t('plugins.typeNative'),
      count: totalPlugins - mcpCount - openApiCount,
      color: token.colorPrimary,
    },
  ].filter((r) => r.count > 0)

  const classifyRows = (data?.classifies ?? []).map((c) => ({
    key: `c-${c.classifyId}`,
    label:
      c.classifyName || (c.classifyId === 0 ? t('plugins.classifyUncategorized') : `#${c.classifyId}`),
    count: c.count,
  }))

  const maxCalls = Math.max(1, ...top.map((p) => p.counter))
  const maxClassify = Math.max(1, ...classifyRows.map((r) => r.count))
  const total = Math.max(1, totalPlugins)

  const barTrack = (percent: number, color: string) => (
    <div style={{ flex: 1, height: 8, borderRadius: 999, background: token.colorFillTertiary, overflow: 'hidden', minWidth: 60 }}>
      <div style={{ width: `${percent}%`, height: '100%', borderRadius: 999, background: color }} />
    </div>
  )

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: spacing.md }}>
      <Row gutter={[spacing.md, spacing.md]}>
        <Col xs={24} sm={12} lg={6}>
          <StatCard title={t('plugins.statsTotalPlugins')} value={totalPlugins} loading={loading} icon={<AppstoreOutlined />} />
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <StatCard title={t('plugins.statsMcpPlugins')} value={mcpCount} loading={loading} icon={<ApiOutlined />} />
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <StatCard title={t('plugins.statsOpenApiPlugins')} value={openApiCount} loading={loading} icon={<FunctionOutlined />} />
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <StatCard title={t('plugins.statsTotalCalls')} value={totalCalls} loading={loading} icon={<ThunderboltOutlined />} />
        </Col>
      </Row>

      <Row gutter={[spacing.md, spacing.md]}>
        <Col xs={24} lg={14}>
          <Card
            title={t('plugins.statsTrend')}
            extra={<Text type="secondary">{t('plugins.statsTrendRange', { total: trend?.total ?? 0, days: trend?.items.length ?? 0 })}</Text>}
          >
            {!trend || trend.items.every((p) => p.count === 0) ? (
              <Empty
                image={Empty.PRESENTED_IMAGE_SIMPLE}
                description={t('plugins.statsTrendEmpty')}
                style={{ padding: token.paddingLG }}
              />
            ) : (
              <>
                <div style={{ display: 'flex', alignItems: 'flex-end', gap: 3, height: 140, marginBottom: spacing.sm }}>
                  {trend.items.map((p) => {
                    const max = Math.max(...trend.items.map((x) => x.count), 1)
                    return (
                      <div
                        key={p.date}
                        title={`${p.date}：${p.count}`}
                        style={{
                          flex: 1,
                          minWidth: 4,
                          height: `${Math.max((p.count / max) * 100, 2)}%`,
                          minHeight: 3,
                          borderRadius: '3px 3px 0 0',
                          background: p.count > 0 ? token.colorPrimary : token.colorFillTertiary,
                          opacity: p.count > 0 ? 1 : 0.6,
                        }}
                      />
                    )
                  })}
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                  <Text type="secondary" style={{ fontSize: 12 }}>{trend.items[0]?.date}</Text>
                  <Text type="secondary" style={{ fontSize: 12 }}>{trend.items[trend.items.length - 1]?.date}</Text>
                </div>
              </>
            )}
          </Card>
        </Col>
        <Col xs={24} lg={10}>
          <Card
            title={t('plugins.statsByType')}
            extra={<Text type="secondary">{t('plugins.statsFunctionsPrefix', { total: totalFunctions })}</Text>}
          >
            <div
              style={{
                display: 'flex',
                height: 12,
                borderRadius: 999,
                overflow: 'hidden',
                gap: 2,
                background: token.colorFillTertiary,
                marginBottom: spacing.md,
              }}
            >
              {typeSegments.map((s) => (
                <div key={s.key} style={{ flex: s.count, background: s.color }} />
              ))}
            </div>
            {typeSegments.map((s) => (
              <div key={s.key} style={{ display: 'flex', alignItems: 'center', gap: spacing.sm, marginBottom: spacing.xs }}>
                <span style={{ width: 9, height: 9, borderRadius: 3, background: s.color, flexShrink: 0 }} />
                <span>{s.label}</span>
                <span style={{ marginLeft: 'auto', fontWeight: 600, fontVariantNumeric: 'tabular-nums' }}>{s.count}</span>
                <span style={{ width: 44, textAlign: 'right', color: token.colorTextTertiary, fontVariantNumeric: 'tabular-nums' }}>
                  {Math.round((s.count / total) * 100)}%
                </span>
              </div>
            ))}
          </Card>
        </Col>
      </Row>

      <Row gutter={[spacing.md, spacing.md]}>
        <Col xs={24} lg={12}>
          <Card title={t('plugins.statsTop')} extra={<Text type="secondary">{t('plugins.statsTopSub')}</Text>}>
            {top.length === 0 ? (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('plugins.empty')} />
            ) : (
              top.map((p) => {
                const label = showTeamName && p.teamName ? `${p.name}-(${p.teamName})` : p.name
                return (
                  <div key={p.id || p.name} style={{ display: 'flex', alignItems: 'center', gap: spacing.sm, marginBottom: spacing.sm }}>
                    <PluginAvatar objectKey={p.avatarPath} title={p.name} size={26} />
                    <Text ellipsis={{ tooltip: true }} style={{ width: 170, flexShrink: 0 }}>
                      {label}
                    </Text>
                    {barTrack((p.counter / maxCalls) * 100, token.colorPrimary)}
                    <Text type="secondary" style={{ width: 56, textAlign: 'right', flexShrink: 0, fontVariantNumeric: 'tabular-nums' }}>
                      {p.counter}
                    </Text>
                  </div>
                )
              })
            )}
          </Card>
        </Col>
        <Col xs={24} lg={12}>
          <Card
            title={t('plugins.statsByClassify')}
            extra={<Text type="secondary">{t('plugins.statsPluginsUnit', { total: totalPlugins })}</Text>}
          >
            {classifyRows.length === 0 ? (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('plugins.empty')} />
            ) : (
              classifyRows.map((r) => (
                <div key={r.key} style={{ display: 'flex', alignItems: 'center', gap: spacing.sm, marginBottom: spacing.sm }}>
                  <Text style={{ width: 120, flexShrink: 0 }} ellipsis={{ tooltip: true }}>{r.label}</Text>
                  {barTrack((r.count / maxClassify) * 100, token.colorPrimary)}
                  <Text type="secondary" style={{ width: 32, textAlign: 'right', flexShrink: 0, fontVariantNumeric: 'tabular-nums' }}>
                    {r.count}
                  </Text>
                </div>
              ))
            )}
          </Card>
        </Col>
      </Row>
    </div>
  )
}
