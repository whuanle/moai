import { useCallback, useEffect, useMemo, useState } from 'react'
import { DownloadOutlined, EyeOutlined } from '@ant-design/icons'
import { Button, DatePicker, Drawer, Form, Input, Select, Space, Spin, Tabs, Tag, Typography, theme } from 'antd'
import type { TableColumnsType } from 'antd'
import type { Dayjs } from 'dayjs'
import { useTranslation } from 'react-i18next'
import { DataTable, QueryBar, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { formatDateTime } from '@/utils/datetime'
import {
  getAppLogs,
  getAppLogMessages,
  type AppLogItem,
  type AspAppSessionMessageItem,
} from '@/api/app'
import { AppMonitorSection } from './AppMonitorSection'
import { AppSecurityEventsCard } from './AppSecurityEventsCard'
import { ChatMessageList, type DisplayMessage } from './chat/ChatMessageList'
import { parseHistoryToolCalls } from './chat/historyToolCalls'
import { chatCssVars } from './chat/chatCssVars'
import './app-chat.css'

const { Text } = Typography
const { RangePicker } = DatePicker

type RangeValue = [Dayjs | null, Dayjs | null] | null

/** 日志分区 Tab：日志（默认）/ 安全事件 / 使用分析（原「监控」分区并入） */
export type AppLogsTab = 'logs' | 'events' | 'usage'

/** 可筛选的用户类型；后端 UserType 枚举按名不区分大小写绑定 */
const USER_TYPE_OPTIONS = ['normal', 'external', 'externalApp', 'timer'] as const

/** 用户类型 → i18n key（未知类型回退 none） */
function userTypeLabelKey(userType?: string | null): string {
  switch (userType) {
    case 'normal':
      return 'appLogs.userTypeNormal'
    case 'external':
      return 'appLogs.userTypeExternal'
    case 'externalApp':
      return 'appLogs.userTypeExternalApp'
    case 'timer':
      return 'appLogs.userTypeTimer'
    default:
      return 'appLogs.userTypeNone'
  }
}

export interface AppLogsSectionProps {
  appId: string
  canManage: boolean
  /** 默认激活 Tab（/monitor 深链落到「使用分析」），缺省 logs */
  defaultTab?: AppLogsTab
}

/**
 * 应用日志分区（Tab 容器，仅团队 Admin+ 访问，入口由 AppWorkspace 控制）：
 * - 日志（默认）：会话日志服务端分页列表 + 会话消息详情抽屉（复用对话页 ChatMessageList 会话式排版）；
 * - 安全事件：内容脱敏规则命中审计（原「安全」分区卡片，2026-10-27 并入）；
 * - 使用分析：用量汇总与按模型分布（原「监控」分区并入，「监控」菜单已移除，/monitor 深链落到本 Tab）。
 */
export function AppLogsSection({ appId, canManage, defaultTab }: AppLogsSectionProps) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const cssVars = useMemo(() => chatCssVars(token), [token])
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

  /**
   * 导出会话 JSON：拉取消息全量（含 tool/reasoning 原始字段）后生成文件下载，
   * 便于管理员离线细看完整对话现场（比抽屉里的会话式视图多出 system/tool 行与思维链）。
   */
  const downloadJson = useCallback(
    async (record: AppLogItem) => {
      if (!record.sessionId) return
      try {
        const messages = await getAppLogMessages(appId, record.sessionId)
        const payload = {
          exportedAt: new Date().toISOString(),
          appId,
          session: { ...record },
          messages,
        }
        // 文件名直接用会话 id（Guid，天然文件名安全），便于与日志列表/库内记录对号
        const blob = new Blob([JSON.stringify(payload, null, 2)], { type: 'application/json;charset=utf-8' })
        const url = URL.createObjectURL(blob)
        const anchor = document.createElement('a')
        anchor.href = url
        anchor.download = `${record.sessionId}.json`
        anchor.click()
        URL.revokeObjectURL(url)
        feedback.success(t('appLogs.downloadSuccess'))
      } catch {
        // 错误已由全局请求中间件统一提示
      }
    },
    [appId, t],
  )

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
        width: 200,
        render: (_, record) => {
          // 按用户类型精确归属：normal 双行显示昵称+登录用户名（createUserName 经填充服务为昵称），
          // 两者相同或缺失时降级单行；external/externalApp 带类型前缀；
          // none（识别不到）只显示 id，不误标为外部用户
          if (record.userType === 'normal') {
            const nick = record.createUserName || ''
            const name = record.ownerUserName || ''
            const fallback = `#${record.ownerId ?? '-'}`
            if (nick && name && nick !== name) {
              return (
                <div>
                  <div>{nick}</div>
                  <Text type="secondary" style={{ fontSize: 12 }}>
                    {name}
                  </Text>
                </div>
              )
            }
            return nick || name || fallback
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
        width: 150,
        fixed: 'right',
        render: (_, record) => (
          <Space size={0}>
            <Button type="link" size="small" icon={<EyeOutlined />} onClick={() => openDetail(record)}>
              {t('appLogs.view')}
            </Button>
            <Button type="link" size="small" icon={<DownloadOutlined />} onClick={() => void downloadJson(record)}>
              {t('appLogs.download')}
            </Button>
          </Space>
        ),
      },
    ],
    [t, downloadJson],
  )

  // 与正式对话页同口径还原会话现场：仅保留 user 与 assistant（有正文或工具调用）消息，
  // 工具调用随 assistant 消息以折叠卡展示；tool/system 中间行不进会话视图
  const displayMessages = useMemo<DisplayMessage[]>(
    () =>
      [...detailMessages]
        .sort((a, b) => (a.seq ?? 0) - (b.seq ?? 0))
        .filter(
          (m) =>
            m.role === 'user' ||
            (m.role === 'assistant' && ((m.content ?? '').trim().length > 0 || parseHistoryToolCalls(m.toolCalls))),
        )
        .map((m) => ({
          id: String(m.messageId ?? crypto.randomUUID()),
          role: m.role === 'user' ? 'user' : 'assistant',
          content: m.content ?? '',
          toolCalls: parseHistoryToolCalls(m.toolCalls),
        })),
    [detailMessages],
  )

  // 会话归属用户显示名：normal 用用户名，其余按 id 兜底（仅作用户侧头像字母）
  const detailUserLabel = detailSession
    ? detailSession.userType === 'normal'
      ? detailSession.createUserName || `#${detailSession.ownerId ?? '-'}`
      : `#${detailSession.ownerId ?? '-'}`
    : 'U'

  const copyMessage = useCallback(
    async (text: string) => {
      try {
        await navigator.clipboard.writeText(text)
        feedback.success(t('appChat.copied'))
      } catch {
        // 忽略剪贴板不可用
      }
    },
    [t],
  )

  const logList = (
    <Space direction="vertical" size={spacing.md} style={{ width: '100%' }}>
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
    </Space>
  )

  // 非管理员仅保留日志 Tab（安全事件与使用分析为 Admin+ 内容，入口本就不可达，防御性收窄）
  const tabItems = [
    { key: 'logs', label: t('appLogs.tabLogs'), children: logList },
    ...(canManage
      ? [
          { key: 'events', label: t('appLogs.tabEvents'), children: <AppSecurityEventsCard appId={appId} canManage={canManage} /> },
          { key: 'usage', label: t('appLogs.tabUsage'), children: <AppMonitorSection appId={appId} /> },
        ]
      : []),
  ]

  return (
    <div>
      <Tabs defaultActiveKey={defaultTab ?? 'logs'} items={tabItems} />

      <Drawer
        open={detailOpen}
        onClose={() => setDetailOpen(false)}
        width={760}
        maskClosable={false}
        destroyOnClose
        title={detailSession?.title || t('appLogs.detailTitle')}
        extra={
          detailSession && (
            <Button
              size="small"
              icon={<DownloadOutlined />}
              onClick={() => void downloadJson(detailSession)}
            >
              {t('appLogs.downloadJson')}
            </Button>
          )
        }
      >
        {detailLoading ? (
          <div style={{ padding: spacing.lg, textAlign: 'center' }}>
            <Spin />
          </div>
        ) : (
          // 注入 --mc-* 主题变量后复用 app-chat.css 会话排版：用户消息靠右、助手消息靠左带头像
          <div style={cssVars}>
            <ChatMessageList
              messages={displayMessages}
              sending={false}
              userName={detailUserLabel}
              onCopy={copyMessage}
              emptyState={<Text type="secondary">{t('appLogs.detailEmpty')}</Text>}
            />
          </div>
        )}
      </Drawer>
    </div>
  )
}
