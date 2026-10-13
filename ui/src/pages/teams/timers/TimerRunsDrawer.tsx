/**
 * 定时器执行历史抽屉：按触发时间倒序分页（默认加载最近 10 条），
 * 存在等待/执行中/待重试的记录时每 3 秒自动轮询，行展开查看回答与失败原因。
 */

import { useCallback, useEffect, useRef, useState } from 'react'
import { Drawer, Space, Tag, Typography, theme } from 'antd'
import { SyncOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { DataTable } from '@/design-system'
import { formatDateTime } from '@/utils/datetime'
import { getTeamTimerExecutions, isTimerStatusActive, type TeamTimerExecutionItem, type TeamTimerItem } from '@/api/timer'

const { Text, Paragraph } = Typography

const STATUS_COLOR: Record<string, string> = {
  pending: 'default',
  running: 'processing',
  success: 'success',
  failed: 'error',
  retrying: 'warning',
}

function renderDuration(ms: string | number | null | undefined, fallback: string): string {
  const value = Number(ms ?? 0)
  if (!ms || Number.isNaN(value) || value <= 0) return fallback
  if (value < 1000) return `${value} ms`
  return `${(value / 1000).toFixed(1)} s`
}

export function TimerRunsDrawer({ timer, open, onClose }: { timer: TeamTimerItem | null; open: boolean; onClose: () => void }) {
  const { t } = useTranslation()
  const { token } = theme.useToken()

  const [items, setItems] = useState<TeamTimerExecutionItem[]>([])
  const [total, setTotal] = useState(0)
  const [pageNo, setPageNo] = useState(1)
  const [pageSize, setPageSize] = useState(10)
  const [loading, setLoading] = useState(false)
  const loadingRef = useRef(false)

  const timerId = timer?.timerId ? String(timer.timerId) : ''

  const load = useCallback(async () => {
    if (!timerId || loadingRef.current) return
    loadingRef.current = true
    setLoading(true)
    try {
      const res = await getTeamTimerExecutions(timerId, pageNo, pageSize)
      setItems(res.items)
      setTotal(res.totalCount)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      loadingRef.current = false
      setLoading(false)
    }
  }, [timerId, pageNo, pageSize])

  useEffect(() => {
    if (open && timerId) {
      void load()
    }
  }, [open, timerId, load])

  // 有进行中的记录时自动轮询，全部到达终态后停止
  const hasActive = items.some((item) => isTimerStatusActive(item.status))
  useEffect(() => {
    if (!open || !hasActive) return
    const id = window.setInterval(() => void load(), 3000)
    return () => window.clearInterval(id)
  }, [open, hasActive, load])

  // 切换定时器/关闭时重置分页
  useEffect(() => {
    if (!open) {
      setPageNo(1)
      setItems([])
      setTotal(0)
    }
  }, [open, timerId])

  return (
    <Drawer
      title={t('timer.runsTitle', { name: timer?.name ?? '' })}
      placement="right"
      width={760}
      open={open}
      onClose={onClose}
      maskClosable={false}
    >
      <DataTable<TeamTimerExecutionItem>
        size="small"
        rowKey="executionId"
        loading={loading}
        dataSource={items}
        onRefresh={() => void load()}
        refreshLoading={loading}
        pagination={{
          current: pageNo,
          pageSize,
          total,
          showSizeChanger: false,
          onChange: (page, size) => {
            setPageNo(page)
            setPageSize(size)
          },
        }}
        expandable={{
          expandedRowRender: (record) => (
            <div style={{ display: 'grid', gap: 8 }}>
              {record.errorMessage && (
                <div>
                  <Text type="danger">{t('timer.colError')}</Text>
                  <Paragraph style={{ whiteSpace: 'pre-wrap', marginBottom: 0, color: token.colorError, fontSize: 12 }}>
                    {record.errorMessage}
                  </Paragraph>
                </div>
              )}
              <div>
                <Text type="secondary">{t('timer.colResultDetail')}</Text>
                <Paragraph style={{ whiteSpace: 'pre-wrap', marginBottom: 0, fontSize: 12 }}>
                  {record.result || '-'}
                </Paragraph>
              </div>
            </div>
          ),
          rowExpandable: (record) => Boolean(record.result || record.errorMessage),
        }}
        columns={[
          {
            title: t('timer.colTrigger'),
            dataIndex: 'triggerType',
            width: 90,
            render: (v: string) =>
              v === 'manual' ? <Tag>{t('timer.triggerManual')}</Tag> : <Tag color="blue">{t('timer.triggerScheduled')}</Tag>,
          },
          {
            title: t('timer.colStatus'),
            dataIndex: 'status',
            width: 130,
            render: (v: string, record) => (
              <Space size={4}>
                <Tag
                  color={STATUS_COLOR[v] ?? 'default'}
                  icon={v === 'running' ? <SyncOutlined spin /> : undefined}
                  style={{ marginInlineEnd: 0 }}
                >
                  {t(`timer.status.${v}`)}
                </Tag>
                {v === 'retrying' && record.nextRetryTime && (
                  <Text type="secondary" style={{ fontSize: 12 }}>
                    {formatDateTime(record.nextRetryTime)}
                  </Text>
                )}
              </Space>
            ),
          },
          {
            title: t('timer.colAttempt'),
            dataIndex: 'attemptCount',
            width: 80,
            render: (v: number) => v || '-',
          },
          {
            title: t('timer.colStart'),
            dataIndex: 'startTime',
            width: 165,
            render: (v: string | null, record) => (
              <div>
                <div>{v ? formatDateTime(v) : formatDateTime(record.createTime ?? '')}</div>
                <Text type="secondary" style={{ fontSize: 12 }}>
                  {renderDuration(record.durationMs, '')}
                </Text>
              </div>
            ),
          },
          {
            title: t('timer.colTriggerUser'),
            dataIndex: 'triggerUserName',
            width: 100,
            render: (v: string | null) => v || '-',
          },
          {
            title: t('timer.colResult'),
            key: 'result',
            ellipsis: true,
            render: (_, record) => {
              if (record.status === 'success') {
                return <Text style={{ fontSize: 12 }} ellipsis={{ tooltip: record.result }}>{record.result || '-'}</Text>
              }
              if (record.errorMessage) {
                return <Text type="danger" style={{ fontSize: 12 }} ellipsis={{ tooltip: record.errorMessage }}>{record.errorMessage}</Text>
              }
              return '-'
            },
          },
        ]}
      />
    </Drawer>
  )
}
