import { useCallback, useEffect, useMemo, useState } from 'react'
import { Alert, Button, DatePicker, Form, Input, Modal, Popconfirm, Space, Tag, Tooltip, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import { CopyOutlined, PlusOutlined, StopOutlined, ThunderboltOutlined } from '@ant-design/icons'
import dayjs, { type Dayjs } from 'dayjs'
import { useTranslation } from 'react-i18next'
import { Card as DSCard, DataTable, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { Env } from '@/config/env'
import { formatDateTime } from '@/utils/datetime'
import {
  createTeamApiKey,
  deleteTeamApiKey,
  getTeamApiKeys,
  getTeamGatewayModels,
  updateTeamApiKey,
  type TeamApiKeyItem,
  type TeamGatewayModelItem,
} from '@/api/gateway'

const { Text, Paragraph } = Typography

/** 重置周期单位：0=不重置 1=小时 2=天 3=周 4=月（对齐后端） */
const PERIOD_UNIT_KEYS = ['noReset', 'hour', 'day', 'week', 'month'] as const

interface CreateFormValues {
  name: string
  expireTime?: Dayjs
}

/** 将后端 long（可能序列化为字符串）安全转数值 */
function toNumber(v: number | string | null | undefined): number {
  const n = typeof v === 'number' ? v : Number(v)
  return Number.isFinite(n) ? n : 0
}

/**
 * 团队模型网关区块：API Key 管理 + 可用模型/额度 + 接入说明。
 * 挂载在团队管理页（TeamManage）下。
 */
export function TeamGateway({ teamId, canManage }: { teamId: number; canManage: boolean }) {
  const { t } = useTranslation()
  const [keys, setKeys] = useState<TeamApiKeyItem[]>([])
  const [models, setModels] = useState<TeamGatewayModelItem[]>([])
  const [keysLoading, setKeysLoading] = useState(true)
  const [modelsLoading, setModelsLoading] = useState(true)
  const [createOpen, setCreateOpen] = useState(false)
  const [creating, setCreating] = useState(false)
  const [createForm] = Form.useForm<CreateFormValues>()
  const [createdSecret, setCreatedSecret] = useState<{ secret: string; keyPrefix: string } | null>(null)
  const [baseUrl, setBaseUrl] = useState('')

  useEffect(() => {
    // 展示后端接入地址：dev 取 VITE_ServerUrl（后端 5000），生产同源部署回退 origin
    setBaseUrl(`${Env.serverUrl}/api/aigateway/${teamId}/v1`)
  }, [teamId])

  const reloadKeys = useCallback(async () => {
    if (!Number.isFinite(teamId) || teamId <= 0) return
    setKeysLoading(true)
    try {
      setKeys(await getTeamApiKeys(teamId))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setKeysLoading(false)
    }
  }, [teamId])

  const reloadModels = useCallback(async () => {
    if (!Number.isFinite(teamId) || teamId <= 0) return
    setModelsLoading(true)
    try {
      setModels(await getTeamGatewayModels(teamId))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setModelsLoading(false)
    }
  }, [teamId])

  useEffect(() => {
    if (!canManage) return
    void reloadKeys()
  }, [canManage, reloadKeys])

  useEffect(() => {
    void reloadModels()
  }, [reloadModels])

  const handleCreate = async () => {
    const values = await createForm.validateFields()
    setCreating(true)
    try {
      const result = await createTeamApiKey(teamId, {
        name: values.name,
        expireTime: values.expireTime ? values.expireTime.toISOString() : undefined,
      })
      setCreateOpen(false)
      createForm.resetFields()
      if (result.secret) {
        setCreatedSecret({ secret: result.secret, keyPrefix: result.keyPrefix ?? '' })
      }
      await reloadKeys()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setCreating(false)
    }
  }

  const handleToggle = async (item: TeamApiKeyItem) => {
    if (!item.id) return
    try {
      await updateTeamApiKey(teamId, item.id, { isDisable: !item.isDisable })
      feedback.success(t('gateway.updateSuccess'))
      await reloadKeys()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleDelete = async (item: TeamApiKeyItem) => {
    if (!item.id) return
    try {
      await deleteTeamApiKey(teamId, item.id)
      feedback.success(t('gateway.deleteSuccess'))
      await reloadKeys()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const copyText = async (text: string) => {
    try {
      await navigator.clipboard.writeText(text)
      feedback.success(t('common.copySuccess'))
    } catch {
      feedback.error(t('common.copyFailed'))
    }
  }

  const renderStatus = (item: TeamApiKeyItem) => {
    if (item.isExpired) return <Tag color="orange">{t('gateway.statusExpired')}</Tag>
    if (item.isDisable) return <Tag color="red">{t('gateway.statusDisabled')}</Tag>
    return <Tag color="green">{t('gateway.statusEnabled')}</Tag>
  }

  const keyColumns: TableColumnsType<TeamApiKeyItem> = useMemo(
    () => [
      { title: t('gateway.colKeyName'), dataIndex: 'name' },
      {
        title: t('gateway.colKeyPrefix'),
        dataIndex: 'keyPrefix',
        width: 170,
        render: (v: string | null) => <Text code>{v || '-'}</Text>,
      },
      { title: t('gateway.colStatus'), key: 'status', width: 100, render: (_, record) => renderStatus(record) },
      {
        title: t('gateway.colLastUsed'),
        dataIndex: 'lastUsedTime',
        width: 170,
        render: (v: string | null) => (v ? formatDateTime(v) : t('gateway.neverUsed')),
      },
      {
        title: t('gateway.colExpireTime'),
        dataIndex: 'expireTime',
        width: 170,
        render: (v: string | null) => (v ? formatDateTime(v) : t('gateway.neverExpire')),
      },
      {
        title: t('gateway.colCreateTime'),
        dataIndex: 'createTime',
        width: 170,
        render: (v: string | null) => (v ? formatDateTime(v) : '-'),
      },
      {
        title: t('gateway.colActions'),
        key: 'actions',
        width: 110,
        render: (_, record) => (
          <Space size={0}>
            <Tooltip title={record.isDisable ? t('gateway.enable') : t('gateway.disable')}>
              <Button
                type="text"
                size="small"
                icon={<StopOutlined />}
                aria-label={record.isDisable ? t('gateway.enable') : t('gateway.disable')}
                onClick={() => void handleToggle(record)}
              />
            </Tooltip>
            <Popconfirm title={t('gateway.deleteConfirm')} onConfirm={() => void handleDelete(record)}>
              <Button type="text" size="small" danger aria-label={t('gateway.delete')}>
                {t('gateway.delete')}
              </Button>
            </Popconfirm>
          </Space>
        ),
      },
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t],
  )

  const modelColumns: TableColumnsType<TeamGatewayModelItem> = useMemo(
    () => [
      { title: t('gateway.colModelName'), dataIndex: 'name' },
      {
        title: t('gateway.colModelId'),
        dataIndex: 'modelId',
        render: (v: string | null) => <Text code>{v || '-'}</Text>,
      },
      { title: t('gateway.colChannel'), dataIndex: 'channelName', width: 160 },
      {
        title: t('gateway.colQuota'),
        key: 'quota',
        width: 220,
        render: (_, record) => {
          if (!record.quota) return <Tag color="blue">{t('gateway.quotaUnlimited')}</Tag>
          const used = toNumber(record.quota.usedTokens)
          const limit = toNumber(record.quota.limitValue)
          const unitKey = PERIOD_UNIT_KEYS[record.quota.periodUnit ?? 0] ?? 'noReset'
          return (
            <Space size={spacing.xs} wrap>
              <span>
                {used.toLocaleString()} / {limit.toLocaleString()}
              </span>
              <Text type="secondary" style={{ fontSize: 12 }}>
                {t('gateway.periodPer', { period: t(`gateway.period.${unitKey}`, { count: record.quota.periodValue ?? 1, value: record.quota.periodValue ?? 1 }) })}
              </Text>
            </Space>
          )
        },
      },
      {
        title: t('gateway.colTotalUsed'),
        key: 'totalUsed',
        width: 140,
        render: (_, record) => toNumber(record.totalUsedTokens).toLocaleString(),
      },
    ],
    [t],
  )

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: spacing.lg }}>
      <Alert
        type="info"
        showIcon
        icon={<ThunderboltOutlined />}
        message={t('gateway.endpointTitle')}
        description={
          <div>
            <Paragraph style={{ marginBottom: spacing.xs }}>
              <Text type="secondary">Base URL: </Text>
              <Text code>{baseUrl}</Text>
              <Button type="text" size="small" icon={<CopyOutlined />} aria-label={t('common.copy')} onClick={() => void copyText(baseUrl)} />
            </Paragraph>
            <Text type="secondary" style={{ fontSize: 12 }}>
              {t('gateway.endpointHint')}
            </Text>
          </div>
        }
      />

      {canManage && (
        <DSCard title={t('gateway.keysTitle')} styles={{ body: { paddingTop: spacing.sm } }}>
          <DataTable<TeamApiKeyItem>
            sticky
            rowKey="id"
            columns={keyColumns}
            dataSource={keys}
            loading={keysLoading}
            onRefresh={() => void reloadKeys()}
            refreshLoading={keysLoading}
            toolbar={
              <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreateOpen(true)}>
                {t('gateway.createKey')}
              </Button>
            }
          />
        </DSCard>
      )}

      <DSCard title={t('gateway.modelsTitle')} styles={{ body: { paddingTop: spacing.sm } }}>
        <DataTable<TeamGatewayModelItem>
          sticky
          rowKey="aiModelId"
          columns={modelColumns}
          dataSource={models}
          loading={modelsLoading}
          onRefresh={() => void reloadModels()}
          refreshLoading={modelsLoading}
          scroll={{ y: 360 }}
        />
      </DSCard>

      <Modal
        title={t('gateway.createKey')}
        open={createOpen}
        onOk={() => void handleCreate()}
        confirmLoading={creating}
        onCancel={() => setCreateOpen(false)}
        maskClosable={false}
        destroyOnHidden
      >
        <Form form={createForm} layout="vertical">
          <Form.Item
            name="name"
            label={t('gateway.colKeyName')}
            rules={[
              { required: true, message: t('gateway.keyNamePlaceholder') },
              { max: 100, message: `${t('gateway.colKeyName')} ≤ 100` },
            ]}
          >
            <Input placeholder={t('gateway.keyNamePlaceholder')} maxLength={100} />
          </Form.Item>
          <Form.Item name="expireTime" label={t('gateway.colExpireTime')}>
            <DatePicker
              showTime
              style={{ width: '100%' }}
              disabledDate={(d) => d.isBefore(dayjs().startOf('day'))}
              placeholder={t('gateway.neverExpire')}
            />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        title={t('gateway.secretTitle')}
        open={createdSecret !== null}
        maskClosable={false}
        onCancel={() => setCreatedSecret(null)}
        footer={[
          <Button key="copy" icon={<CopyOutlined />} onClick={() => void copyText(createdSecret?.secret ?? '')}>
            {t('common.copy')}
          </Button>,
          <Button key="ok" type="primary" onClick={() => setCreatedSecret(null)}>
            {t('common.ok')}
          </Button>,
        ]}
      >
        <Alert type="warning" showIcon message={t('gateway.secretWarning')} style={{ marginBottom: spacing.md }} />
        <Paragraph code copyable={false} style={{ marginBottom: 0, wordBreak: 'break-all' }}>
          {createdSecret?.secret}
        </Paragraph>
      </Modal>
    </div>
  )
}
