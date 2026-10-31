/**
 * 团队定时器分区：列表 + 新建/编辑弹窗 + 立即执行 + 执行历史抽屉。
 * 定时器绑定团队内已发布的 Agent/流程应用，按 cron（UTC）无头执行一次提问；
 * 立即执行用于调试，失败按配置的重试次数与间隔自动重试。
 */

import { useCallback, useEffect, useState } from 'react'
import { Badge, Button, Form, Input, InputNumber, Modal, Popconfirm, Select, Space, Switch, Tag, Tooltip, Typography } from 'antd'
import {
  CaretRightOutlined,
  DeleteOutlined,
  EditOutlined,
  HistoryOutlined,
  PlusOutlined,
} from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { AuditCell, CronExpressionInput, DataTable, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { formatDateTime } from '@/utils/datetime'
import { getApps, type AppItem } from '@/api/app'
import {
  TIMER_STATUS,
  createTeamTimer,
  deleteTeamTimer,
  getTeamTimers,
  isTimerStatusActive,
  runTeamTimer,
  updateTeamTimer,
  type TeamTimerItem,
} from '@/api/timer'
import { TimerRunsDrawer } from './TimerRunsDrawer'

const { Text } = Typography

/** 调度表达式选择器的展示时区：北京时间（提交值仍为 UTC） */
const CRON_DISPLAY_OFFSET_MINUTES = 480

interface TimerFormValues {
  name: string
  description?: string
  appId: string
  cron: string
  inputContent: string
  isEnabled: boolean
  maxRetryCount: number
  retryIntervalSeconds: number
}

/** 行记录 → 表单回填值（appId 统一转字符串，编辑回显用） */
function toFormValues(record: TeamTimerItem | null): TimerFormValues {
  return {
    name: record?.name ?? '',
    description: record?.description || undefined,
    appId: record?.appId ? String(record.appId) : undefined,
    cron: record?.cronExpression ?? '0 1 * * *',
    inputContent: record?.inputContent ?? '',
    isEnabled: record?.isEnabled ?? true,
    maxRetryCount: record?.maxRetryCount ?? 0,
    retryIntervalSeconds: record?.retryIntervalSeconds ?? 60,
  } as TimerFormValues
}

export function TeamTimers({ teamId, canManage }: { teamId: number; canManage: boolean }) {
  const { t } = useTranslation()

  const [loading, setLoading] = useState(false)
  const [items, setItems] = useState<TeamTimerItem[]>([])
  const [total, setTotal] = useState(0)
  const [pageNo, setPageNo] = useState(1)
  const [pageSize, setPageSize] = useState(20)
  const [keyword, setKeyword] = useState('')

  // 新建 / 编辑弹窗
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<TeamTimerItem | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [form] = Form.useForm<TimerFormValues>()
  const [maxRetryCount, setMaxRetryCount] = useState(0)

  // 绑定应用下拉数据
  const [apps, setApps] = useState<AppItem[]>([])

  // 立即执行的行级 loading
  const [runningId, setRunningId] = useState<string | null>(null)

  // 执行历史抽屉
  const [runsTimer, setRunsTimer] = useState<TeamTimerItem | null>(null)
  const [runsOpen, setRunsOpen] = useState(false)

  const load = useCallback(async () => {
    if (!Number.isFinite(teamId) || teamId <= 0) return
    setLoading(true)
    try {
      const res = await getTeamTimers(teamId, { pageNo, pageSize, keyword: keyword.trim() || undefined })
      setItems(res.items)
      setTotal(res.totalCount)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [teamId, pageNo, pageSize, keyword])

  useEffect(() => {
    void load()
  }, [load])

  // 有执行中的任务时每 5 秒自动刷新列表，状态翻到终态后停止
  const hasActiveRun = items.some((x) => isTimerStatusActive(x.lastExecutionStatus))
  useEffect(() => {
    if (!hasActiveRun) return
    const pollId = window.setInterval(() => void load(), 5000)
    return () => window.clearInterval(pollId)
  }, [hasActiveRun, load])

  const loadApps = useCallback(async () => {
    if (!Number.isFinite(teamId) || teamId <= 0) return
    try {
      const res = await getApps(teamId)
      // 仅已发布应用可被定时执行，未发布的直接不出现在下拉
      setApps((res.items ?? []).filter((app) => app.publishStatus === 1))
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }, [teamId])

  const openCreate = () => {
    setEditing(null)
    const values = toFormValues(null)
    setMaxRetryCount(values.maxRetryCount)
    form.setFieldsValue(values)
    setModalOpen(true)
    void loadApps()
  }

  const openEdit = (record: TeamTimerItem) => {
    setEditing(record)
    const values = toFormValues(record)
    setMaxRetryCount(values.maxRetryCount)
    form.setFieldsValue(values)
    setModalOpen(true)
    void loadApps()
  }

  const handleSubmit = async () => {
    let values: TimerFormValues
    try {
      values = await form.validateFields()
    } catch {
      // 校验失败由表单红字提示
      return
    }
    setSubmitting(true)
    try {
      const payload = {
        name: values.name.trim(),
        description: values.description?.trim(),
        appId: String(values.appId),
        cronExpression: values.cron.trim(),
        inputContent: values.inputContent,
        isEnabled: values.isEnabled,
        maxRetryCount: values.maxRetryCount,
        // 不开启重试（0 次）时间隔输入框不渲染，validateFields 返回值缺该字段；统一兜底默认 60
        retryIntervalSeconds: values.maxRetryCount > 0 ? (values.retryIntervalSeconds ?? 60) : 60,
      }
      if (editing?.timerId) {
        await updateTeamTimer(String(editing.timerId), payload)
        feedback.success(t('timer.updateSuccess'))
      } else {
        await createTeamTimer(teamId, payload)
        feedback.success(t('timer.createSuccess'))
      }
      setModalOpen(false)
      await load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSubmitting(false)
    }
  }

  const handleDelete = async (record: TeamTimerItem) => {
    try {
      await deleteTeamTimer(String(record.timerId))
      feedback.success(t('timer.deleteSuccess'))
      await load()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleRunNow = async (record: TeamTimerItem) => {
    setRunningId(String(record.timerId))
    try {
      await runTeamTimer(String(record.timerId))
      feedback.success(t('timer.runTriggered'))
      setRunsTimer(record)
      setRunsOpen(true)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setRunningId(null)
    }
  }

  const handleToggleEnabled = async (record: TeamTimerItem, enabled: boolean) => {
    try {
      await updateTeamTimer(String(record.timerId), {
        name: record.name ?? '',
        description: record.description || undefined,
        appId: String(record.appId),
        cronExpression: record.cronExpression ?? '',
        inputContent: record.inputContent ?? '',
        isEnabled: enabled,
        maxRetryCount: record.maxRetryCount ?? 0,
        retryIntervalSeconds: record.retryIntervalSeconds ?? 60,
      })
      feedback.success(t('timer.updateSuccess'))
      await load()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const renderApp = (record: TeamTimerItem) => {
    if (!record.appName) {
      return <Text type="secondary">{t('timer.appDeleted')}</Text>
    }
    return (
      <Space size={spacing.xs}>
        <span>{record.appName}</span>
        <Tag color={record.appType === 'workflow' ? 'purple' : 'blue'}>
          {record.appType === 'workflow' ? t('timer.appTypeWorkflow') : t('timer.appTypeAgent')}
        </Tag>
        {record.appPublishStatus !== 1 && (
          <Tooltip title={t('timer.appUnpublishedTip')}>
            <Tag color="warning">{t('timer.appUnpublished')}</Tag>
          </Tooltip>
        )}
      </Space>
    )
  }

  const renderCron = (record: TeamTimerItem) => (
    <div>
      <code style={{ fontSize: 12 }}>{record.cronExpression}</code>
      <div>
        <Text type="secondary" style={{ fontSize: 12 }}>
          {!record.isEnabled
            ? t('timer.paused')
            : record.nextRunTime
              ? `${t('timer.nextRun')}${formatDateTime(record.nextRunTime)}`
              : '-'}
        </Text>
      </div>
    </div>
  )

  const renderRetry = (record: TeamTimerItem) => {
    const count = record.maxRetryCount ?? 0
    if (count <= 0) return <Text type="secondary">{t('timer.noRetry')}</Text>
    return (
      <Text style={{ fontSize: 12 }}>
        {t('timer.retryText', { count, seconds: record.retryIntervalSeconds ?? 60 })}
      </Text>
    )
  }

  /** 执行状态：活跃（等待/执行中/待重试）蓝点呼吸提示，终态显示结果，未执行过为 - */
  const renderExecStatus = (record: TeamTimerItem) => {
    const status = record.lastExecutionStatus
    if (!status) return <Text type="secondary">-</Text>
    const text = t(`timer.status.${status}`)
    if (isTimerStatusActive(status)) {
      return <Badge status="processing" text={text} />
    }
    if (status === TIMER_STATUS.Success) {
      return <Badge status="success" text={text} />
    }
    if (status === TIMER_STATUS.Failed) {
      return <Badge status="error" text={text} />
    }
    return <Text type="secondary">{text}</Text>
  }

  return (
    <>
      <DataTable<TeamTimerItem>
        rowKey="timerId"
        loading={loading}
        dataSource={items}
        scroll={{ x: 'max-content' }}
        onRefresh={() => void load()}
        refreshLoading={loading}
        toolbar={
          <div style={{ display: 'flex', alignItems: 'center', flexWrap: 'wrap', gap: spacing.sm }}>
            <Input.Search
              value={keyword}
              onChange={(e) => setKeyword(e.target.value)}
              onSearch={(v) => {
                setKeyword(v)
                setPageNo(1)
              }}
              placeholder={t('timer.searchPlaceholder')}
              allowClear
              maxLength={100}
              style={{ width: 220 }}
            />
            {canManage && (
              <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
                {t('timer.create')}
              </Button>
            )}
          </div>
        }
        pagination={{
          current: pageNo,
          pageSize,
          total,
          onChange: (page, size) => {
            setPageNo(page)
            setPageSize(size)
          },
        }}
        columns={[
          {
            title: t('timer.colName'),
            dataIndex: 'name',
            width: 200,
            render: (v: string, record) => (
              <div>
                <div style={{ fontWeight: 500 }}>{v}</div>
                {record.description && (
                  <Text type="secondary" style={{ fontSize: 12 }} ellipsis={{ tooltip: record.description }}>
                    {record.description}
                  </Text>
                )}
              </div>
            ),
          },
          {
            title: t('timer.colApp'),
            key: 'app',
            width: 220,
            render: (_, record) => renderApp(record),
          },
          {
            title: t('timer.colCron'),
            key: 'cron',
            width: 190,
            render: (_, record) => renderCron(record),
          },
          {
            title: t('timer.colExecStatus'),
            key: 'execStatus',
            width: 110,
            render: (_, record) => renderExecStatus(record),
          },
          {
            title: t('timer.colInput'),
            dataIndex: 'inputContent',
            width: 260,
            // 执行指令常含超长无空格 URL：必须约束宽度 + ellipsis，否则 auto 布局把整行撑出容器
            ellipsis: { showTitle: true },
          },
          {
            title: t('timer.colRetry'),
            key: 'retry',
            width: 150,
            render: (_, record) => renderRetry(record),
          },
          {
            title: t('timer.colCreate'),
            key: 'create',
            width: 150,
            render: (_, record) => <AuditCell user={record.createUserName} time={record.createTime} />,
          },
          {
            title: t('timer.colUpdate'),
            key: 'update',
            width: 150,
            render: (_, record) => <AuditCell user={record.updateUserName} time={record.updateTime} />,
          },
          {
            title: t('timer.colLastRun'),
            dataIndex: 'lastRunTime',
            width: 160,
            render: (v: string | null) => (v ? formatDateTime(v) : '-'),
          },
          {
            title: t('timer.colEnabled'),
            dataIndex: 'isEnabled',
            width: 90,
            render: (v: boolean, record) =>
              canManage ? (
                <Switch
                  size="small"
                  checked={v}
                  onChange={(checked) => void handleToggleEnabled(record, checked)}
                />
              ) : (
                <Tag color={v ? 'success' : 'default'}>{v ? t('timer.enabledTag') : t('timer.paused')}</Tag>
              ),
          },
          ...(canManage
            ? [
                {
                  title: t('timer.colActions'),
                  key: 'actions',
                  width: 320,
                  fixed: 'right' as const,
                  render: (_: unknown, record: TeamTimerItem) => (
                    <Space size={0}>
                      <Button
                        type="link"
                        size="small"
                        icon={<CaretRightOutlined />}
                        loading={runningId === String(record.timerId)}
                        onClick={() => void handleRunNow(record)}
                      >
                        {t('timer.runNow')}
                      </Button>
                      <Button
                        type="link"
                        size="small"
                        icon={<HistoryOutlined />}
                        onClick={() => {
                          setRunsTimer(record)
                          setRunsOpen(true)
                        }}
                      >
                        {t('timer.history')}
                      </Button>
                      <Button type="link" size="small" icon={<EditOutlined />} onClick={() => openEdit(record)}>
                        {t('timer.edit')}
                      </Button>
                      <Popconfirm title={t('timer.deleteConfirm')} onConfirm={() => void handleDelete(record)}>
                        <Button type="link" size="small" danger icon={<DeleteOutlined />}>
                          {t('timer.delete')}
                        </Button>
                      </Popconfirm>
                    </Space>
                  ),
                },
              ]
            : []),
        ]}
      />
      <Modal
        title={editing ? t('timer.editTitle') : t('timer.createTitle')}
        open={modalOpen}
        onCancel={() => setModalOpen(false)}
        onOk={() => void handleSubmit()}
        confirmLoading={submitting}
        okText={t('timer.save')}
        cancelText={t('timer.cancel')}
        maskClosable={false}
        destroyOnHidden
        width={560}
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="name"
            label={t('timer.name')}
            rules={[{ required: true, message: t('timer.namePlaceholder') }, { max: 100 }]}
          >
            <Input placeholder={t('timer.namePlaceholder')} maxLength={100} />
          </Form.Item>
          <Form.Item name="description" label={t('timer.description')} rules={[{ max: 255 }]}>
            <Input placeholder={t('timer.descriptionPlaceholder')} maxLength={255} />
          </Form.Item>
          <Form.Item
            name="appId"
            label={t('timer.app')}
            rules={[{ required: true, message: t('timer.appPlaceholder') }]}
          >
            <Select
              showSearch
              optionFilterProp="title"
              placeholder={t('timer.appPlaceholder')}
              notFoundContent={t('timer.appEmptyPublished')}
              options={apps.map((app) => ({
                value: String(app.appId),
                title: app.name ?? '',
                label: (
                  <Space size={spacing.xs}>
                    <span>{app.name}</span>
                    <Tag color={app.appType === 'workflow' ? 'purple' : 'blue'} style={{ marginInlineEnd: 0 }}>
                      {app.appType === 'workflow' ? t('timer.appTypeWorkflow') : t('timer.appTypeAgent')}
                    </Tag>
                  </Space>
                ),
              }))}
            />
          </Form.Item>
          <Form.Item
            name="cron"
            label={t('timer.cron')}
            rules={[{ required: true, message: t('timer.cronRequired') }]}
            extra={t('timer.cronHint')}
          >
            <CronExpressionInput displayOffsetMinutes={CRON_DISPLAY_OFFSET_MINUTES} />
          </Form.Item>
          <Form.Item
            name="inputContent"
            label={t('timer.inputContent')}
            rules={[{ required: true, message: t('timer.inputContentPlaceholder') }, { max: 8000 }]}
          >
            <Input.TextArea placeholder={t('timer.inputContentPlaceholder')} maxLength={8000} rows={4} showCount />
          </Form.Item>
          <Space size={spacing.lg} wrap>
            <Form.Item name="isEnabled" label={t('timer.enabled')} valuePropName="checked">
              <Switch />
            </Form.Item>
            <Form.Item name="maxRetryCount" label={t('timer.retryCount')} rules={[{ required: true }]}>
              <InputNumber
                min={0}
                max={5}
                precision={0}
                style={{ width: 100 }}
                onChange={(v) => setMaxRetryCount(Number(v ?? 0))}
              />
            </Form.Item>
            {maxRetryCount > 0 && (
              <Form.Item name="retryIntervalSeconds" label={t('timer.retryInterval')} rules={[{ required: true }]}>
                <InputNumber min={10} max={3600} step={10} precision={0} style={{ width: 120 }} />
              </Form.Item>
            )}
          </Space>
        </Form>
      </Modal>
      <TimerRunsDrawer
        timer={runsTimer}
        open={runsOpen}
        onClose={() => {
          setRunsOpen(false)
          void load()
        }}
      />
    </>
  )
}
