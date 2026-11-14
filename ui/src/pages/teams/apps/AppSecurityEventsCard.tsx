import { useCallback, useEffect, useState } from 'react'
import { Form, Input, Select, Tag } from 'antd'
import { useTranslation } from 'react-i18next'
import { DataTable, QueryBar } from '@/design-system'
import { formatDateTime } from '@/utils/datetime'
import { getAppSecurityEvents, type AppSecurityEventItem } from '@/api/app'

export interface AppSecurityEventsCardProps {
  appId: string
  canManage: boolean
}

const EVENT_PAGE_SIZE = 50

/** 可筛选的事件作用域（对齐后端 Scope 取值） */
const EVENT_SCOPE_OPTIONS = ['input', 'toolResult', 'toolArgs', 'modelOutput'] as const

/** 动作 → 语义色（blocked 危险 / masked 处理 / flagged 观测） */
const ACTION_TAG_COLOR: Record<string, string> = {
  blocked: 'red',
  masked: 'gold',
  flagged: 'blue',
}

/**
 * 安全事件列表：内容脱敏规则命中审计（按时间倒序，仅团队管理员可见）。
 * 作为「日志」分区的「安全事件」Tab 内容（标题由 Tab 标签承载）；
 * 工具栏 = 规则关键字搜索 + 作用域筛选 + 查询按钮（查询即刷新，不另设刷新按钮，见 frontend-conventions 第 7 条）。
 */
export function AppSecurityEventsCard({ appId, canManage }: AppSecurityEventsCardProps) {
  const { t } = useTranslation()
  const [loading, setLoading] = useState(false)
  const [items, setItems] = useState<AppSecurityEventItem[]>([])
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(EVENT_PAGE_SIZE)
  const [keyword, setKeyword] = useState<string | undefined>(undefined)
  const [scope, setScope] = useState<string | undefined>(undefined)

  const load = useCallback(
    async (pageNo: number, size: number, kw?: string, sc?: string) => {
      setLoading(true)
      try {
        const res = await getAppSecurityEvents(appId, pageNo, size, { keyword: kw, scope: sc })
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
      void load(1, EVENT_PAGE_SIZE)
    }
  }, [canManage, load])

  const handleSearch = (values: Record<string, unknown>) => {
    const kw = typeof values.keyword === 'string' ? values.keyword.trim() || undefined : undefined
    const sc = typeof values.scope === 'string' ? values.scope || undefined : undefined
    setKeyword(kw)
    setScope(sc)
    void load(1, pageSize, kw, sc)
  }

  if (!canManage) {
    return null
  }

  const columns = [
    { title: t('appSecurity.events.colTime'), dataIndex: 'createTime', width: 170, render: (v: string) => <span style={{ whiteSpace: 'nowrap' }}>{formatDateTime(v)}</span> },
    { title: t('appSecurity.events.colScope'), dataIndex: 'scope', width: 110, render: (v: string) => t(`appSecurity.scope.${v}`) },
    {
      title: t('appSecurity.events.colRule'),
      render: (_: unknown, record: AppSecurityEventItem) =>
        record.ruleName || t(`appSecurity.type.${record.ruleType}`),
    },
    {
      title: t('appSecurity.events.colAction'),
      dataIndex: 'action',
      width: 110,
      render: (v: string) => <Tag color={ACTION_TAG_COLOR[v] ?? 'default'}>{t(`appSecurity.action.${v}`)}</Tag>,
    },
    { title: t('appSecurity.events.colCount'), dataIndex: 'hitCount', width: 80 },
  ]

  return (
    <>
      <QueryBar onSearch={handleSearch} loading={loading}>
        <Form.Item name="keyword">
          <Input
            placeholder={t('appSecurity.events.keywordPlaceholder')}
            allowClear
            maxLength={50}
            style={{ width: 220 }}
          />
        </Form.Item>
        <Form.Item name="scope">
          <Select
            allowClear
            placeholder={t('appSecurity.events.scopePlaceholder')}
            style={{ width: 150 }}
            options={EVENT_SCOPE_OPTIONS.map((value) => ({ value, label: t(`appSecurity.scope.${value}`) }))}
          />
        </Form.Item>
      </QueryBar>

      <DataTable<AppSecurityEventItem>
        columns={columns}
        dataSource={items}
        loading={loading}
        rowKey="id"
        pagination={{
          current: page,
          pageSize,
          total,
          // pageSize 为受控值：切换每页数量时必须更新状态，否则选择无效（antd onChange 携带新 pageSize）
          onChange: (p, size) => {
            const next = size !== pageSize ? 1 : p
            setPageSize(size)
            setPage(next)
            void load(next, size, keyword, scope)
          },
        }}
      />
    </>
  )
}
