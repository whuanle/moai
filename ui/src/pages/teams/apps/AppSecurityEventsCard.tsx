import { useCallback, useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { DataTable } from '@/design-system'
import { getAppSecurityEvents, type AppSecurityEventItem } from '@/api/app'

export interface AppSecurityEventsCardProps {
  appId: string
  canManage: boolean
}

const EVENT_PAGE_SIZE = 10

function formatTime(value: string): string {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    return value
  }

  const pad = (n: number) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`
}

/**
 * 安全事件列表：内容脱敏规则命中审计（按时间倒序，仅团队管理员可见）。
 * 作为「日志」分区的「安全事件」Tab 内容（原独立卡片已并入 Tab，标题由 Tab 标签承载）。
 */
export function AppSecurityEventsCard({ appId, canManage }: AppSecurityEventsCardProps) {
  const { t } = useTranslation()
  const [loading, setLoading] = useState(false)
  const [items, setItems] = useState<AppSecurityEventItem[]>([])
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)

  const load = useCallback(
    async (pageNo: number) => {
      setLoading(true)
      try {
        const res = await getAppSecurityEvents(appId, pageNo, EVENT_PAGE_SIZE)
        setItems(res.items)
        setTotal(res.total)
        setPage(res.pageNo)
      } catch {
        // 错误已由全局请求中间件统一提示
      } finally {
        setLoading(false)
      }
    },
    [appId],
  )

  useEffect(() => {
    if (canManage) {
      void load(1)
    }
  }, [canManage, load])

  if (!canManage) {
    return null
  }

  const columns = [
    { title: t('appSecurity.events.colTime'), dataIndex: 'createTime', render: (v: string) => formatTime(v) },
    { title: t('appSecurity.events.colScope'), dataIndex: 'scope', render: (v: string) => t(`appSecurity.scope.${v}`) },
    {
      title: t('appSecurity.events.colRule'),
      render: (_: unknown, record: AppSecurityEventItem) =>
        record.ruleName || t(`appSecurity.type.${record.ruleType}`),
    },
    {
      title: t('appSecurity.events.colAction'),
      dataIndex: 'action',
      render: (v: string) => t(`appSecurity.action.${v}`),
    },
    { title: t('appSecurity.events.colCount'), dataIndex: 'hitCount', width: 80 },
  ]

  return (
    <DataTable<AppSecurityEventItem>
      columns={columns}
      dataSource={items}
      loading={loading}
      rowKey="id"
      onRefresh={() => void load(page)}
      refreshLoading={loading}
      pagination={{
        current: page,
        pageSize: EVENT_PAGE_SIZE,
        total,
        onChange: (p) => void load(p),
      }}
    />
  )
}
