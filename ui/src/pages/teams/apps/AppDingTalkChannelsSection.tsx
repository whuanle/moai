import { useCallback, useEffect, useMemo, useState } from 'react'
import { PlusOutlined } from '@ant-design/icons'
import { Alert, Button, Form, Input, Modal, Popconfirm, Space, Tag } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { Card as DSCard, DataTable, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { formatDateTime } from '@/utils/datetime'
import {
  createDingTalkApp,
  deleteDingTalkApp,
  getDingTalkApps,
  updateDingTalkApp,
  type DingTalkAppItemView,
} from '@/api/dingtalkApp'

interface AppDingTalkChannelsSectionProps {
  teamId: number
  appId: string
  canManage: boolean
  isPublished: boolean
}

interface FormValues {
  name: string
  appKey: string
  appSecret?: string
  description?: string
}

/**
 * 应用工作台「外部渠道」钉钉分区：把已发布内部 Agent 应用接入钉钉机器人（用户在钉钉单聊/群聊 @机器人发消息即与本应用对话）。
 * 只提供新建（创建连接即绑定到当前应用，单次调用原子生效）、编辑（名称/AppKey/密钥/描述，密钥留空保持不变）、停用/启用、删除；
 * 删除即彻底移除连接，不保留可复用的连接。
 */
export function AppDingTalkChannelsSection({ teamId, appId, canManage, isPublished }: AppDingTalkChannelsSectionProps) {
  const { t } = useTranslation()
  const [form] = Form.useForm<FormValues>()
  const [loading, setLoading] = useState(true)
  const [items, setItems] = useState<DingTalkAppItemView[]>([])
  const [createOpen, setCreateOpen] = useState(false)
  const [editing, setEditing] = useState<DingTalkAppItemView>()
  const [submitting, setSubmitting] = useState(false)
  const [actionKey, setActionKey] = useState<string>()

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const res = await getDingTalkApps(teamId)
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

  // 同一钉钉应用只能接入一个渠道，故接入本应用的连接 = bindChannelId 命中当前 appId
  const boundItems = useMemo(
    () => items.filter((x) => x.bindChannelType === 'app' && x.bindChannelId === appId),
    [items, appId],
  )

  const runAction = async (key: string, action: () => Promise<void>) => {
    setActionKey(key)
    try {
      await action()
      feedback.success(t('appDingTalkChannels.operateSuccess'))
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

  const openEdit = (record: DingTalkAppItemView) => {
    setEditing(record)
    form.resetFields()
    form.setFieldsValue({
      name: record.name,
      appKey: record.appKey,
      description: record.description || undefined,
      appSecret: undefined,
    })
    setCreateOpen(true)
  }

  const handleSubmit = async (values: FormValues) => {
    setSubmitting(true)
    try {
      if (editing) {
        // 密钥留空表示保持不变；不改变当前启停状态
        await updateDingTalkApp(editing.dingTalkAppId, {
          name: values.name,
          description: values.description || undefined,
          appKey: values.appKey || undefined,
          appSecret: values.appSecret || undefined,
          isDisable: editing.isDisable,
        })
      } else {
        // 创建连接即绑定到当前应用，连接与绑定后端同一事务落库
        await createDingTalkApp({
          teamId,
          name: values.name,
          description: values.description || undefined,
          appKey: values.appKey,
          appSecret: values.appSecret!,
          channelType: 'app',
          channelId: appId,
        })
      }
      feedback.success(t('appDingTalkChannels.operateSuccess'))
      setCreateOpen(false)
      form.resetFields()
      await load()
    } catch {
      // 错误已由全局请求中间件统一提示（如 AppKey 已建过连接返回 409）
    } finally {
      setSubmitting(false)
    }
  }

  const columns: TableColumnsType<DingTalkAppItemView> = [
    {
      title: t('appDingTalkChannels.columnName'),
      dataIndex: 'name',
      ellipsis: true,
    },
    {
      title: t('appDingTalkChannels.columnAppKey'),
      dataIndex: 'appKey',
      ellipsis: true,
    },
    {
      title: t('appDingTalkChannels.columnStatus'),
      dataIndex: 'status',
      width: 110,
      render: (_, record) => {
        if (record.isDisable) return <Tag color="red">{t('appDingTalkChannels.statusDisabled')}</Tag>
        if (record.isOnline) return <Tag color="green">{t('appDingTalkChannels.statusOnline')}</Tag>
        return <Tag>{t('appDingTalkChannels.statusOffline')}</Tag>
      },
    },
    {
      title: t('appDingTalkChannels.columnBindTime'),
      dataIndex: 'bindTime',
      width: 170,
      render: (value: string | null) => (value ? formatDateTime(value) : '-'),
    },
    {
      title: t('appDingTalkChannels.columnCreator'),
      dataIndex: 'createUserName',
      width: 120,
      ellipsis: true,
      render: (value: string | null) => value || '-',
    },
  ]

  if (canManage) {
    columns.push({
      title: t('appDingTalkChannels.columnActions'),
      key: 'actions',
      width: 190,
      render: (_, record) => (
        <Space size={spacing.sm}>
          <Button type="link" size="small" onClick={() => openEdit(record)}>
            {t('appDingTalkChannels.edit')}
          </Button>
          {record.isDisable ? (
            <Button
              type="link"
              size="small"
              loading={actionKey === `enable:${record.dingTalkAppId}`}
              onClick={() => void runAction(`enable:${record.dingTalkAppId}`, () => updateDingTalkApp(record.dingTalkAppId, { name: record.name, description: record.description ?? undefined, isDisable: false }))}
            >
              {t('appDingTalkChannels.enable')}
            </Button>
          ) : (
            <Popconfirm
              title={t('appDingTalkChannels.disableConfirm')}
              okText={t('appManage.confirm')}
              cancelText={t('appManage.cancel')}
              onConfirm={() => void runAction(`disable:${record.dingTalkAppId}`, () => updateDingTalkApp(record.dingTalkAppId, { name: record.name, description: record.description ?? undefined, isDisable: true }))}
            >
              <Button type="link" size="small" loading={actionKey === `disable:${record.dingTalkAppId}`}>
                {t('appDingTalkChannels.disable')}
              </Button>
            </Popconfirm>
          )}
          <Popconfirm
            title={t('appDingTalkChannels.deleteConfirm')}
            okText={t('appManage.confirm')}
            cancelText={t('appManage.cancel')}
            onConfirm={() => void runAction(`delete:${record.dingTalkAppId}`, () => deleteDingTalkApp(record.dingTalkAppId))}
          >
            <Button type="link" size="small" danger loading={actionKey === `delete:${record.dingTalkAppId}`}>
              {t('appDingTalkChannels.delete')}
            </Button>
          </Popconfirm>
        </Space>
      ),
    })
  }

  return (
    <Space direction="vertical" style={{ width: '100%' }} size={spacing.md}>
      <Alert type="info" showIcon message={t('appDingTalkChannels.hintTitle')} description={t('appDingTalkChannels.hint')} />
      {!isPublished && <Alert type="warning" showIcon message={t('appDingTalkChannels.unpublishedHint')} />}
      <DSCard title={t('appDingTalkChannels.cardTitle')}>
        <DataTable
          rowKey="dingTalkAppId"
          columns={columns}
          dataSource={boundItems}
          loading={loading}
          onRefresh={load}
          refreshLoading={loading}
          pagination={false}
          toolbar={
            canManage ? (
              <Space>
                <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
                  {t('appDingTalkChannels.addDingTalk')}
                </Button>
              </Space>
            ) : undefined
          }
        />

        <Modal
          title={editing ? t('appDingTalkChannels.editTitle') : t('appDingTalkChannels.createTitle')}
          open={createOpen}
          onCancel={() => setCreateOpen(false)}
          maskClosable={false}
          confirmLoading={submitting}
          okText={editing ? t('appDingTalkChannels.editOk') : t('appDingTalkChannels.createOk')}
          cancelText={t('appManage.cancel')}
          onOk={() => form.submit()}
          destroyOnClose
        >
          <Form form={form} layout="vertical" onFinish={handleSubmit} style={{ marginTop: spacing.md }}>
            <Form.Item
              name="name"
              label={t('appDingTalkChannels.fieldName')}
              rules={[{ required: true, max: 50 }]}
            >
              <Input maxLength={50} placeholder={t('appDingTalkChannels.fieldNamePlaceholder')} />
            </Form.Item>
            <Form.Item
              name="appKey"
              label={t('appDingTalkChannels.fieldAppKey')}
              rules={[{ required: true, max: 64 }]}
            >
              <Input maxLength={64} placeholder={t('appDingTalkChannels.fieldAppKeyPlaceholder')} />
            </Form.Item>
            <Form.Item
              name="appSecret"
              label={t('appDingTalkChannels.fieldAppSecret')}
              rules={[{ required: !editing, max: 128 }]}
            >
              <Input.Password maxLength={128} placeholder={editing ? t('appDingTalkChannels.fieldAppSecretKeepPlaceholder') : t('appDingTalkChannels.fieldAppSecretPlaceholder')} autoComplete="new-password" />
            </Form.Item>
            <Form.Item name="description" label={t('appDingTalkChannels.fieldDescription')} rules={[{ max: 255 }]}>
              <Input.TextArea rows={2} maxLength={255} />
            </Form.Item>
          </Form>
        </Modal>
      </DSCard>
    </Space>
  )
}
