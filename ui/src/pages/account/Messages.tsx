import { useCallback, useEffect, useState } from 'react'
import { Button, Card, Empty, Pagination, Popconfirm, Segmented, Space, theme, Typography } from 'antd'
import { CheckOutlined, DeleteOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { Page, feedback } from '@/design-system'
import { formatDateTime } from '@/utils/datetime'
import { useNotificationStore } from '@/store/notification'
import { onEventPushEvent, EVENT_MESSAGE_NEW } from '@/realtime/eventPush'
import {
  deleteMessages,
  getMessages,
  markAllMessagesRead,
  markMessagesRead,
  type MessageItem,
} from '@/api/message'

const { Text } = Typography

type ReadFilter = 'all' | 'unread'

/** 站内信列表：未读高亮、点击标记已读、全部已读、删除（软删）；message.new 事件到达时在线刷新 */
export function Messages() {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const refreshUnread = useNotificationStore((state) => state.refresh)

  const [loading, setLoading] = useState(false)
  const [items, setItems] = useState<MessageItem[]>([])
  const [total, setTotal] = useState(0)
  const [pageNo, setPageNo] = useState(1)
  const [pageSize, setPageSize] = useState(10)
  const [filter, setFilter] = useState<ReadFilter>('all')

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

  const handleMarkRead = useCallback(async (id: string) => {
    await markMessagesRead([id])
    feedback.success(t('messages.readSuccess'))
    afterChange()
  }, [afterChange, t])

  const handleMarkAllRead = useCallback(async () => {
    await markAllMessagesRead()
    feedback.success(t('messages.markAllReadSuccess'))
    afterChange()
  }, [afterChange, t])

  const handleDelete = useCallback(async (id: string) => {
    await deleteMessages([id])
    feedback.success(t('messages.deleteSuccess'))
    afterChange()
  }, [afterChange, t])

  const unreadExists = items.some((item) => !item.isRead)

  const renderRow = (item: MessageItem) => {
    const sender = item.createUserName || t('messages.senderSystem')
    return (
      <div
        key={item.id}
        role="button"
        tabIndex={0}
        onClick={() => {
          if (!item.isRead) void handleMarkRead(item.id ?? '')
        }}
        onKeyDown={(e) => {
          if ((e.key === 'Enter' || e.key === ' ') && !item.isRead) {
            e.preventDefault()
            void handleMarkRead(item.id ?? '')
          }
        }}
        style={{
          display: 'flex',
          gap: 12,
          padding: '12px 16px',
          borderBottom: `1px solid ${token.colorSplit}`,
          cursor: item.isRead ? 'default' : 'pointer',
          alignItems: 'flex-start',
        }}
      >
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
            <Button type="link" size="small" icon={<CheckOutlined />} onClick={() => void handleMarkRead(item.id ?? '')}>
              {t('messages.markRead')}
            </Button>
          )}
          <Popconfirm
            title={t('messages.deleteConfirm')}
            onConfirm={() => void handleDelete(item.id ?? '')}
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
      title={t('messages.title')}
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
          <Popconfirm title={t('messages.markAllReadConfirm')} onConfirm={() => void handleMarkAllRead()}>
            <Button type="link" size="small" disabled={!unreadExists} icon={<CheckOutlined />}>
              {t('messages.markAllRead')}
            </Button>
          </Popconfirm>
        </Space>
      }
    >
      <Card styles={{ body: { padding: 0 } }}>
        {items.length === 0 && !loading ? (
          <div style={{ padding: 48 }}>
            <Empty description={t('messages.empty')} />
          </div>
        ) : (
          <div style={{ minHeight: 120, opacity: loading ? 0.6 : 1, transition: 'opacity 0.2s' }}>
            {items.map(renderRow)}
          </div>
        )}
      </Card>
      <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: 16 }}>
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
    </Page>
  )
}
