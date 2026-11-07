import { useCallback, useEffect, useMemo, useState } from 'react'
import { Alert, Button, Form, Input, Modal, Popconfirm, Space, Tooltip, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import { CopyOutlined, DeleteOutlined, EditOutlined, EyeInvisibleOutlined, EyeOutlined, KeyOutlined, PlusOutlined, SearchOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { DataTable, feedback, PageToolbar } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { formatDateTime } from '@/utils/datetime'
import { NAME_PATTERN } from '@/utils/validation'
import {
  createAccessApp,
  deleteAccessApp,
  getAccessApps,
  resetAccessAppSecret,
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
        style={{ cursor: 'pointer', wordBreak: 'break-all' }}
        title={revealed ? t('accessApp.hideKey') : t('accessApp.revealKey')}
        onClick={() => setRevealed((v) => !v)}
      >
        {revealed ? value : masked}
      </Text>
      <Tooltip title={revealed ? t('accessApp.hideKey') : t('accessApp.revealKey')}>
        <Button
          type="text"
          size="small"
          icon={revealed ? <EyeInvisibleOutlined /> : <EyeOutlined />}
          aria-label={revealed ? t('accessApp.hideKey') : t('accessApp.revealKey')}
          onClick={() => setRevealed((v) => !v)}
        />
      </Tooltip>
      <Tooltip title={t('common.copy')}>
        <Button
          type="text"
          size="small"
          icon={<CopyOutlined />}
          aria-label={t('common.copy')}
          onClick={() => void copy()}
        />
      </Tooltip>
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
 * 团队「应用接入」区块：创建 moai-ac- key（团队级全量访问 /api/teamres，无需勾选范围）。
 * 接入密钥原文仅在创建/重置时返回一次；列表回显 appid 明文。
 * 应用/知识库/知识图谱各自的资源 key（moai-tr-）在对应资源开启对外功能时自动生成，
 * 在各资源的功能页（应用 ACP/A2A、知识库/知识图谱 MCP）查看与管理，不在此处。
 */
export function TeamAccessApps({ teamId, canManage }: TeamAccessAppsProps) {
  const { t } = useTranslation()
  const [items, setItems] = useState<AccessAppItem[]>([])
  const [loading, setLoading] = useState(true)
  const [keyword, setKeyword] = useState('')
  const [appliedKeyword, setAppliedKeyword] = useState('')
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<AccessAppItem | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [credentialShown, setCredentialShown] = useState<{ key?: string; secret?: string } | null>(null)
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

  /** 关键字筛选（查询按钮/回车触发，客户端过滤名称/描述） */
  const applySearch = useCallback(() => setAppliedKeyword(keyword.trim()), [keyword])

  const filteredItems = useMemo(() => {
    const kw = appliedKeyword.toLowerCase()
    if (!kw) return items
    return items.filter((item) => `${item.name ?? ''}\n${item.description ?? ''}`.toLowerCase().includes(kw))
  }, [items, appliedKeyword])

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
        if (res.key || res.secret) setCredentialShown({ key: res.key ?? undefined, secret: res.secret ?? undefined })
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

  const handleResetSecret = async (item: AccessAppItem) => {
    if (!item.accessAppId) return
    try {
      const secret = await resetAccessAppSecret(item.accessAppId)
      setCredentialShown({ secret: secret ?? undefined })
      feedback.success(t('accessApp.resetSecretSuccess'))
    } catch {
      // 错误已由全局请求中间件统一提示
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

  const columns: TableColumnsType<AccessAppItem> = useMemo(
    () => [
      {
        title: t('accessApp.colName'),
        dataIndex: 'name',
        ellipsis: true,
        render: (v: string | null) => (v ? v : '-'),
      },
      {
        title: t('accessApp.description'),
        dataIndex: 'description',
        ellipsis: true,
        render: (v: string | null) => (v ? v : '-'),
      },
      {
        title: t('accessApp.colKey'),
        dataIndex: 'key',
        width: 300,
        render: (v: string | null) => (v ? <KeyCell value={v} /> : '-'),
      },
      {
        title: t('accessApp.colCreateTime'),
        dataIndex: 'createTime',
        width: 170,
        render: (v: string | null) => (v ? formatDateTime(v) : '-'),
      },
      {
        title: t('accessApp.colLastUsed'),
        dataIndex: 'lastUsedTime',
        width: 170,
        render: (v: string | null) => (v ? formatDateTime(v) : '-'),
      },
      {
        title: t('accessApp.colActions'),
        key: 'actions',
        width: 240,
        fixed: 'right' as const,
        render: (_, record) => (
          <Space size={0}>
            <Button type="link" size="small" icon={<EditOutlined />} onClick={() => openEdit(record)}>
              {t('accessApp.edit')}
            </Button>
            <Popconfirm title={t('accessApp.resetSecretConfirm')} onConfirm={() => void handleResetSecret(record)}>
              <Button type="link" size="small" icon={<KeyOutlined />}>
                {t('accessApp.resetSecret')}
              </Button>
            </Popconfirm>
            <Popconfirm title={t('accessApp.deleteConfirm')} onConfirm={() => void handleDelete(record)}>
              <Button type="link" size="small" danger icon={<DeleteOutlined />}>
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
    <>
      <PageToolbar
        filters={
          <Input
            value={keyword}
            onChange={(e) => setKeyword(e.target.value)}
            onPressEnter={applySearch}
            allowClear
            maxLength={100}
            prefix={<SearchOutlined style={{ color: 'inherit' }} />}
            placeholder={t('accessApp.searchPlaceholder')}
            style={{ width: 240 }}
          />
        }
        queries={
          <Button loading={loading} onClick={applySearch}>
            {t('ds.query.search')}
          </Button>
        }
        actions={
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
            {t('accessApp.create')}
          </Button>
        }
      />

      <DataTable<AccessAppItem>
        sticky
        scroll={{ x: 'max-content' }}
        rowKey="accessAppId"
        columns={columns}
        dataSource={filteredItems}
        loading={loading}
      />

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
              { pattern: NAME_PATTERN, message: t('validation.nameChars') },
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
        open={credentialShown !== null}
        maskClosable={false}
        onCancel={() => setCredentialShown(null)}
        footer={[
          <Button key="ok" type="primary" onClick={() => setCredentialShown(null)}>
            {t('common.ok')}
          </Button>,
        ]}
      >
        <Alert type="warning" showIcon message={t('accessApp.keyWarning')} style={{ marginBottom: spacing.md }} />
        {credentialShown?.key && (
          <>
            <Text strong>{t('accessApp.appIdLabel')}</Text>
            <Paragraph code copyable style={{ wordBreak: 'break-all' }}>
              {credentialShown.key}
            </Paragraph>
          </>
        )}
        {credentialShown?.secret && (
          <>
            <Text strong>{t('accessApp.secretLabel')}</Text>
            <Paragraph code copyable style={{ marginBottom: 0, wordBreak: 'break-all' }}>
              {credentialShown.secret}
            </Paragraph>
          </>
        )}
      </Modal>
    </>
  )
}
