import { useCallback, useEffect, useMemo, useState } from 'react'
import { Card, Col, Empty, Row, Typography, theme } from 'antd'
import { ApiOutlined, AppstoreOutlined, FunctionOutlined, ThunderboltOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { classifyLabel, type PluginClassify } from '@/api/classify'
import { pluginApi } from '@/api/plugin'
import { getUsageTrend, type UsageTrend } from '@/api/statistics'
import { StatCard } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { PluginAvatar } from './components/PluginAvatarUpload'

const { Text } = Typography

/** Kiota 计数字段为 string | null，统计前归一为数值. */
function toNum(v: string | number | null | undefined): number {
  const n = Number(v ?? 0)
  return Number.isFinite(n) ? n : 0
}

interface PluginStatsPanelProps {
  classifies: PluginClassify[]
}

/**
 * 插件统计页签：概览卡 + 调用趋势（统一统计日表按日聚合）+ 类型构成 + 分类分布 + 调用量 TOP5.
 */
export function PluginStatsPanel({ classifies }: PluginStatsPanelProps) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const [items, setItems] = useState<Awaited<ReturnType<typeof pluginApi.getManagePlugins>>>([])
  const [trend, setTrend] = useState<UsageTrend | null>(null)
  const [loading, setLoading] = useState(true)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const [list, trendData] = await Promise.all([
        pluginApi.getManagePlugins(),
        getUsageTrend('plugin-usage', 30).catch(() => null),
      ])
      setItems(list)
      setTrend(trendData)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void load()
  }, [load])

  const stats = useMemo(() => {
    let mcp = 0
    let openApi = 0
    let calls = 0
    let functions = 0
    const classifyCounts = new Map<number, number>()
    for (const item of items) {
      if (item.type === 0) mcp += 1
      else if (item.type === 1) openApi += 1
      calls += toNum(item.counter)
      functions += item.functionCount ?? 0
      const key = item.classifyId || 0
      classifyCounts.set(key, (classifyCounts.get(key) ?? 0) + 1)
    }
    const top = [...items]
      .sort((a, b) => toNum(b.counter) - toNum(a.counter))
      .slice(0, 5)
    return { total: items.length, mcp, openApi, calls, functions, classifyCounts, top }
  }, [items])

  const typeSegments = useMemo(() => {
    const rows = [
      { key: 'mcp', label: t('plugins.typeMcp'), count: stats.mcp, color: token.colorSuccess },
      { key: 'openApi', label: t('plugins.typeOpenapi'), count: stats.openApi, color: token.colorWarning },
      {
        key: 'other',
        label: t('plugins.typeNative'),
        count: stats.total - stats.mcp - stats.openApi,
        color: token.colorPrimary,
      },
    ]
    return rows.filter((r) => r.count > 0)
  }, [stats, t, token])

  const classifyRows = useMemo(() => {
    const rows = classifies
      .map((c) => ({ key: `c-${c.classifyId}`, label: classifyLabel(c), count: stats.classifyCounts.get(c.classifyId ?? -1) ?? 0 }))
      .filter((r) => r.count > 0)
    const uncategorized = stats.classifyCounts.get(0) ?? 0
    if (uncategorized > 0) {
      rows.push({ key: 'c-0', label: t('plugins.classifyUncategorized'), count: uncategorized })
    }
    return rows.sort((a, b) => b.count - a.count)
  }, [classifies, stats.classifyCounts, t])

  const maxCalls = Math.max(1, ...stats.top.map((p) => toNum(p.counter)))
  const maxClassify = Math.max(1, ...classifyRows.map((r) => r.count))
  const total = Math.max(1, stats.total)

  const barTrack = (percent: number, color: string) => (
    <div style={{ flex: 1, height: 8, borderRadius: 999, background: token.colorFillTertiary, overflow: 'hidden', minWidth: 60 }}>
      <div style={{ width: `${percent}%`, height: '100%', borderRadius: 999, background: color }} />
    </div>
  )

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: spacing.md }}>
      <Row gutter={[spacing.md, spacing.md]}>
        <Col xs={24} sm={12} lg={6}>
          <StatCard title={t('plugins.statsTotalPlugins')} value={stats.total} loading={loading} icon={<AppstoreOutlined />} />
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <StatCard title={t('plugins.statsMcpPlugins')} value={stats.mcp} loading={loading} icon={<ApiOutlined />} />
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <StatCard title={t('plugins.statsOpenApiPlugins')} value={stats.openApi} loading={loading} icon={<FunctionOutlined />} />
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <StatCard title={t('plugins.statsTotalCalls')} value={stats.calls} loading={loading} icon={<ThunderboltOutlined />} />
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
            extra={<Text type="secondary">{t('plugins.statsFunctionsPrefix', { total: stats.functions })}</Text>}
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
            {stats.top.length === 0 ? (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('plugins.empty')} />
            ) : (
              stats.top.map((p) => {
                const calls1 = toNum(p.counter)
                return (
                  <div key={p.id || p.pluginName} style={{ display: 'flex', alignItems: 'center', gap: spacing.sm, marginBottom: spacing.sm }}>
                    <PluginAvatar objectKey={p.avatarPath} title={p.title ?? p.pluginName} size={26} />
                    <Text ellipsis={{ tooltip: true }} style={{ width: 130, flexShrink: 0 }}>
                      {p.title || p.pluginName}
                    </Text>
                    {barTrack((calls1 / maxCalls) * 100, token.colorPrimary)}
                    <Text type="secondary" style={{ width: 56, textAlign: 'right', flexShrink: 0, fontVariantNumeric: 'tabular-nums' }}>
                      {calls1}
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
            extra={<Text type="secondary">{t('plugins.statsPluginsUnit', { total: stats.total })}</Text>}
          >
            {classifyRows.length === 0 ? (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('plugins.empty')} />
            ) : (
              classifyRows.map((r) => (
                <div key={r.key} style={{ display: 'flex', alignItems: 'center', gap: spacing.sm, marginBottom: spacing.sm }}>
                  <Text style={{ width: 88, flexShrink: 0 }} ellipsis={{ tooltip: true }}>{r.label}</Text>
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
