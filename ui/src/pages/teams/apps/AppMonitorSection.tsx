import { useCallback, useEffect, useMemo, useState } from 'react'
import { Alert, Col, Progress, Row, Statistic } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { Card, DataTable } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { getAppUsage, type AppUsageModelItem, type AppUsageSummary } from '@/api/app'

export interface AppMonitorSectionProps {
  appId: string
}

/**
 * 应用用量监控分区：汇总卡片 + 按模型分布表。
 * 数据来自聚合用量表，最多滞后约 1 分钟；仅团队 Admin+ 访问（入口由 AppWorkspace 控制）。
 */
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
  })
  const [byModel, setByModel] = useState<AppUsageModelItem[]>([])

  useEffect(() => {
    if (!appId) return
    let cancelled = false
    setLoading(true)
    getAppUsage(appId)
      .then((res) => {
        if (cancelled) return
        setSummary(res.summary)
        setByModel(res.byModel ?? [])
      })
      .catch(() => {
        if (cancelled) return
        setSummary({ callCount: 0, promptTokens: 0, completionTokens: 0, totalTokens: 0 })
        setByModel([])
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
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
      </Row>

      <Card title={t('appMonitor.byModelTitle')}>
        <DataTable<AppUsageModelItem>
          sticky
          rowKey="modelId"
          columns={columns}
          dataSource={byModel}
          loading={loading}
          pagination={false}
          locale={{ emptyText: t('appMonitor.empty') }}
          scroll={{ x: 860 }}
        />
      </Card>
    </div>
  )
}
