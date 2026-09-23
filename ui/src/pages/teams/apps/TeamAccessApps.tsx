import { useCallback, useEffect, useMemo, useState } from 'react'
import { Alert, Button, Form, Input, Modal, Popconfirm, Space, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import { CopyOutlined, EyeInvisibleOutlined, EyeOutlined, PlusOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { Card as DSCard, DataTable, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { formatDateTime } from '@/utils/datetime'
import {
  createAccessApp,
  deleteAccessApp,
  getAccessApps,
  updateAccessApp,
  type AccessAppItem,
} from '@/api/access-app'

const { Text, Paragraph } = Typography

/** 接入 key 单元格：默认掩码，点击展开完整 key，支持复制 */
function KeyCell({ value }: { value: string }) {
  const { t } = useTranslation()
  const [revealed, setRevealed] = useState(false)
  const masked = value.length <= 12 ? value : value.slice(0, 12) + '****'

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(value)
      feedback.success(t('common.copySuccess'))
    } catch {
      feedback.error(t('common.copyFailed'))
    }
  }

  return (
    <Space size={0}>
      <Text
        code
        style={{ cursor: 'pointer' }}
        title={revealed ? t('accessApp.hideKey') : t('accessApp.revealKey')}
        onClick={() => setRevealed((v) => !v)}
      >
        {revealed ? value : masked}
      </Text>
      <Button
        type="text"
        size="small"
        icon={revealed ? <EyeInvisibleOutlined /> : <EyeOutlined />}
        aria-label={revealed ? t('accessApp.hideKey') : t('accessApp.revealKey')}
        onClick={() => setRevealed((v) => !v)}
      />
      <Button
        type="text"
        size="small"
        icon={<CopyOutlined />}
        aria-label={t('common.copy')}
        onClick={() => void copy()}
      />
    </Space>
  )
}

interface AccessAppFormValues {
  name: string
  description?: string
}

interface TeamAccessAppsProps {
  /** 所属团队 id */
  teamId: number
  /** 团队 Owner/Admin 才能管理应用接入 */
  canManage: boolean
}

/**
 * 团队「应用接入」区块：创建 key，应用 token 可访问其所属团队的资源（团队级授权）。
 * key 原文仅在创建时返回一次；列表只回显前缀。
 */
export function TeamAccessApps({ teamId, canManage }: TeamAccessAppsProps) {
  const { t } = useTranslation()
  const [items, setItems] = useState<AccessAppItem[]>([])
  const [loading, setLoading] = useState(true)
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<AccessAppItem | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [createdKey, setCreatedKey] = useState<string | null>(null)
  const [form] = Form.useForm<AccessAppFormValues>()

  const load = useCallback(async () => {
    if (!Number.isFinite(teamId) || teamId <= 0) return
    setLoading(true)
    try {
      const accessRes = await getAccessApps(teamId)
      setItems(accessRes.items ?? [])
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [teamId])

  useEffect(() => {
    if (!canManage) return
    void load()
  }, [canManage, load])

  const openCreate = () => {
    setEditing(null)
    form.resetFields()
    setModalOpen(true)
  }

  const openEdit = (item: AccessAppItem) => {
    setEditing(item)
    form.setFieldsValue({
      name: item.name ?? '',
      description: item.description ?? undefined,
    })
    setModalOpen(true)
  }

  const handleSubmit = async () => {
    const values = await form.validateFields()
    setSubmitting(true)
    try {
      if (editing?.accessAppId) {
        await updateAccessApp(editing.accessAppId, {
          name: values.name,
          description: values.description,
        })
        feedback.success(t('accessApp.updateSuccess'))
      } else {
        const res = await createAccessApp({
          teamId,
          name: values.name,
          description: values.description,
        })
        if (res.key) setCreatedKey(res.key)
        feedback.success(t('accessApp.createSuccess'))
      }
      setModalOpen(false)
      await load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSubmitting(false)
    }
  }

  const handleDelete = async (item: AccessAppItem) => {
    if (!item.accessAppId) return
    try {
      await deleteAccessApp(item.accessAppId)
      feedback.success(t('accessApp.deleteSuccess'))
      await load()
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

  const columns: TableColumnsType<AccessAppItem> = useMemo(
    () => [
      { title: t('accessApp.colName'), dataIndex: 'name' },
      {
        title: t('accessApp.colKey'),
        dataIndex: 'key',
        width: 260,
        render: (v: string | null) => (v ? <KeyCell value={v} /> : '-'),
      },
      {
        title: t('accessApp.colCreateTime'),
        dataIndex: 'createTime',
        width: 170,
        render: (v: string | null) => (v ? formatDateTime(v) : '-'),
      },
      {
        title: t('accessApp.colActions'),
        key: 'actions',
        width: 150,
        fixed: 'right' as const,
        render: (_, record) => (
          <Space size={0}>
            <Button type="link" size="small" onClick={() => openEdit(record)}>
              {t('accessApp.edit')}
            </Button>
            <Popconfirm title={t('accessApp.deleteConfirm')} onConfirm={() => void handleDelete(record)}>
              <Button type="link" size="small" danger>
                {t('accessApp.delete')}
              </Button>
            </Popconfirm>
          </Space>
        ),
      },
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t],
  )

  if (!canManage) {
    return <Text type="secondary">{t('accessApp.adminOnly')}</Text>
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: spacing.md }}>
      <Alert type="info" showIcon message={t('accessApp.usageHint')} />
      <DSCard styles={{ body: { paddingTop: spacing.sm } }}>
        <DataTable<AccessAppItem>
          sticky
          scroll={{ x: 'max-content' }}
          rowKey="accessAppId"
          columns={columns}
          dataSource={items}
          loading={loading}
          onRefresh={() => void load()}
          refreshLoading={loading}
          toolbar={
            <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
              {t('accessApp.create')}
            </Button>
          }
        />
      </DSCard>

      <Modal
        title={editing ? t('accessApp.editTitle') : t('accessApp.createTitle')}
        open={modalOpen}
        onOk={() => void handleSubmit()}
        confirmLoading={submitting}
        onCancel={() => setModalOpen(false)}
        destroyOnHidden
        maskClosable={false}
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="name"
            label={t('accessApp.colName')}
            rules={[
              { required: true, message: t('accessApp.namePlaceholder') },
              { max: 20, message: `${t('accessApp.colName')} ≤ 20` },
            ]}
          >
            <Input placeholder={t('accessApp.namePlaceholder')} maxLength={20} />
          </Form.Item>
          <Form.Item name="description" label={t('accessApp.description')} rules={[{ max: 255 }]}>
            <Input.TextArea placeholder={t('accessApp.descriptionPlaceholder')} maxLength={255} rows={3} />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        title={t('accessApp.keyTitle')}
        open={createdKey !== null}
        maskClosable={false}
        onCancel={() => setCreatedKey(null)}
        footer={[
          <Button key="copy" icon={<CopyOutlined />} onClick={() => void copyText(createdKey ?? '')}>
            {t('common.copy')}
          </Button>,
          <Button key="ok" type="primary" onClick={() => setCreatedKey(null)}>
            {t('common.ok')}
          </Button>,
        ]}
      >
        <Alert type="warning" showIcon message={t('accessApp.keyWarning')} style={{ marginBottom: spacing.md }} />
        <Paragraph code copyable={false} style={{ marginBottom: 0, wordBreak: 'break-all' }}>
          {createdKey}
        </Paragraph>
      </Modal>
    </div>
  )
}
