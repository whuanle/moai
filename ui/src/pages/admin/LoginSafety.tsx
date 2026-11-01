import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  CheckOutlined,
  ReloadOutlined,
  StopOutlined,
  UndoOutlined,
} from '@ant-design/icons'
import { Badge, Button, Form, Input, InputNumber, Modal, Popconfirm, Select, Space, Tabs, Tag, Tooltip, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { Navigate, useSearchParams } from 'react-router'
import { DataTable, Page, QueryBar, feedback } from '@/design-system'
import { controlHeight, spacing, tableCardChromeHeight } from '@/design-system/theme'
import { useAppStore } from '@/store/app'
import { useBadgeStore } from '@/store/badges'
import { formatDateTime } from '@/utils/datetime'
import './LoginSafety.css'
import {
  addIpBan,
  getIpBans,
  getLoginLogs,
  getRiskEventUnreadCount,
  getRiskEvents,
  liftIpBan,
  markRiskEventsRead,
  type LoginIpBanItem,
  type LoginLogItem,
  type LoginRiskEventItem,
} from '@/api/loginsafety'

const { Text } = Typography

const pageVerticalPadding = spacing.lg * 2
const tabsBarHeight = 46
const queryBarHeight = controlHeight + spacing.md
const tableHeaderHeight = 55
const tablePaginationHeight = 57
const panelHeight = `calc(100vh - ${pageVerticalPadding + tabsBarHeight}px)`
const panelTableScrollY = `calc(100vh - ${pageVerticalPadding + tabsBarHeight + queryBarHeight + tableHeaderHeight + tablePaginationHeight + tableCardChromeHeight}px)`

/** 风险等级 → 展示 Tag */
function renderRiskLevel(level: number | undefined, t: (key: string) => string) {
  switch (level) {
    case 3:
      return <Tag color="red">{t('loginsafety.riskHigh')}</Tag>
    case 2:
      return <Tag color="orange">{t('loginsafety.riskMedium')}</Tag>
    case 1:
      return <Tag color="blue">{t('loginsafety.riskLow')}</Tag>
    default:
      return <Tag>{t('loginsafety.riskNone')}</Tag>
  }
}

/** 规则编码 → 展示标题（i18n 缺失时回退编码本身） */
function ruleTitle(code: string | null | undefined, t: (key: string) => string): string {
  if (!code) return '-'
  const key = `loginsafety.rule.${code}`
  const title = t(key)
  return title === key ? code : `${code} ${title}`
}

/** 登录安全页（仅管理员）：Tab 隔离 登录日志 / 风险事件 / IP 封禁 */
export function LoginSafety() {
  const { t } = useTranslation()
  const isAdmin = useAppStore((state) => state.userInfo?.isAdmin === true)

  const [searchParams, setSearchParams] = useSearchParams()
  const activeTab = searchParams.get('tab') ?? 'loginlog'

  if (!isAdmin) {
    return <Navigate to="/apps" replace />
  }

  const handleTabChange = (key: string) => {
    setSearchParams(key === 'loginlog' ? {} : { tab: key }, { replace: true })
  }

  return (
    <Page>
      <Tabs
        activeKey={activeTab}
        onChange={handleTabChange}
        style={{ minHeight: panelHeight }}
        items={[
          { key: 'loginlog', label: t('loginsafety.tabLoginLog'), children: <LoginLogPanel /> },
          { key: 'riskevent', label: t('loginsafety.tabRiskEvent'), children: <RiskEventPanel /> },
          { key: 'ipban', label: t('loginsafety.tabIpBan'), children: <IpBanPanel /> },
        ]}
      />
    </Page>
  )
}

/** 登录日志面板 */
function LoginLogPanel() {
  const { t } = useTranslation()
  const [form] = Form.useForm()
  const [loading, setLoading] = useState(true)
  const [items, setItems] = useState<LoginLogItem[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [pageNo, setPageNo] = useState(1)
  const [pageSize, setPageSize] = useState(20)

  const load = useCallback(
    async (page = pageNo, size = pageSize) => {
      setLoading(true)
      try {
        const values = form.getFieldsValue()
        const res = await getLoginLogs({
          pageNo: page,
          pageSize: size,
          userName: values.userName?.trim() || undefined,
          ip: values.ip?.trim() || undefined,
          isSuccess: values.isSuccess,
          riskLevel: values.riskLevel,
        })
        setItems(res.items ?? [])
        setTotalCount(res.total ?? 0)
      } catch {
        // 错误已由全局请求中间件统一提示
      } finally {
        setLoading(false)
      }
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [pageNo, pageSize],
  )

  useEffect(() => {
    void load(1, pageSize)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const columns: TableColumnsType<LoginLogItem> = useMemo(
    () => [
      {
        title: t('loginsafety.colTime'),
        dataIndex: 'createTime',
        width: 160,
        render: (v: string | null) => formatDateTime(v),
      },
      { title: t('loginsafety.colUserName'), dataIndex: 'userName', width: 140, ellipsis: true },
      {
        title: t('loginsafety.colLoginType'),
        dataIndex: 'loginType',
        width: 90,
        render: (v: number) => (v === 1 ? <Tag color="geekblue">{t('loginsafety.loginTypeOAuth')}</Tag> : <Tag>{t('loginsafety.loginTypePassword')}</Tag>),
      },
      {
        title: t('loginsafety.colResult'),
        dataIndex: 'isSuccess',
        width: 90,
        render: (v: boolean) =>
          v ? <Badge status="success" text={t('loginsafety.resultSuccess')} /> : <Badge status="error" text={t('loginsafety.resultFail')} />,
      },
      {
        title: t('loginsafety.colFailReason'),
        dataIndex: 'failReason',
        width: 140,
        ellipsis: true,
        render: (v: string | null) => v || '-',
      },
      { title: t('loginsafety.colIp'), dataIndex: 'ip', width: 130 },
      {
        title: t('loginsafety.colLocation'),
        key: 'location',
        width: 170,
        ellipsis: true,
        render: (_, record) => {
          const location = [record.province, record.city].filter(Boolean).join(' ')
          const isp = record.isp ? ` ${record.isp}` : ''
          const text = `${location}${isp}`.trim()
          return text || '-'
        },
      },
      {
        title: t('loginsafety.colRisk'),
        dataIndex: 'riskLevel',
        width: 90,
        render: (v: number) => renderRiskLevel(v, t),
      },
      {
        title: t('loginsafety.colUserAgent'),
        dataIndex: 'userAgent',
        ellipsis: true,
        render: (v: string | null) => (
          <Tooltip title={v} placement="topLeft">
            <Text type="secondary" style={{ fontSize: 12 }}>{v || '-'}</Text>
          </Tooltip>
        ),
      },
    ],
    [t],
  )

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: panelHeight, minHeight: 0 }}>
      <QueryBar
        onSearch={() => {
          setPageNo(1)
          void load(1, pageSize)
        }}
        onReset={() => {
          form.resetFields()
          setPageNo(1)
          void load(1, pageSize)
        }}
        loading={loading}
      >
        <Form.Item name="userName">
          <Input placeholder={t('loginsafety.searchUserName')} allowClear maxLength={50} style={{ width: 180 }} />
        </Form.Item>
        <Form.Item name="ip">
          <Input placeholder={t('loginsafety.searchIp')} allowClear maxLength={45} style={{ width: 160 }} />
        </Form.Item>
        <Form.Item name="isSuccess">
          <Select placeholder={t('loginsafety.searchResult')} allowClear style={{ width: 120 }}
            options={[
              { value: true, label: t('loginsafety.resultSuccess') },
              { value: false, label: t('loginsafety.resultFail') },
            ]}
          />
        </Form.Item>
        <Form.Item name="riskLevel">
          <Select placeholder={t('loginsafety.searchRisk')} allowClear style={{ width: 120 }}
            options={[
              { value: 3, label: t('loginsafety.riskHigh') },
              { value: 2, label: t('loginsafety.riskMedium') },
              { value: 1, label: t('loginsafety.riskLow') },
              { value: 0, label: t('loginsafety.riskNone') },
            ]}
          />
        </Form.Item>
      </QueryBar>
      <div style={{ flex: 1, minHeight: 0 }}>
        <DataTable<LoginLogItem>
          rowKey="id"
          columns={columns}
          dataSource={items}
          loading={loading}
          sticky
          scroll={{ x: 1300, y: panelTableScrollY }}
          pagination={{
            current: pageNo,
            pageSize,
            total: totalCount,
            showSizeChanger: true,
            onChange: (page, size) => {
              setPageNo(page)
              setPageSize(size)
              void load(page, size)
            },
          }}
        />
      </div>
    </div>
  )
}

/** 风险事件面板 */
function RiskEventPanel() {
  const { t } = useTranslation()
  const [form] = Form.useForm()
  const setBadge = useBadgeStore((state) => state.setBadge)
  const [loading, setLoading] = useState(true)
  const [items, setItems] = useState<LoginRiskEventItem[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [pageNo, setPageNo] = useState(1)
  const [pageSize, setPageSize] = useState(20)
  const [selectedIds, setSelectedIds] = useState<string[]>([])

  const refreshBadge = useCallback(async () => {
    try {
      setBadge('logEvents', await getRiskEventUnreadCount())
    } catch {
      // 静默
    }
  }, [setBadge])

  const load = useCallback(
    async (page = pageNo, size = pageSize) => {
      setLoading(true)
      try {
        const values = form.getFieldsValue()
        const res = await getRiskEvents({
          pageNo: page,
          pageSize: size,
          userName: values.userName?.trim() || undefined,
          isRead: values.isRead,
          riskLevel: values.riskLevel,
        })
        setItems(res.items ?? [])
        setTotalCount(res.total ?? 0)
        setSelectedIds([])
      } catch {
        // 错误已由全局请求中间件统一提示
      } finally {
        setLoading(false)
      }
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [pageNo, pageSize],
  )

  useEffect(() => {
    void load(1, pageSize)
    void refreshBadge()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const handleMarkRead = async (ids: string[]) => {
    if (ids.length === 0) return
    try {
      await markRiskEventsRead(ids)
      feedback.success(t('loginsafety.markReadSuccess'))
      void refreshBadge()
      void load(pageNo, pageSize)
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const columns: TableColumnsType<LoginRiskEventItem> = useMemo(
    () => [
      {
        title: t('loginsafety.colTime'),
        dataIndex: 'createTime',
        width: 160,
        render: (v: string | null) => formatDateTime(v),
      },
      { title: t('loginsafety.colUserName'), dataIndex: 'userName', width: 130, ellipsis: true },
      {
        title: t('loginsafety.colRule'),
        dataIndex: 'ruleCode',
        width: 170,
        render: (v: string | null) => (
          <Tag color={v === 'R1' || v === 'R2' || v === 'R5' || v === 'R6' ? 'red' : 'orange'}>{ruleTitle(v, t)}</Tag>
        ),
      },
      {
        title: t('loginsafety.colRisk'),
        dataIndex: 'riskLevel',
        width: 90,
        render: (v: number) => renderRiskLevel(v, t),
      },
      { title: t('loginsafety.colSummary'), dataIndex: 'summary', ellipsis: true },
      { title: t('loginsafety.colIp'), dataIndex: 'ip', width: 130 },
      {
        title: t('loginsafety.colLocation'),
        key: 'location',
        width: 130,
        ellipsis: true,
        render: (_, record) => [record.province, record.city].filter(Boolean).join(' ') || '-',
      },
      {
        title: t('loginsafety.colReadStatus'),
        dataIndex: 'isRead',
        width: 90,
        render: (v: boolean) =>
          v ? <Text type="secondary">{t('loginsafety.readStatusDone')}</Text> : <Badge status="processing" text={t('loginsafety.readStatusUnread')} />,
      },
      {
        title: t('loginsafety.colActions'),
        key: 'actions',
        width: 110,
        fixed: 'right',
        render: (_, record) =>
          record.isRead ? null : (
            <Button type="link" size="small" icon={<CheckOutlined />} onClick={() => void handleMarkRead([record.id as string])}>
              {t('loginsafety.markRead')}
            </Button>
          ),
      },
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, pageNo, pageSize],
  )

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: panelHeight, minHeight: 0 }}>
      <QueryBar
        onSearch={() => {
          setPageNo(1)
          void load(1, pageSize)
        }}
        onReset={() => {
          form.resetFields()
          setPageNo(1)
          void load(1, pageSize)
        }}
        loading={loading}
        extra={
          <Space>
            <Button
              icon={<CheckOutlined />}
              disabled={selectedIds.length === 0}
              onClick={() => void handleMarkRead(selectedIds)}
            >
              {t('loginsafety.markSelectedRead', { count: selectedIds.length })}
            </Button>
            <Button icon={<ReloadOutlined />} onClick={() => void load(pageNo, pageSize)}>
              {t('loginsafety.refresh')}
            </Button>
          </Space>
        }
      >
        <Form.Item name="userName">
          <Input placeholder={t('loginsafety.searchUserName')} allowClear maxLength={50} style={{ width: 180 }} />
        </Form.Item>
        <Form.Item name="isRead">
          <Select placeholder={t('loginsafety.searchReadStatus')} allowClear style={{ width: 130 }}
            options={[
              { value: false, label: t('loginsafety.readStatusUnread') },
              { value: true, label: t('loginsafety.readStatusDone') },
            ]}
          />
        </Form.Item>
        <Form.Item name="riskLevel">
          <Select placeholder={t('loginsafety.searchRisk')} allowClear style={{ width: 120 }}
            options={[
              { value: 3, label: t('loginsafety.riskHigh') },
              { value: 2, label: t('loginsafety.riskMedium') },
            ]}
          />
        </Form.Item>
      </QueryBar>
      <div style={{ flex: 1, minHeight: 0 }}>
        <DataTable<LoginRiskEventItem>
          rowKey="id"
          columns={columns}
          dataSource={items}
          loading={loading}
          sticky
          rowSelection={{
            selectedRowKeys: selectedIds,
            onChange: (keys) => setSelectedIds(keys as string[]),
            getCheckboxProps: (record) => ({ disabled: record.isRead === true }),
          }}
          rowClassName={(record) => (record.isRead ? '' : 'loginsafety-unread-row')}
          scroll={{ x: 1300, y: panelTableScrollY }}
          pagination={{
            current: pageNo,
            pageSize,
            total: totalCount,
            showSizeChanger: true,
            onChange: (page, size) => {
              setPageNo(page)
              setPageSize(size)
              void load(page, size)
            },
          }}
        />
      </div>
    </div>
  )
}

/** IP 封禁面板 */
function IpBanPanel() {
  const { t } = useTranslation()
  const [form] = Form.useForm()
  const [addForm] = Form.useForm()
  const [loading, setLoading] = useState(true)
  const [items, setItems] = useState<LoginIpBanItem[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [pageNo, setPageNo] = useState(1)
  const [pageSize, setPageSize] = useState(20)
  const [addOpen, setAddOpen] = useState(false)
  const [submitting, setSubmitting] = useState(false)

  const load = useCallback(
    async (page = pageNo, size = pageSize) => {
      setLoading(true)
      try {
        const values = form.getFieldsValue()
        const res = await getIpBans({
          pageNo: page,
          pageSize: size,
          ip: values.ip?.trim() || undefined,
        })
        setItems(res.items ?? [])
        setTotalCount(res.total ?? 0)
      } catch {
        // 错误已由全局请求中间件统一提示
      } finally {
        setLoading(false)
      }
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [pageNo, pageSize],
  )

  useEffect(() => {
    void load(1, pageSize)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const handleLift = async (record: LoginIpBanItem) => {
    try {
      await liftIpBan(record.id as string)
      feedback.success(t('loginsafety.liftSuccess'))
      void load(pageNo, pageSize)
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleAdd = async () => {
    const values = await addForm.validateFields()
    setSubmitting(true)
    try {
      await addIpBan(values.ip.trim(), values.minutes, values.reason?.trim() ?? '')
      feedback.success(t('loginsafety.addBanSuccess'))
      setAddOpen(false)
      addForm.resetFields()
      void load(1, pageSize)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSubmitting(false)
    }
  }

  const renderStatus = (record: LoginIpBanItem) => {
    if (record.isLifted) return <Badge status="default" text={t('loginsafety.banStatusLifted')} />
    if (record.expireTime && new Date(record.expireTime).getTime() <= Date.now()) {
      return <Badge status="default" text={t('loginsafety.banStatusExpired')} />
    }
    return <Badge status="error" text={t('loginsafety.banStatusActive')} />
  }

  const renderSource = (source: string | null | undefined) => {
    if (source === 'manual') return <Tag color="purple">{t('loginsafety.banSourceManual')}</Tag>
    if (source === 'auto:R1') return <Tag color="red">R1 {t('loginsafety.banSourceAutoBrute')}</Tag>
    if (source === 'auto:R2') return <Tag color="red">R2 {t('loginsafety.banSourceAutoSpray')}</Tag>
    return <Tag>{source || '-'}</Tag>
  }

  const columns: TableColumnsType<LoginIpBanItem> = useMemo(
    () => [
      { title: t('loginsafety.colIp'), dataIndex: 'ip', width: 160 },
      { title: t('loginsafety.colBanReason'), dataIndex: 'reason', ellipsis: true },
      {
        title: t('loginsafety.colBanSource'),
        dataIndex: 'sourceRule',
        width: 150,
        render: (v: string | null) => renderSource(v),
      },
      {
        title: t('loginsafety.colBanTime'),
        dataIndex: 'createTime',
        width: 160,
        render: (v: string | null) => formatDateTime(v),
      },
      {
        title: t('loginsafety.colExpireTime'),
        dataIndex: 'expireTime',
        width: 160,
        render: (v: string | null) => formatDateTime(v),
      },
      {
        title: t('loginsafety.colBanStatus'),
        key: 'status',
        width: 110,
        render: (_, record) => renderStatus(record),
      },
      {
        title: t('loginsafety.colActions'),
        key: 'actions',
        width: 110,
        fixed: 'right',
        render: (_, record) => {
          const active = !record.isLifted && record.expireTime && new Date(record.expireTime).getTime() > Date.now()
          return active ? (
            <Popconfirm title={t('loginsafety.liftConfirm', { ip: record.ip ?? '' })} onConfirm={() => void handleLift(record)}>
              <Button type="link" size="small" icon={<UndoOutlined />}>
                {t('loginsafety.lift')}
              </Button>
            </Popconfirm>
          ) : null
        },
      },
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, pageNo, pageSize],
  )

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: panelHeight, minHeight: 0 }}>
      <QueryBar
        onSearch={() => {
          setPageNo(1)
          void load(1, pageSize)
        }}
        onReset={() => {
          form.resetFields()
          setPageNo(1)
          void load(1, pageSize)
        }}
        loading={loading}
        extra={
          <Button danger icon={<StopOutlined />} onClick={() => setAddOpen(true)}>
            {t('loginsafety.addBan')}
          </Button>
        }
      >
        <Form.Item name="ip">
          <Input placeholder={t('loginsafety.searchIp')} allowClear maxLength={45} style={{ width: 180 }} />
        </Form.Item>
      </QueryBar>
      <div style={{ flex: 1, minHeight: 0 }}>
        <DataTable<LoginIpBanItem>
          rowKey="id"
          columns={columns}
          dataSource={items}
          loading={loading}
          sticky
          scroll={{ x: 1100, y: panelTableScrollY }}
          pagination={{
            current: pageNo,
            pageSize,
            total: totalCount,
            showSizeChanger: true,
            onChange: (page, size) => {
              setPageNo(page)
              setPageSize(size)
              void load(page, size)
            },
          }}
        />
      </div>
      <Modal
        open={addOpen}
        title={t('loginsafety.addBanTitle')}
        onOk={() => void handleAdd()}
        onCancel={() => setAddOpen(false)}
        okText={t('loginsafety.confirm')}
        cancelText={t('loginsafety.cancel')}
        confirmLoading={submitting}
        destroyOnHidden
        maskClosable={false}
      >
        <Form form={addForm} layout="vertical">
          <Form.Item
            name="ip"
            label={t('loginsafety.banIpLabel')}
            rules={[{ required: true, message: t('loginsafety.banIpRequired') }]}
          >
            <Input maxLength={45} placeholder="1.2.3.4" />
          </Form.Item>
          <Form.Item
            name="minutes"
            label={t('loginsafety.banMinutesLabel')}
            initialValue={30}
            rules={[{ required: true, message: t('loginsafety.banMinutesRequired') }]}
          >
            <InputNumber min={1} max={525600} style={{ width: '100%' }} addonAfter={t('loginsafety.minutesUnit')} />
          </Form.Item>
          <Form.Item name="reason" label={t('loginsafety.banReasonLabel')}>
            <Input maxLength={200} />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  )
}
