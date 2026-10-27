import { useCallback, useEffect, useMemo, useState } from 'react'
import { CopyOutlined, PlusOutlined } from '@ant-design/icons'
import { Alert, Button, Form, Input, InputNumber, Modal, Popconfirm, Space, Tag } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { Card as DSCard, DataTable, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { formatDateTime } from '@/utils/datetime'
import { AppChannelGuide } from './AppChannelGuide'
import {
  createWeixinWorkApp,
  deleteWeixinWorkApp,
  getWeixinWorkApps,
  updateWeixinWorkApp,
  type WeixinWorkAppItemView,
} from '@/api/weixinworkApp'

interface AppWeixinWorkChannelsSectionProps {
  teamId: number
  appId: string
  canManage: boolean
  isPublished: boolean
}

interface FormValues {
  name: string
  corpId: string
  corpSecret?: string
  agentId: number
  token?: string
  encodingAesKey?: string
  apiEndpoint?: string
  description?: string
}

/** 构造企业微信回调地址（配置到企业微信管理端 接收消息 → URL） */
function buildCallbackUrl(weixinWorkAppId: string): string {
  return `${window.location.origin}/api/weixinwork_app/${weixinWorkAppId}/callback`
}

/**
 * 应用工作台「外部渠道」企业微信分区：把已发布内部 Agent 应用接入企业微信自建应用
 * （用户在企微单聊应用或群里 @应用 发消息即与本应用对话）。
 * 只提供新建（创建连接即绑定到当前应用，单次调用原子生效）、编辑（名称/企业 ID/AgentId/Secret/回调配置，密钥留空保持不变）、
 * 停用/启用、删除；删除即彻底移除连接，不保留可复用的连接。
 */
export function AppWeixinWorkChannelsSection({ teamId, appId, canManage, isPublished }: AppWeixinWorkChannelsSectionProps) {
  const { t } = useTranslation()
  const [form] = Form.useForm<FormValues>()
  const [loading, setLoading] = useState(true)
  const [items, setItems] = useState<WeixinWorkAppItemView[]>([])
  const [createOpen, setCreateOpen] = useState(false)
  const [editing, setEditing] = useState<WeixinWorkAppItemView>()
  const [submitting, setSubmitting] = useState(false)
  const [actionKey, setActionKey] = useState<string>()

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const res = await getWeixinWorkApps(teamId)
      setItems(res.items)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [teamId])

  useEffect(() => {
    load()
  }, [load])

  // 同一企业微信应用只能接入一个渠道，故接入本应用的连接 = bindChannelId 命中当前 appId
  const boundItems = useMemo(
    () => items.filter((x) => x.bindChannelType === 'app' && x.bindChannelId === appId),
    [items, appId],
  )

  const runAction = async (key: string, action: () => Promise<void>) => {
    setActionKey(key)
    try {
      await action()
      feedback.success(t('appWeixinWorkChannels.operateSuccess'))
      await load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setActionKey(undefined)
    }
  }

  const openCreate = () => {
    setEditing(undefined)
    form.resetFields()
    setCreateOpen(true)
  }

  const openEdit = (record: WeixinWorkAppItemView) => {
    setEditing(record)
    form.resetFields()
    form.setFieldsValue({
      name: record.name,
      corpId: record.corpId,
      agentId: record.agentId,
      apiEndpoint: record.apiEndpoint || undefined,
      description: record.description || undefined,
      corpSecret: undefined,
      token: undefined,
      encodingAesKey: undefined,
    })
    setCreateOpen(true)
  }

  const copyCallbackUrl = async () => {
    if (!editing) return
    try {
      await navigator.clipboard.writeText(buildCallbackUrl(editing.weixinWorkAppId))
      feedback.success(t('common.copySuccess'))
    } catch {
      feedback.error(t('common.copyFailed'))
    }
  }

  const handleSubmit = async (values: FormValues) => {
    setSubmitting(true)
    try {
      if (editing) {
        // 密钥类留空表示保持不变；不改变当前启停状态
        await updateWeixinWorkApp(editing.weixinWorkAppId, {
          name: values.name,
          description: values.description || undefined,
          corpId: values.corpId,
          agentId: values.agentId,
          corpSecret: values.corpSecret || undefined,
          token: values.token || undefined,
          encodingAesKey: values.encodingAesKey || undefined,
          apiEndpoint: values.apiEndpoint || undefined,
          isDisable: editing.isDisable,
        })
      } else {
        // 创建连接即绑定到当前应用，连接与绑定后端同一事务落库
        await createWeixinWorkApp({
          teamId,
          name: values.name,
          description: values.description || undefined,
          corpId: values.corpId,
          corpSecret: values.corpSecret!,
          agentId: values.agentId,
          token: values.token!,
          encodingAesKey: values.encodingAesKey!,
          apiEndpoint: values.apiEndpoint || undefined,
          channelType: 'app',
          channelId: appId,
        })
      }
      feedback.success(t('appWeixinWorkChannels.operateSuccess'))
      setCreateOpen(false)
      form.resetFields()
      await load()
    } catch {
      // 错误已由全局请求中间件统一提示（如该企业微信应用已建过连接返回 409）
    } finally {
      setSubmitting(false)
    }
  }

  const columns: TableColumnsType<WeixinWorkAppItemView> = [
    {
      title: t('appWeixinWorkChannels.columnName'),
      dataIndex: 'name',
      ellipsis: true,
    },
    {
      title: t('appWeixinWorkChannels.columnCorpId'),
      dataIndex: 'corpId',
      ellipsis: true,
      render: (value: string, record) => `${value} / ${record.agentId}`,
    },
    {
      title: t('appWeixinWorkChannels.columnStatus'),
      dataIndex: 'status',
      width: 110,
      render: (_, record) => {
        if (record.isDisable) return <Tag color="red">{t('appWeixinWorkChannels.statusDisabled')}</Tag>
        if (record.isOnline) return <Tag color="green">{t('appWeixinWorkChannels.statusOnline')}</Tag>
        return <Tag>{t('appWeixinWorkChannels.statusOffline')}</Tag>
      },
    },
    {
      title: t('appWeixinWorkChannels.columnBindTime'),
      dataIndex: 'bindTime',
      width: 170,
      render: (value: string | null) => (value ? formatDateTime(value) : '-'),
    },
    {
      title: t('appWeixinWorkChannels.columnCreator'),
      dataIndex: 'createUserName',
      width: 120,
      ellipsis: true,
      render: (value: string | null) => value || '-',
    },
  ]

  if (canManage) {
    columns.push({
      title: t('appWeixinWorkChannels.columnActions'),
      key: 'actions',
      width: 190,
      render: (_, record) => (
        <Space size={spacing.sm}>
          <Button type="link" size="small" onClick={() => openEdit(record)}>
            {t('appWeixinWorkChannels.edit')}
          </Button>
          {record.isDisable ? (
            <Button
              type="link"
              size="small"
              loading={actionKey === `enable:${record.weixinWorkAppId}`}
              onClick={() => void runAction(`enable:${record.weixinWorkAppId}`, () => updateWeixinWorkApp(record.weixinWorkAppId, { name: record.name, description: record.description ?? undefined, corpId: record.corpId, agentId: record.agentId, isDisable: false }))}
            >
              {t('appWeixinWorkChannels.enable')}
            </Button>
          ) : (
            <Popconfirm
              title={t('appWeixinWorkChannels.disableConfirm')}
              okText={t('appManage.confirm')}
              cancelText={t('appManage.cancel')}
              onConfirm={() => void runAction(`disable:${record.weixinWorkAppId}`, () => updateWeixinWorkApp(record.weixinWorkAppId, { name: record.name, description: record.description ?? undefined, corpId: record.corpId, agentId: record.agentId, isDisable: true }))}
            >
              <Button type="link" size="small" loading={actionKey === `disable:${record.weixinWorkAppId}`}>
                {t('appWeixinWorkChannels.disable')}
              </Button>
            </Popconfirm>
          )}
          <Popconfirm
            title={t('appWeixinWorkChannels.deleteConfirm')}
            okText={t('appManage.confirm')}
            cancelText={t('appManage.cancel')}
            onConfirm={() => void runAction(`delete:${record.weixinWorkAppId}`, () => deleteWeixinWorkApp(record.weixinWorkAppId))}
          >
            <Button type="link" size="small" danger loading={actionKey === `delete:${record.weixinWorkAppId}`}>
              {t('appWeixinWorkChannels.delete')}
            </Button>
          </Popconfirm>
        </Space>
      ),
    })
  }

  // 接入指引默认收缩，点击展开后按步骤逐条分行展示
  const guidePanel = <AppChannelGuide ns="appWeixinWorkChannels" />

  const refreshButton = (
    <Button onClick={load} loading={loading}>
      {t('ds.table.refresh')}
    </Button>
  )

  return (
    <Space direction="vertical" style={{ width: '100%' }} size={spacing.md}>
      {!isPublished && <Alert type="warning" showIcon message={t('appWeixinWorkChannels.unpublishedHint')} />}
      <DSCard
        title={t('appWeixinWorkChannels.cardTitle')}
        extra={
          <Space>
            {canManage && (
              <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
                {t('appWeixinWorkChannels.addWeixinWork')}
              </Button>
            )}
            {refreshButton}
          </Space>
        }
      >
        {guidePanel}
        <DataTable
          rowKey="weixinWorkAppId"
          columns={columns}
          dataSource={boundItems}
          loading={loading}
          pagination={false}
        />

        <Modal
          title={editing ? t('appWeixinWorkChannels.editTitle') : t('appWeixinWorkChannels.createTitle')}
          open={createOpen}
          onCancel={() => setCreateOpen(false)}
          maskClosable={false}
          confirmLoading={submitting}
          okText={editing ? t('appWeixinWorkChannels.editOk') : t('appWeixinWorkChannels.createOk')}
          cancelText={t('appManage.cancel')}
          onOk={() => form.submit()}
          destroyOnClose
        >
          <Form form={form} layout="vertical" onFinish={handleSubmit} style={{ marginTop: spacing.md }}>
            {editing && (
              <Form.Item label={t('appWeixinWorkChannels.fieldCallbackUrl')}>
                <Space.Compact style={{ width: '100%' }}>
                  <Input readOnly value={buildCallbackUrl(editing.weixinWorkAppId)} />
                  <Button icon={<CopyOutlined />} onClick={() => void copyCallbackUrl()} aria-label={t('common.copy')} />
                </Space.Compact>
              </Form.Item>
            )}
            <Form.Item
              name="name"
              label={t('appWeixinWorkChannels.fieldName')}
              rules={[{ required: true, max: 50 }]}
            >
              <Input maxLength={50} placeholder={t('appWeixinWorkChannels.fieldNamePlaceholder')} />
            </Form.Item>
            <Form.Item
              name="corpId"
              label={t('appWeixinWorkChannels.fieldCorpId')}
              rules={[{ required: true, max: 64 }]}
            >
              <Input maxLength={64} placeholder={t('appWeixinWorkChannels.fieldCorpIdPlaceholder')} />
            </Form.Item>
            <Form.Item
              name="agentId"
              label={t('appWeixinWorkChannels.fieldAgentId')}
              rules={[{ required: true }]}
            >
              <InputNumber min={1} precision={0} style={{ width: '100%' }} placeholder={t('appWeixinWorkChannels.fieldAgentIdPlaceholder')} />
            </Form.Item>
            <Form.Item
              name="corpSecret"
              label={t('appWeixinWorkChannels.fieldCorpSecret')}
              rules={[{ required: !editing, max: 128 }]}
            >
              <Input.Password maxLength={128} placeholder={editing ? t('appWeixinWorkChannels.fieldCorpSecretKeepPlaceholder') : t('appWeixinWorkChannels.fieldCorpSecretPlaceholder')} autoComplete="new-password" />
            </Form.Item>
            <Form.Item
              name="token"
              label={t('appWeixinWorkChannels.fieldToken')}
              rules={[{ required: !editing, max: 128 }]}
            >
              <Input.Password maxLength={128} placeholder={editing ? t('appWeixinWorkChannels.fieldTokenKeepPlaceholder') : t('appWeixinWorkChannels.fieldTokenPlaceholder')} autoComplete="new-password" />
            </Form.Item>
            <Form.Item
              name="encodingAesKey"
              label={t('appWeixinWorkChannels.fieldEncodingAesKey')}
              rules={[{ required: !editing, len: 43 }]}
            >
              <Input.Password maxLength={43} placeholder={editing ? t('appWeixinWorkChannels.fieldAesKeyKeepPlaceholder') : t('appWeixinWorkChannels.fieldEncodingAesKeyPlaceholder')} autoComplete="new-password" />
            </Form.Item>
            <Form.Item name="apiEndpoint" label={t('appWeixinWorkChannels.fieldApiEndpoint')} rules={[{ type: 'url', max: 200 }]}>
              <Input maxLength={200} placeholder={t('appWeixinWorkChannels.fieldApiEndpointPlaceholder')} />
            </Form.Item>
            <Form.Item name="description" label={t('appWeixinWorkChannels.fieldDescription')} rules={[{ max: 255 }]}>
              <Input.TextArea rows={2} maxLength={255} />
            </Form.Item>
          </Form>
        </Modal>
      </DSCard>
    </Space>
  )
}
