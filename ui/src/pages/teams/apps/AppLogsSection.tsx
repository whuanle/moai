import { useCallback, useEffect, useMemo, useState } from 'react'
import { Button, DatePicker, Drawer, Form, Input, Select, Space, Spin, Tag, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import type { Dayjs } from 'dayjs'
import { useTranslation } from 'react-i18next'
import { DataTable, QueryBar } from '@/design-system'
import { neutralColors, radius, spacing } from '@/design-system/theme'
import { formatDateTime } from '@/utils/datetime'
import {
  getAppLogs,
  getAppLogMessages,
  type AppLogItem,
  type AspAppSessionMessageItem,
} from '@/api/app'

const { Text } = Typography
const { RangePicker } = DatePicker

type RangeValue = [Dayjs | null, Dayjs | null] | null

/** 可筛选的用户类型；后端 UserType 枚举按名不区分大小写绑定 */
const USER_TYPE_OPTIONS = ['normal', 'external', 'externalApp'] as const

/** 用户类型 → i18n key（未知类型回退 none） */
function userTypeLabelKey(userType?: string | null): string {
  switch (userType) {
    case 'normal':
      return 'appLogs.userTypeNormal'
    case 'external':
      return 'appLogs.userTypeExternal'
    case 'externalApp':
      return 'appLogs.userTypeExternalApp'
    default:
      return 'appLogs.userTypeNone'
  }
}

/** 角色 → i18n key（未知角色回退 system） */
function roleLabelKey(role?: string | null): string {
  switch (role) {
    case 'user':
      return 'appLogs.roleUser'
    case 'assistant':
      return 'appLogs.roleAssistant'
    case 'tool':
      return 'appLogs.roleTool'
    case 'system':
      return 'appLogs.roleSystem'
    default:
      return 'appLogs.roleSystem'
  }
}

const ROLE_COLOR: Record<string, string> = {
  user: 'blue',
  assistant: 'green',
  tool: 'orange',
  system: 'default',
}

/** 解析 assistant 的 toolCalls 文本为工具名数组；非法/空返回空数组 */
function parseToolNames(toolCalls?: string | null): string[] {
  if (!toolCalls) return []
  try {
    const parsed: unknown = JSON.parse(toolCalls)
    if (!Array.isArray(parsed)) return []
    return parsed
      .map((item) => {
        if (typeof item === 'string') return item
        const fn = (item as { function?: { name?: unknown } })?.function?.name
        const name = fn ?? (item as { name?: unknown })?.name
        return typeof name === 'string' ? name : ''
      })
      .filter((name) => name.length > 0)
  } catch {
    return []
  }
}

export interface AppLogsSectionProps {
  appId: string
}

/**
 * 应用对话日志分区：服务端分页列表 + 会话消息详情抽屉。
 * 仅团队 Admin+ 访问（入口由 AppWorkspace 控制）。
 */
export function AppLogsSection({ appId }: AppLogsSectionProps) {
  const { t } = useTranslation()
  const [loading, setLoading] = useState(true)
  const [items, setItems] = useState<AppLogItem[]>([])
  const [total, setTotal] = useState(0)
  const [pageNo, setPageNo] = useState(1)
  const [pageSize, setPageSize] = useState(20)
  const [keyword, setKeyword] = useState<string | undefined>(undefined)
  const [userType, setUserType] = useState<string | undefined>(undefined)
  const [range, setRange] = useState<RangeValue>(null)

  const [detailOpen, setDetailOpen] = useState(false)
  const [detailSession, setDetailSession] = useState<AppLogItem | null>(null)
  const [detailMessages, setDetailMessages] = useState<AspAppSessionMessageItem[]>([])
  const [detailLoading, setDetailLoading] = useState(false)

  const load = useCallback(
    async (page: number, size: number, kw?: string, ut?: string, rg?: RangeValue) => {
      if (!appId) return
      setLoading(true)
      try {
        const res = await getAppLogs(appId, {
          pageNo: page,
          pageSize: size,
          keyword: kw,
          userType: ut,
          from: rg?.[0] ? rg[0].toISOString() : undefined,
          to: rg?.[1] ? rg[1].toISOString() : undefined,
        })
        setItems(res.items)
        setTotal(res.total)
      } catch {
        // 错误已由全局请求中间件统一提示
      } finally {
        setLoading(false)
      }
    },
    [appId],
  )

  useEffect(() => {
    void load(1, 20)
  }, [load])

  const openDetail = (record: AppLogItem) => {
    setDetailSession(record)
    setDetailMessages([])
    setDetailOpen(true)
  }

  useEffect(() => {
    if (!detailOpen || !detailSession?.sessionId) return
    const sessionId = detailSession.sessionId
    let cancelled = false
    setDetailLoading(true)
    getAppLogMessages(appId, sessionId)
      .then((messages) => {
        if (!cancelled) setDetailMessages(messages)
      })
      .catch(() => {
        if (!cancelled) setDetailMessages([])
      })
      .finally(() => {
        if (!cancelled) setDetailLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [detailOpen, detailSession?.sessionId, appId])

  const handleSearch = (values: Record<string, unknown>) => {
    const kw = typeof values.keyword === 'string' ? values.keyword.trim() || undefined : undefined
    const ut = typeof values.userType === 'string' ? values.userType || undefined : undefined
    const rg = Array.isArray(values.range) ? (values.range as RangeValue) : null
    setKeyword(kw)
    setUserType(ut)
    setRange(rg)
    setPageNo(1)
    void load(1, pageSize, kw, ut, rg)
  }

  const handleReset = () => {
    setKeyword(undefined)
    setUserType(undefined)
    setRange(null)
    setPageNo(1)
    void load(1, pageSize, undefined, undefined, null)
  }

  const columns: TableColumnsType<AppLogItem> = useMemo(
    () => [
      {
        title: t('appLogs.columnTitle'),
        dataIndex: 'title',
        ellipsis: true,
        render: (v: string | null) => v || '-',
      },
      {
        title: t('appLogs.columnUser'),
        key: 'user',
        width: 180,
        render: (_, record) => {
          // 按用户类型精确归属：normal 显示用户名，external/externalApp 带类型前缀；
          // none（识别不到）只显示 id，不误标为外部用户
          if (record.userType === 'normal') {
            return record.createUserName || `#${record.ownerId ?? '-'}`
          }
          if (record.userType === 'external' || record.userType === 'externalApp') {
            return `${t(userTypeLabelKey(record.userType))} #${record.ownerId ?? '-'}`
          }
          return `#${record.ownerId ?? '-'}`
        },
      },
      {
        title: t('appLogs.columnUserType'),
        key: 'userType',
        width: 110,
        render: (_, record) => <Tag>{t(userTypeLabelKey(record.userType))}</Tag>,
      },
      {
        title: t('appLogs.columnTotalTokens'),
        dataIndex: 'totalTokens',
        width: 120,
        render: (v: number | null) => v ?? 0,
      },
      {
        title: t('appLogs.columnLastMessageTime'),
        dataIndex: 'lastMessageTime',
        width: 160,
        render: (v: string | null) => <span style={{ whiteSpace: 'nowrap' }}>{formatDateTime(v)}</span>,
      },
      {
        title: t('appLogs.columnCreateTime'),
        dataIndex: 'createTime',
        width: 160,
        render: (v: string | null) => <span style={{ whiteSpace: 'nowrap' }}>{formatDateTime(v)}</span>,
      },
      {
        title: t('appLogs.columnActions'),
        key: 'actions',
        width: 90,
        fixed: 'right',
        render: (_, record) => (
          <Button type="link" onClick={() => openDetail(record)}>
            {t('appLogs.view')}
          </Button>
        ),
      },
    ],
    [t],
  )

  const orderedMessages = useMemo(
    () => [...detailMessages].sort((a, b) => (a.seq ?? 0) - (b.seq ?? 0)),
    [detailMessages],
  )

  return (
    <div>
      <QueryBar onSearch={handleSearch} onReset={handleReset} loading={loading}>
        <Form.Item name="keyword">
          <Input
            placeholder={t('appLogs.keywordPlaceholder')}
            allowClear
            maxLength={50}
            style={{ width: 240 }}
          />
        </Form.Item>
        <Form.Item name="userType">
          <Select
            allowClear
            placeholder={t('appLogs.userType')}
            style={{ width: 160 }}
            options={USER_TYPE_OPTIONS.map((value) => ({ value, label: t(userTypeLabelKey(value)) }))}
          />
        </Form.Item>
        <Form.Item name="range">
          <RangePicker showTime />
        </Form.Item>
      </QueryBar>

      <DataTable<AppLogItem>
        rowKey="sessionId"
        columns={columns}
        dataSource={items}
        loading={loading}
        sticky
        scroll={{ x: 980 }}
        pagination={{
          current: pageNo,
          pageSize,
          total,
          showSizeChanger: true,
          onChange: (page, size) => {
            setPageNo(page)
            setPageSize(size)
            void load(page, size, keyword, userType, range)
          },
        }}
      />

      <Drawer
        open={detailOpen}
        onClose={() => setDetailOpen(false)}
        width={760}
        maskClosable={false}
        destroyOnClose
        title={detailSession?.title || t('appLogs.detailTitle')}
      >
        {detailLoading ? (
          <div style={{ padding: spacing.lg, textAlign: 'center' }}>
            <Spin />
          </div>
        ) : orderedMessages.length === 0 ? (
          <Text type="secondary">{t('appLogs.detailEmpty')}</Text>
        ) : (
          <Space direction="vertical" size={spacing.sm} style={{ width: '100%' }}>
            {orderedMessages.map((message, index) => {
              const role = message.role ?? 'system'
              const toolNames = parseToolNames(message.toolCalls)
              return (
                <div
                  key={message.messageId ?? `${message.seq ?? index}`}
                  style={{
                    border: `1px solid ${neutralColors.border}`,
                    borderRadius: radius.default,
                    padding: spacing.sm,
                  }}
                >
                  <Space size={spacing.xs} wrap style={{ marginBottom: spacing.xs }}>
                    <Tag color={ROLE_COLOR[role] ?? 'default'}>{t(roleLabelKey(role))}</Tag>
                    {toolNames.map((name, i) => (
                      <Tag key={`${name}-${i}`} color="geekblue">
                        {name}
                      </Tag>
                    ))}
                  </Space>
                  <div style={{ whiteSpace: 'pre-wrap', wordBreak: 'break-word' }}>
                    {message.content || ''}
                  </div>
                </div>
              )
            })}
          </Space>
        )}
      </Drawer>
    </div>
  )
}
