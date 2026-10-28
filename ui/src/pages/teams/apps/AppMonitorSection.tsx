import { useCallback, useEffect, useMemo, useState } from 'react'
import { Alert, Col, Progress, Row, Statistic } from 'antd'
import type { TableColumnsType } from 'antd'
import { Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { Card, DataTable } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { getAppUsage, type AppUsageActivityStats, type AppUsageModelItem, type AppUsageSummary } from '@/api/app'
import { getAppMemoryStats } from '@/api/memory'
import { formatDateTime } from '@/utils/datetime'

export interface AppMonitorSectionProps {
  appId: string
}

/**
 * 应用用量监控分区：汇总卡片 + 按模型分布表。
 * 数据来自聚合用量表，最多滞后约 1 分钟；仅团队 Admin+ 访问（入口由 AppWorkspace 控制）。
 */
const { Text } = Typography

export function AppMonitorSection({ appId }: AppMonitorSectionProps) {
  const { t, i18n } = useTranslation()
  const formatInt = useCallback(
    (value?: number | null) => new Intl.NumberFormat(i18n.language).format(Number(value ?? 0)),
    [i18n.language],
  )
  const [loading, setLoading] = useState(true)
  const [summary, setSummary] = useState<AppUsageSummary>({
    callCount: 0,
    promptTokens: 0,
    completionTokens: 0,
    totalTokens: 0,
    cachedTokens: 0,
  })
  const [byModel, setByModel] = useState<AppUsageModelItem[]>([])
  const [stats, setStats] = useState<AppUsageActivityStats>({
    useCount: 0,
    sessionCount: 0,
    userCount: 0,
    messageCount: 0,
    lastActiveTime: null,
  })
  const [memoryCount, setMemoryCount] = useState(0)

  useEffect(() => {
    if (!appId) return
    let cancelled = false
    setLoading(true)
    getAppUsage(appId)
      .then((res) => {
        if (cancelled) return
        setSummary(res.summary)
        setByModel(res.byModel ?? [])
        setStats(res.stats)
      })
      .catch(() => {
        if (cancelled) return
        setSummary({ callCount: 0, promptTokens: 0, completionTokens: 0, totalTokens: 0, cachedTokens: 0 })
        setByModel([])
        setStats({ useCount: 0, sessionCount: 0, userCount: 0, messageCount: 0, lastActiveTime: null })
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
    // 记忆条数来自记忆统计接口（应用内全部用户），失败不影响用量展示
    getAppMemoryStats(appId)
      .then((res) => {
        if (!cancelled) setMemoryCount(res.totalMemories)
      })
      .catch(() => undefined)
    return () => {
      cancelled = true
    }
  }, [appId])

  const totalTokens = summary.totalTokens ?? 0

  const columns: TableColumnsType<AppUsageModelItem> = useMemo(
    () => [
      {
        title: t('appMonitor.columnModel'),
        key: 'model',
        ellipsis: true,
        render: (_, record) => record.modelName || record.modelId || '-',
      },
      {
        title: t('appMonitor.callCount'),
        dataIndex: 'callCount',
        width: 120,
        render: (v: number | null) => formatInt(v),
      },
      {
        title: t('appMonitor.promptTokens'),
        dataIndex: 'promptTokens',
        width: 140,
        render: (v: number | null) => formatInt(v),
      },
      {
        title: t('appMonitor.completionTokens'),
        dataIndex: 'completionTokens',
        width: 140,
        render: (v: number | null) => formatInt(v),
      },
      {
        title: t('appMonitor.cachedTokens'),
        dataIndex: 'cachedTokens',
        width: 140,
        render: (v: number | null) => formatInt(v),
      },
      {
        title: t('appMonitor.totalTokens'),
        dataIndex: 'totalTokens',
        width: 140,
        render: (v: number | null) => formatInt(v),
      },
      {
        title: t('appMonitor.columnShare'),
        key: 'share',
        width: 180,
        render: (_, record) => {
          const percent = totalTokens > 0 ? Math.round(((record.totalTokens ?? 0) / totalTokens) * 100) : 0
          return <Progress percent={percent} size="small" />
        },
      },
    ],
    [t, totalTokens, formatInt],
  )

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: spacing.md }}>
      <Alert type="info" showIcon message={t('appMonitor.lagHint')} />

      <Row gutter={[spacing.md, spacing.md]}>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic title={t('appMonitor.callCount')} value={formatInt(summary.callCount)} />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic title={t('appMonitor.promptTokens')} value={formatInt(summary.promptTokens)} />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic title={t('appMonitor.completionTokens')} value={formatInt(summary.completionTokens)} />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic title={t('appMonitor.totalTokens')} value={formatInt(summary.totalTokens)} />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title={t('appMonitor.cachedTokens')}
              value={formatInt(summary.cachedTokens)}
              suffix={
                summary.promptTokens > 0 && summary.cachedTokens > 0
                  ? `(${Math.round((summary.cachedTokens / summary.promptTokens) * 100)}%)`
                  : undefined
              }
            />
          </Card>
        </Col>
      </Row>

      <Row gutter={[spacing.md, spacing.md]}>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic title={t('appMonitor.useCount')} value={formatInt(stats.useCount)} />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic title={t('appMonitor.userCount')} value={formatInt(stats.userCount)} />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic title={t('appMonitor.sessionCount')} value={formatInt(stats.sessionCount)} />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic title={t('appMonitor.messageCount')} value={formatInt(stats.messageCount)} />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic title={t('appMonitor.memoryCount')} value={formatInt(memoryCount)} />
          </Card>
        </Col>
      </Row>
      {stats.lastActiveTime && (
        <Text type="secondary">
          {t('appMonitor.lastActive', { time: formatDateTime(stats.lastActiveTime) })}
        </Text>
      )}

      <Card title={t('appMonitor.byModelTitle')}>
        <DataTable<AppUsageModelItem>
          sticky
          rowKey="modelId"
          columns={columns}
          dataSource={byModel}
          loading={loading}
          pagination={false}
          locale={{ emptyText: t('appMonitor.empty') }}
          scroll={{ x: 1000 }}
        />
      </Card>
    </div>
  )
}
