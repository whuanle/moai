import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  CheckCircleOutlined,
  CopyOutlined,
  DeleteOutlined,
  EditOutlined,
  KeyOutlined,
  PlusOutlined,
  ReloadOutlined,
  SearchOutlined,
  StopOutlined,
} from '@ant-design/icons'
import { Alert, Button, Form, Input, Modal, Popconfirm, Radio, Space, Tag, Tooltip, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { AuditCell, DataTable, PageToolbar, feedback } from '@/design-system'
import { controlHeight, spacing, tableCardChromeHeight } from '@/design-system/theme'
import {
  createSystemAccessApp,
  deleteSystemAccessApp,
  getSystemAccessApps,
  resetSystemAccessAppSecret,
  updateSystemAccessApp,
  type SystemAccessAppItem,
} from '@/api/systemAccessApp'

const { Text, Paragraph } = Typography

const pageVerticalPadding = spacing.lg * 2
const adminTabsBarHeight = 46
const toolbarHeight = controlHeight + spacing.md
const tableHeaderHeight = 55
const tablePaginationHeight = 57
const panelHeight = `calc(100vh - ${pageVerticalPadding + adminTabsBarHeight}px)`
const tableScrollY = `calc(100vh - ${pageVerticalPadding + adminTabsBarHeight + toolbarHeight + tableHeaderHeight + tablePaginationHeight + tableCardChromeHeight}px)`

interface AccessAppFormValues {
  name: string
  description?: string
  scopes: string[]
}

/** 按 读/写 两位范围代码推导三档档位 */
type ResourceLevel = 'none' | 'read' | 'write'

/**
 * 系统资源范围分组编辑器：组织架构、用户治理各自 无/只读/可写 三档，value 即后端 scopes 代码列表（Form 受控）.
 */
function ScopeGroupEditor({ value = [], onChange }: { value?: string[]; onChange?: (v: string[]) => void }) {
  const { t } = useTranslation()
  const has = (code: string) => value.includes(code)
  const levelOf = (readCode: string, writeCode: string): ResourceLevel => (has(writeCode) ? 'write' : has(readCode) ? 'read' : 'none')
  const setLevel = (readCode: string, writeCode: string, level: ResourceLevel) =>
    onChange?.([
      ...value.filter((c) => c !== readCode && c !== writeCode),
      ...(level === 'read' ? [readCode] : level === 'write' ? [readCode, writeCode] : []),
    ])

  const resourceGroups: Array<{ label: string; read: string; write: string }> = [
    { label: t('systemAccessApp.groupOrg'), read: 'org_read', write: 'org_write' },
    { label: t('systemAccessApp.groupUser'), read: 'user_read', write: 'user_write' },
    { label: t('systemAccessApp.groupTeam'), read: 'team_read', write: 'team_write' },
  ]

  return (
    <Space direction="vertical" size={spacing.sm} style={{ display: 'flex' }}>
      {resourceGroups.map((group) => (
        <div key={group.read}>
          <Text strong style={{ fontSize: 13 }}>{group.label}</Text>
          <Radio.Group
            value={levelOf(group.read, group.write)}
            onChange={(e) => setLevel(group.read, group.write, e.target.value as ResourceLevel)}
            style={{ marginLeft: spacing.sm }}
          >
            <Radio value="none">{t('systemAccessApp.levelNone')}</Radio>
            <Radio value="read">{t('systemAccessApp.levelRead')}</Radio>
            <Radio value="write">{t('systemAccessApp.levelWrite')}</Radio>
          </Radio.Group>
        </div>
      ))}
    </Space>
  )
}

/**
 * 应用接入面板（系统级，仅 root，由 AdminIntegrations Tab 容器承载）：
 * 创建系统接入 key，内部系统（如 ERP）凭 key 经 /api/subsys/system/* 维护系统资源（组织/用户）。
 * key 原文仅在创建时显示一次；列表只回显前缀。
 */
export function SystemAccessAppsPanel() {
  const { t } = useTranslation()
  const [loading, setLoading] = useState(true)
  const [items, setItems] = useState<SystemAccessAppItem[]>([])
  const [keyword, setKeyword] = useState('')

  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<SystemAccessAppItem | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [credentialShown, setCredentialShown] = useState<{ key?: string; secret?: string } | null>(null)
  const [form] = Form.useForm<AccessAppFormValues>()

  const load = useCallback(async (search = keyword) => {
    setLoading(true)
    try {
      const list = await getSystemAccessApps(search)
      setItems(list)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [keyword])

  useEffect(() => {
    void load()
    // 首次进入按初始条件拉取，后续由交互触发
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const openCreate = () => {
    setEditing(null)
    form.resetFields()
    form.setFieldsValue({ scopes: ['org_read'] })
    setModalOpen(true)
  }

  const openEdit = (item: SystemAccessAppItem) => {
    setEditing(item)
    form.setFieldsValue({
      name: item.name ?? '',
      description: item.description ?? undefined,
      scopes: item.scopes ?? [],
    })
    setModalOpen(true)
  }

  const handleSubmit = async () => {
    const values = await form.validateFields()
    setSubmitting(true)
    try {
      if (editing?.id) {
        await updateSystemAccessApp(editing.id, {
          name: values.name,
          description: values.description,
          scopes: values.scopes ?? [],
          status: editing.status === 'disabled' ? 'disabled' : 'enabled',
        })
        feedback.success(t('systemAccessApp.updateSuccess'))
      } else {
        const res = await createSystemAccessApp({
          name: values.name,
          description: values.description,
          scopes: values.scopes ?? [],
        })
        if (res.key || res.secret) setCredentialShown({ key: res.key ?? undefined, secret: res.secret ?? undefined })
        feedback.success(t('systemAccessApp.createSuccess'))
      }
      setModalOpen(false)
      await load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSubmitting(false)
    }
  }

  const handleToggleStatus = async (item: SystemAccessAppItem) => {
    if (!item.id) return
    try {
      await updateSystemAccessApp(item.id, {
        name: item.name ?? '',
        description: item.description ?? undefined,
        scopes: item.scopes ?? [],
        status: item.status === 'disabled' ? 'enabled' : 'disabled',
      })
      feedback.success(t(item.status === 'disabled' ? 'systemAccessApp.enableSuccess' : 'systemAccessApp.disableSuccess'))
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleResetSecret = async (item: SystemAccessAppItem) => {
    if (!item.id) return
    try {
      const secret = await resetSystemAccessAppSecret(item.id)
      setCredentialShown({ secret: secret ?? undefined })
      feedback.success(t('systemAccessApp.resetSecretSuccess'))
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleDelete = async (item: SystemAccessAppItem) => {
    if (!item.id) return
    try {
      await deleteSystemAccessApp(item.id)
      feedback.success(t('systemAccessApp.deleteSuccess'))
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const copyKeyPrefix = async (value: string) => {
    try {
      await navigator.clipboard.writeText(value)
      feedback.success(t('common.copySuccess'))
    } catch {
      feedback.error(t('common.copyFailed'))
    }
  }

  const columns: TableColumnsType<SystemAccessAppItem> = useMemo(
    () => [
      {
        title: t('systemAccessApp.colName'),
        key: 'name',
        width: 200,
        render: (_, record) => (
          <div style={{ lineHeight: 1.4, minWidth: 0 }}>
            <div>{record.name || '-'}</div>
            {record.description && (
              <Text type="secondary" style={{ fontSize: 12 }}>
                {record.description}
              </Text>
            )}
          </div>
        ),
      },
      {
        title: t('systemAccessApp.colKeyPrefix'),
        dataIndex: 'keyPrefix',
        key: 'keyPrefix',
        width: 180,
        render: (v: string | null) => (
          <Space size={0}>
            <Text code>{v || '-'}</Text>
            <Tooltip title={t('common.copy')}>
              <Button type="text" size="small" icon={<CopyOutlined />} aria-label={t('common.copy')} onClick={() => void copyKeyPrefix(v ?? '')} />
            </Tooltip>
          </Space>
        ),
      },
      {
        title: t('systemAccessApp.colScopes'),
        key: 'scopes',
        width: 240,
        render: (_, record) => (
          <Space size={[spacing.xs, spacing.xs]} wrap>
            {(record.scopes ?? []).map((code) => (
              <Tag key={code}>{t(`systemAccessApp.scope.${code}`)}</Tag>
            ))}
          </Space>
        ),
      },
      {
        title: t('systemAccessApp.colStatus'),
        key: 'status',
        width: 90,
        render: (_, record) =>
          record.status === 'disabled' ? (
            <Tag color="error">{t('systemAccessApp.statusDisabled')}</Tag>
          ) : (
            <Tag color="success">{t('systemAccessApp.statusEnabled')}</Tag>
          ),
      },
      {
        title: t('systemAccessApp.colCreate'),
        key: 'create',
        width: 150,
        render: (_, record) => <AuditCell user={record.createUserName} time={record.createTime} />,
      },
      {
        title: t('systemAccessApp.colUpdate'),
        key: 'update',
        width: 150,
        render: (_, record) => <AuditCell user={record.updateUserName} time={record.updateTime} />,
      },
      {
        title: t('systemAccessApp.colActions'),
        key: 'actions',
        width: 320,
        fixed: 'right',
        render: (_, record) => (
          <Space size={0}>
            <Button type="link" size="small" icon={<EditOutlined />} onClick={() => openEdit(record)}>
              {t('systemAccessApp.edit')}
            </Button>
            <Popconfirm title={t('systemAccessApp.resetSecretConfirm')} onConfirm={() => void handleResetSecret(record)}>
              <Button type="link" size="small" icon={<KeyOutlined />}>
                {t('systemAccessApp.resetSecret')}
              </Button>
            </Popconfirm>
            <Popconfirm
              title={t(record.status === 'disabled' ? 'systemAccessApp.enableConfirm' : 'systemAccessApp.disableConfirm')}
              okButtonProps={{ danger: record.status !== 'disabled' }}
              onConfirm={() => void handleToggleStatus(record)}
            >
              <Button
                type="link"
                size="small"
                danger={record.status !== 'disabled'}
                icon={record.status === 'disabled' ? <CheckCircleOutlined /> : <StopOutlined />}
              >
                {t(record.status === 'disabled' ? 'systemAccessApp.enable' : 'systemAccessApp.disable')}
              </Button>
            </Popconfirm>
            <Popconfirm title={t('systemAccessApp.deleteConfirm')} okButtonProps={{ danger: true }} onConfirm={() => void handleDelete(record)}>
              <Button type="link" size="small" danger icon={<DeleteOutlined />}>
                {t('systemAccessApp.delete')}
              </Button>
            </Popconfirm>
          </Space>
        ),
      },
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t],
  )

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: panelHeight, minHeight: 0 }}>
      <PageToolbar
        filters={
          <Space size={spacing.xs} wrap>
            <Input
              value={keyword}
              onChange={(e) => setKeyword(e.target.value)}
              onPressEnter={() => void load()}
              allowClear
              maxLength={50}
              prefix={<SearchOutlined style={{ color: 'inherit' }} />}
              placeholder={t('systemAccessApp.searchPlaceholder')}
              style={{ width: 260 }}
            />
            <Button type="primary" icon={<SearchOutlined />} onClick={() => void load()}>
              {t('systemAccessApp.search')}
            </Button>
          </Space>
        }
        actions={
          <>
            <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
              {t('systemAccessApp.create')}
            </Button>
            <Tooltip title={t('systemAccessApp.refresh')}>
              <Button icon={<ReloadOutlined />} aria-label={t('systemAccessApp.refresh')} onClick={() => void load()} />
            </Tooltip>
          </>
        }
      />
      <div style={{ flex: 1, minHeight: 0 }}>
        <DataTable<SystemAccessAppItem>
          rowKey="id"
          columns={columns}
          dataSource={items}
          loading={loading}
          sticky
          scroll={{ x: 1330, y: tableScrollY }}
          pagination={false}
        />
      </div>

      <Modal
        open={modalOpen}
        title={t(editing ? 'systemAccessApp.editTitle' : 'systemAccessApp.createTitle')}
        onCancel={() => setModalOpen(false)}
        onOk={() => void handleSubmit()}
        confirmLoading={submitting}
        destroyOnHidden
        maskClosable={false}
      >
        <Form<AccessAppFormValues> form={form} layout="vertical" autoComplete="off">
          <Form.Item
            name="name"
            label={t('systemAccessApp.name')}
            rules={[{ required: true, message: t('systemAccessApp.nameRequired') }, { max: 40, message: t('systemAccessApp.nameMax') }]}
          >
            <Input maxLength={40} placeholder={t('systemAccessApp.namePlaceholder')} />
          </Form.Item>
          <Form.Item name="description" label={t('systemAccessApp.description')}>
            <Input.TextArea rows={2} maxLength={255} placeholder={t('systemAccessApp.descriptionPlaceholder')} />
          </Form.Item>
          <Form.Item
            name="scopes"
            label={t('systemAccessApp.scopes')}
            rules={[{ required: true, message: t('systemAccessApp.scopesRequired') }]}
          >
            <ScopeGroupEditor />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        open={credentialShown !== null}
        title={t('systemAccessApp.keyOnceTitle')}
        onCancel={() => setCredentialShown(null)}
        footer={<Button type="primary" onClick={() => setCredentialShown(null)}>{t('systemAccessApp.keyOnceSaved')}</Button>}
        destroyOnHidden
        maskClosable={false}
      >
        <Alert type="warning" showIcon message={t('systemAccessApp.keyOnceHint')} style={{ marginBottom: spacing.md }} />
        {credentialShown?.key && (
          <>
            <Text strong>{t('systemAccessApp.appIdLabel')}</Text>
            <Paragraph code copyable style={{ wordBreak: 'break-all' }}>
              {credentialShown.key}
            </Paragraph>
          </>
        )}
        {credentialShown?.secret && (
          <>
            <Text strong>{t('systemAccessApp.secretLabel')}</Text>
            <Paragraph code copyable style={{ marginBottom: 0, wordBreak: 'break-all' }}>
              {credentialShown.secret}
            </Paragraph>
          </>
        )}
      </Modal>
    </div>
  )
}
