import { useCallback, useEffect, useMemo, useState } from 'react'
import { PlusOutlined } from '@ant-design/icons'
import { Alert, Button, Form, Input, Modal, Popconfirm, Space, Tag } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { Card as DSCard, DataTable, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { formatDateTime } from '@/utils/datetime'
import {
  createFeishuApp,
  deleteFeishuApp,
  getFeishuApps,
  updateFeishuApp,
  type FeishuAppItemView,
} from '@/api/feishuApp'

interface AppChannelsSectionProps {
  teamId: number
  appId: string
  canManage: boolean
  isPublished: boolean
}

interface CreateFormValues {
  name: string
  appId: string
  appSecret: string
  domain?: string
  description?: string
}

/**
 * 应用工作台「外部渠道」分区：把已发布内部 Agent 应用接入飞书（用户在飞书发消息即与本应用对话）。
 * 只提供新建（创建连接即绑定到当前应用，单次调用原子生效）、停用/启用、删除；删除即彻底移除连接，不保留可复用的连接。
 */
export function AppChannelsSection({ teamId, appId, canManage, isPublished }: AppChannelsSectionProps) {
  const { t } = useTranslation()
  const [form] = Form.useForm<CreateFormValues>()
  const [loading, setLoading] = useState(true)
  const [items, setItems] = useState<FeishuAppItemView[]>([])
  const [createOpen, setCreateOpen] = useState(false)
  const [creating, setCreating] = useState(false)
  const [actionKey, setActionKey] = useState<string>()

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const res = await getFeishuApps(teamId)
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

  // 同一飞书应用只能接入一个渠道，故接入本应用的连接 = bindChannelId 命中当前 appId
  const boundItems = useMemo(
    () => items.filter((x) => x.bindChannelType === 'app' && x.bindChannelId === appId),
    [items, appId],
  )

  const runAction = async (key: string, action: () => Promise<void>) => {
    setActionKey(key)
    try {
      await action()
      feedback.success(t('appChannels.operateSuccess'))
      await load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setActionKey(undefined)
    }
  }

  const handleCreate = async (values: CreateFormValues) => {
    setCreating(true)
    try {
      // 创建连接即绑定到当前应用，连接与绑定后端同一事务落库
      await createFeishuApp({
        teamId,
        name: values.name,
        description: values.description || undefined,
        appId: values.appId,
        appSecret: values.appSecret,
        domain: values.domain || undefined,
        channelType: 'app',
        channelId: appId,
      })
      feedback.success(t('appChannels.operateSuccess'))
      setCreateOpen(false)
      form.resetFields()
      await load()
    } catch {
      // 错误已由全局请求中间件统一提示（如 AppID 已建过连接返回 409）
    } finally {
      setCreating(false)
    }
  }

  const columns: TableColumnsType<FeishuAppItemView> = [
    {
      title: t('appChannels.columnName'),
      dataIndex: 'name',
      ellipsis: true,
    },
    {
      title: t('appChannels.columnAppId'),
      dataIndex: 'appId',
      ellipsis: true,
    },
    {
      title: t('appChannels.columnStatus'),
      dataIndex: 'status',
      width: 110,
      render: (_, record) => {
        if (record.isDisable) return <Tag color="red">{t('appChannels.statusDisabled')}</Tag>
        if (record.isOnline) return <Tag color="green">{t('appChannels.statusOnline')}</Tag>
        return <Tag>{t('appChannels.statusOffline')}</Tag>
      },
    },
    {
      title: t('appChannels.columnBindTime'),
      dataIndex: 'bindTime',
      width: 170,
      render: (value: string | null) => (value ? formatDateTime(value) : '-'),
    },
    {
      title: t('appChannels.columnCreator'),
      dataIndex: 'createUserName',
      width: 120,
      ellipsis: true,
      render: (value: string | null) => value || '-',
    },
  ]

  if (canManage) {
    columns.push({
      title: t('appChannels.columnActions'),
      key: 'actions',
      width: 150,
      render: (_, record) => (
        <Space size={spacing.sm}>
          {record.isDisable ? (
            <Button
              type="link"
              size="small"
              loading={actionKey === `enable:${record.feishuAppId}`}
              onClick={() => void runAction(`enable:${record.feishuAppId}`, () => updateFeishuApp(record.feishuAppId, { name: record.name, description: record.description ?? undefined, domain: record.domain ?? undefined, isDisable: false }))}
            >
              {t('appChannels.enable')}
            </Button>
          ) : (
            <Popconfirm
              title={t('appChannels.disableConfirm')}
              okText={t('appManage.confirm')}
              cancelText={t('appManage.cancel')}
              onConfirm={() => void runAction(`disable:${record.feishuAppId}`, () => updateFeishuApp(record.feishuAppId, { name: record.name, description: record.description ?? undefined, domain: record.domain ?? undefined, isDisable: true }))}
            >
              <Button type="link" size="small" loading={actionKey === `disable:${record.feishuAppId}`}>
                {t('appChannels.disable')}
              </Button>
            </Popconfirm>
          )}
          <Popconfirm
            title={t('appChannels.deleteConfirm')}
            okText={t('appManage.confirm')}
            cancelText={t('appManage.cancel')}
            onConfirm={() => void runAction(`delete:${record.feishuAppId}`, () => deleteFeishuApp(record.feishuAppId))}
          >
            <Button type="link" size="small" danger loading={actionKey === `delete:${record.feishuAppId}`}>
              {t('appChannels.delete')}
            </Button>
          </Popconfirm>
        </Space>
      ),
    })
  }

  return (
    <Space direction="vertical" style={{ width: '100%' }} size={spacing.md}>
      <Alert type="info" showIcon message={t('appChannels.hintTitle')} description={t('appChannels.hint')} />
      {!isPublished && <Alert type="warning" showIcon message={t('appChannels.unpublishedHint')} />}
      <DSCard title={t('appChannels.cardTitle')}>
        <DataTable
          rowKey="feishuAppId"
          columns={columns}
          dataSource={boundItems}
          loading={loading}
          onRefresh={load}
          refreshLoading={loading}
          pagination={false}
          toolbar={
            canManage ? (
              <Space>
                <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreateOpen(true)}>
                  {t('appChannels.addFeishu')}
                </Button>
              </Space>
            ) : undefined
          }
        />
      </DSCard>

      <Modal
        title={t('appChannels.createTitle')}
        open={createOpen}
        onCancel={() => setCreateOpen(false)}
        maskClosable={false}
        confirmLoading={creating}
        okText={t('appChannels.createOk')}
        cancelText={t('appManage.cancel')}
        onOk={() => form.submit()}
        destroyOnClose
      >
        <Form form={form} layout="vertical" onFinish={handleCreate} style={{ marginTop: spacing.md }}>
          <Form.Item
            name="name"
            label={t('appChannels.fieldName')}
            rules={[{ required: true, max: 50 }]}
          >
            <Input maxLength={50} placeholder={t('appChannels.fieldNamePlaceholder')} />
          </Form.Item>
          <Form.Item
            name="appId"
            label={t('appChannels.fieldAppId')}
            rules={[{ required: true, max: 64 }]}
          >
            <Input maxLength={64} placeholder={t('appChannels.fieldAppIdPlaceholder')} />
          </Form.Item>
          <Form.Item
            name="appSecret"
            label={t('appChannels.fieldAppSecret')}
            rules={[{ required: true, max: 128 }]}
          >
            <Input.Password maxLength={128} placeholder={t('appChannels.fieldAppSecretPlaceholder')} autoComplete="new-password" />
          </Form.Item>
          <Form.Item name="domain" label={t('appChannels.fieldDomain')} rules={[{ max: 100, type: 'url' }]}>
            <Input maxLength={100} placeholder={t('appChannels.fieldDomainPlaceholder')} />
          </Form.Item>
          <Form.Item name="description" label={t('appChannels.fieldDescription')} rules={[{ max: 255 }]}>
            <Input.TextArea rows={2} maxLength={255} />
          </Form.Item>
        </Form>
      </Modal>
    </Space>
  )
}
