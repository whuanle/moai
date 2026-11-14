import { useCallback, useEffect, useState } from 'react'
import { Button, Card, Checkbox, Empty, Pagination, Popconfirm, Segmented, Space, theme, Typography } from 'antd'
import { CheckOutlined, DeleteOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { Page, feedback } from '@/design-system'
import { controlHeight, spacing } from '@/design-system/theme'
import { formatDateTime } from '@/utils/datetime'
import { useNotificationStore } from '@/store/notification'
import { onEventPushEvent, EVENT_MESSAGE_NEW } from '@/realtime/eventPush'
import {
  deleteMessages,
  getMessages,
  markMessagesRead,
  type MessageItem,
} from '@/api/message'

const { Text } = Typography

type ReadFilter = 'all' | 'unread'

// AppLayout Content 上下 padding 24*2 + Page 头部行（Segmented 控件高 36 + 头部下间距 24）
// 消息页自身不产生页面滚动条：列表区内部滚动，分页固定在视口底部
const pageVerticalPadding = spacing.lg * 2
const headerRowHeight = controlHeight + spacing.lg
const panelHeight = `calc(100vh - ${pageVerticalPadding + headerRowHeight}px)`

/** 站内信列表：未读高亮、点击标记已读、批量已读/批量删除、全部已读、删除（软删）；message.new 事件到达时在线刷新 */
export function Messages() {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const refreshUnread = useNotificationStore((state) => state.refresh)

  const [loading, setLoading] = useState(false)
  const [items, setItems] = useState<MessageItem[]>([])
  const [total, setTotal] = useState(0)
  const [pageNo, setPageNo] = useState(1)
  const [pageSize, setPageSize] = useState(20)
  const [filter, setFilter] = useState<ReadFilter>('all')
  const [selectedIds, setSelectedIds] = useState<string[]>([])

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const res = await getMessages({ pageNo, pageSize, isRead: filter === 'unread' ? false : undefined })
      setItems(res.items)
      setTotal(res.total)
    } finally {
      setLoading(false)
    }
  }, [pageNo, pageSize, filter])

  useEffect(() => {
    void load()
  }, [load])

  // 在线刷新：新消息事件到达时若停留在本页，静默重载列表并同步角标
  useEffect(() => {
    return onEventPushEvent((payload) => {
      if (payload.event !== EVENT_MESSAGE_NEW) return
      void load()
      void refreshUnread()
    })
  }, [load, refreshUnread])

  const afterChange = useCallback(() => {
    void load()
    void refreshUnread()
  }, [load, refreshUnread])

  // 查询条件（页码/页大小/过滤）变化时清空选择，避免跨页残留
  useEffect(() => {
    setSelectedIds([])
  }, [pageNo, pageSize, filter])

  const toggleSelected = useCallback((id: string) => {
    setSelectedIds((prev) => (prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]))
  }, [])

  const currentPageIds = items.map((item) => item.id ?? '').filter(Boolean)
  const allChecked = currentPageIds.length > 0 && currentPageIds.every((id) => selectedIds.includes(id))
  const someChecked = currentPageIds.some((id) => selectedIds.includes(id))
  // 批量删除只作用于已读项：选中里的未读自动忽略，防止误删未读过的通知
  const selectedReadIds = items.filter((item) => item.isRead && selectedIds.includes(item.id ?? '')).map((item) => item.id ?? '')
  const selectedUnreadCount = items.filter((item) => !item.isRead && selectedIds.includes(item.id ?? '')).length
  const selectedUnreadExists = selectedUnreadCount > 0

  const handleToggleAll = useCallback(() => {
    setSelectedIds((prev) => (currentPageIds.length > 0 && currentPageIds.every((id) => prev.includes(id)) ? [] : currentPageIds))
  }, [currentPageIds])

  const handleMarkRead = useCallback(async (id: string) => {
    await markMessagesRead([id])
    feedback.success(t('messages.readSuccess'))
    afterChange()
  }, [afterChange, t])

  const handleBatchRead = useCallback(async () => {
    await markMessagesRead(selectedIds)
    feedback.success(t('messages.batchReadSuccess'))
    setSelectedIds([])
    afterChange()
  }, [afterChange, selectedIds, t])

  const handleDelete = useCallback(async (id: string) => {
    await deleteMessages([id])
    feedback.success(t('messages.deleteSuccess'))
    afterChange()
  }, [afterChange, t])

  const handleBatchDelete = useCallback(async () => {
    await deleteMessages(selectedReadIds)
    if (selectedUnreadCount > 0) {
      feedback.success(t('messages.batchDeleteSkippedSuccess', { deleted: selectedReadIds.length, unread: selectedUnreadCount }))
    } else {
      feedback.success(t('messages.deleteSuccess'))
    }
    setSelectedIds([])
    afterChange()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [afterChange, selectedReadIds.join(','), selectedUnreadCount, t])

  const renderRow = (item: MessageItem) => {
    const sender = item.createUserName || t('messages.senderSystem')
    const id = item.id ?? ''
    const checked = selectedIds.includes(id)
    return (
      <div
        key={item.id}
        role="button"
        tabIndex={0}
        onClick={() => {
          if (!item.isRead) void handleMarkRead(id)
        }}
        onKeyDown={(e) => {
          if ((e.key === 'Enter' || e.key === ' ') && !item.isRead) {
            e.preventDefault()
            void handleMarkRead(id)
          }
        }}
        style={{
          display: 'flex',
          gap: 12,
          padding: '12px 16px',
          borderBottom: `1px solid ${token.colorSplit}`,
          cursor: item.isRead ? 'default' : 'pointer',
          alignItems: 'flex-start',
          background: checked ? token.colorPrimaryBg : undefined,
          transition: 'background 0.2s',
        }}
      >
        <Checkbox
          checked={checked}
          onClick={(e) => e.stopPropagation()}
          onChange={() => toggleSelected(id)}
          aria-label={t('messages.selectMessage', { title: item.title })}
          style={{ marginTop: 2 }}
        />
        <span
          aria-hidden
          style={{
            flexShrink: 0,
            width: 8,
            height: 8,
            borderRadius: '50%',
            marginTop: 7,
            background: item.isRead ? token.colorFillSecondary : token.colorPrimary,
          }}
        />
        <div style={{ minWidth: 0, flex: 1 }}>
          <Text strong={!item.isRead} ellipsis style={{ display: 'block', fontSize: 14, color: item.isRead ? token.colorTextSecondary : token.colorText }}>
            {item.title}
          </Text>
          <Text type="secondary" ellipsis style={{ display: 'block', fontSize: 13, marginTop: 2 }}>
            {item.content}
          </Text>
          <div style={{ display: 'flex', gap: 12, marginTop: 4, alignItems: 'center' }}>
            <Text type="secondary" style={{ fontSize: 12 }}>{sender}</Text>
            <Text type="secondary" style={{ fontSize: 12 }}>{item.createTime ? formatDateTime(item.createTime) : '-'}</Text>
          </div>
        </div>
        <Space size={0} onClick={(e) => e.stopPropagation()}>
          {!item.isRead && (
            <Button type="link" size="small" icon={<CheckOutlined />} onClick={() => void handleMarkRead(id)}>
              {t('messages.markRead')}
            </Button>
          )}
          <Popconfirm
            title={t('messages.deleteConfirm')}
            onConfirm={() => void handleDelete(id)}
          >
            <Button type="link" size="small" danger icon={<DeleteOutlined />}>
              {t('messages.delete')}
            </Button>
          </Popconfirm>
        </Space>
      </div>
    )
  }

  return (
    <Page
      extra={
        <Space>
          <Segmented
            value={filter}
            onChange={(value) => {
              setFilter(value as ReadFilter)
              setPageNo(1)
            }}
            options={[
              { label: t('messages.tabAll'), value: 'all' },
              { label: t('messages.tabUnread'), value: 'unread' },
            ]}
          />
          <Button
            type="link"
            size="small"
            icon={<CheckOutlined />}
            disabled={selectedIds.length === 0 || !selectedUnreadExists}
            onClick={() => void handleBatchRead()}
          >
            {t('messages.batchRead')}
          </Button>
          <Popconfirm
            title={
              selectedUnreadCount > 0
                ? t('messages.batchDeleteConfirmSkip', { count: selectedReadIds.length, unread: selectedUnreadCount })
                : t('messages.batchDeleteConfirm', { count: selectedReadIds.length })
            }
            onConfirm={() => void handleBatchDelete()}
          >
            <Button type="link" size="small" danger icon={<DeleteOutlined />} disabled={selectedReadIds.length === 0}>
              {t('messages.batchDelete')}
            </Button>
          </Popconfirm>
        </Space>
      }
    >
      <div style={{ display: 'flex', flexDirection: 'column', height: panelHeight, minHeight: 0 }}>
        <Card
          style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' }}
          styles={{
            body: { flex: 1, minHeight: 0, padding: 0, display: 'flex', flexDirection: 'column' },
          }}
        >
          <div
            style={{
              flexShrink: 0,
              display: 'flex',
              alignItems: 'center',
              gap: spacing.sm,
              padding: `${spacing.xxs}px ${spacing.md}px`,
              borderBottom: `1px solid ${token.colorSplit}`,
            }}
          >
            <Checkbox
              checked={allChecked}
              indeterminate={!allChecked && someChecked}
              disabled={currentPageIds.length === 0}
              onChange={handleToggleAll}
            >
              {t('messages.selectAll')}
            </Checkbox>
            <Text type="secondary" style={{ fontSize: 13 }}>
              {t('messages.selectedCount', { count: selectedIds.length })}
            </Text>
          </div>
          <div style={{ flex: 1, minHeight: 0, overflowY: 'auto', opacity: loading ? 0.6 : 1, transition: 'opacity 0.2s' }}>
            {items.length === 0 && !loading ? (
              <div style={{ padding: spacing.xxl }}>
                <Empty description={t('messages.empty')} />
              </div>
            ) : (
              items.map(renderRow)
            )}
          </div>
        </Card>
        <div style={{ flexShrink: 0, display: 'flex', justifyContent: 'flex-end', paddingTop: spacing.md }}>
          <Pagination
            current={pageNo}
            pageSize={pageSize}
            total={total}
            showSizeChanger
            showTotal={(count) => t('ds.table.total', { total: count })}
            onChange={(page, size) => {
              setPageNo(page)
              setPageSize(size)
            }}
          />
        </div>
      </div>
    </Page>
  )
}
