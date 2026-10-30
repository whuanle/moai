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
import { Alert, Button, Form, Input, Modal, Popconfirm, Radio, Select, Space, Tag, Tooltip, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { DataTable, PageToolbar, feedback } from '@/design-system'
import { controlHeight, spacing, tableCardChromeHeight } from '@/design-system/theme'
import { formatDateTime } from '@/utils/datetime'
import { getAdminTeams } from '@/api/team'
import {
  createSubsystem,
  deleteSubsystem,
  getSubsystemAuthorizedTeams,
  getSubsystems,
  resetSubsystemSecret,
  updateSubsystem,
  type SubsystemItem,
  type SubsystemStatus,
  type SubsystemTeamScopeType,
} from '@/api/subsystem'

const { Text, Paragraph } = Typography

const pageVerticalPadding = spacing.lg * 2
const toolbarHeight = controlHeight + spacing.md
const tableHeaderHeight = 55
const tablePaginationHeight = 57
const adminTabsBarHeight = 46
const adminSubsystemsPageHeight = `calc(100vh - ${pageVerticalPadding + adminTabsBarHeight}px)`
const adminSubsystemsTableScrollY = `calc(100vh - ${pageVerticalPadding + toolbarHeight + tableHeaderHeight + tablePaginationHeight + tableCardChromeHeight}px)`

interface SubsystemFormValues {
  key: string
  name: string
  description?: string
  status: SubsystemStatus
  teamScope: SubsystemTeamScopeType
  teamIds?: number[]
  callbackUrl?: string
}

interface TeamOption {
  label: string
  value: number
}

/**
 * 子系统管理面板（仅 root，由 AdminIntegrations Tab 容器承载）：创建/编辑/启停/删除子系统，管理团队授权范围（全员或指定团队）。
 * 接入密钥原文仅在创建/重置时显示一次。
 */
export function SubsystemsPanel() {
  const { t } = useTranslation()

  const [loading, setLoading] = useState(true)
  const [items, setItems] = useState<SubsystemItem[]>([])
  const [keyword, setKeyword] = useState('')

  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<SubsystemItem | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [teamOptions, setTeamOptions] = useState<TeamOption[]>([])
  const [teamLoading, setTeamLoading] = useState(false)
  const [secretShown, setSecretShown] = useState<string | null>(null)
  const [form] = Form.useForm<SubsystemFormValues>()

  const teamScope = Form.useWatch('teamScope', form)

  const load = useCallback(async (search = keyword) => {
    setLoading(true)
    try {
      const list = await getSubsystems(search)
      setItems(list)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [keyword])

  const loadTeams = useCallback(async () => {
    setTeamLoading(true)
    try {
      const res = await getAdminTeams({ pageNo: 1, pageSize: 200 })
      setTeamOptions(
        (res.items ?? []).map((team) => ({
          label: team.name || String(team.teamId ?? ''),
          value: Number(team.teamId),
        })),
      )
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setTeamLoading(false)
    }
  }, [])

  useEffect(() => {
    void load('')
    void loadTeams()
    // 首次进入拉取，后续由交互触发
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const handleSearch = () => {
    void load(keyword)
  }

  const openCreate = () => {
    setEditing(null)
    form.resetFields()
    form.setFieldsValue({ status: 'enabled', teamScope: 'allTeams' })
    setModalOpen(true)
  }

  const openEdit = async (record: SubsystemItem) => {
    setEditing(record)
    form.setFieldsValue({
      key: record.key ?? '',
      name: record.name ?? '',
      description: record.description ?? '',
      status: record.status ?? 'enabled',
      teamScope: record.teamScope ?? 'allTeams',
      teamIds: [],
      callbackUrl: record.callbackUrl ?? '',
    })
    setModalOpen(true)
    if (record.teamScope === 'specifiedTeams' && record.subsystemId) {
      try {
        const teamIds = await getSubsystemAuthorizedTeams(record.subsystemId)
        form.setFieldValue('teamIds', teamIds)
      } catch {
        // 错误已由全局请求中间件统一提示
      }
    }
  }

  const handleSubmit = async () => {
    const values = await form.validateFields()
    setSubmitting(true)
    try {
      const teamIds = values.teamScope === 'specifiedTeams' ? (values.teamIds ?? []) : []
      if (editing?.subsystemId) {
        await updateSubsystem(editing.subsystemId, {
          name: values.name,
          description: values.description,
          status: values.status,
          teamScope: values.teamScope,
          teamIds,
          callbackUrl: values.callbackUrl ?? '',
        })
        feedback.success(t('adminSubsystems.updateSuccess'))
        setModalOpen(false)
      } else {
        const created = await createSubsystem({
          key: values.key,
          name: values.name,
          description: values.description,
          teamScope: values.teamScope,
          teamIds,
          callbackUrl: values.callbackUrl ?? '',
        })
        feedback.success(t('adminSubsystems.createSuccess'))
        setModalOpen(false)
        if (created.secret) setSecretShown(created.secret)
      }
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSubmitting(false)
    }
  }

  const handleToggleStatus = async (record: SubsystemItem) => {
    if (!record.subsystemId) return
    try {
      const teamIds = record.teamScope === 'specifiedTeams' ? await getSubsystemAuthorizedTeams(record.subsystemId) : []
      await updateSubsystem(record.subsystemId, {
        name: record.name ?? '',
        description: record.description ?? '',
        status: record.status === 'enabled' ? 'disabled' : 'enabled',
        teamScope: record.teamScope ?? 'allTeams',
        teamIds,
        callbackUrl: record.callbackUrl ?? '',
      })
      feedback.success(t(record.status === 'enabled' ? 'adminSubsystems.disableSuccess' : 'adminSubsystems.enableSuccess'))
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleResetSecret = async (record: SubsystemItem) => {
    if (!record.subsystemId) return
    try {
      const res = await resetSubsystemSecret(record.subsystemId)
      feedback.success(t('adminSubsystems.resetSecretSuccess'))
      if (res.secret) setSecretShown(res.secret)
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleDelete = async (record: SubsystemItem) => {
    if (!record.subsystemId) return
    try {
      await deleteSubsystem(record.subsystemId)
      feedback.success(t('adminSubsystems.deleteSuccess'))
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const copyText = async (value: string) => {
    try {
      await navigator.clipboard.writeText(value)
      feedback.success(t('common.copySuccess'))
    } catch {
      feedback.error(t('common.copyFailed'))
    }
  }

  const columns: TableColumnsType<SubsystemItem> = useMemo(
    () => [
      {
        title: t('adminSubsystems.colKey'),
        dataIndex: 'key',
        width: 160,
        render: (value: string | null) => <Text code>{value || '-'}</Text>,
      },
      {
        title: t('adminSubsystems.colName'),
        dataIndex: 'name',
        width: 180,
        render: (value: string | null, record) => (
          <div style={{ lineHeight: 1.4, minWidth: 0 }}>
            <div>{value || '-'}</div>
            {record.description?.trim() && (
              <Text type="secondary" style={{ fontSize: 12 }}>
                {record.description}
              </Text>
            )}
          </div>
        ),
      },
      {
        title: t('adminSubsystems.colScope'),
        key: 'scope',
        width: 240,
        render: (_, record) => {
          if (record.teamScope !== 'specifiedTeams') {
            return <Tag color="geekblue">{t('adminSubsystems.scopeAllTeams')}</Tag>
          }
          // 展示完整授权团队名称清单；历史数据（团队已删除）回退 #id，名称缺失时回退计数
          const teams = record.authorizedTeams ?? []
          if (teams.length === 0) {
            return <Tag>{t('adminSubsystems.scopeSpecifiedTeams', { count: record.authorizedTeamCount ?? 0 })}</Tag>
          }
          return (
            <Space size={4} wrap>
              {teams.map((team) => (
                <Tag key={team.teamId ?? team.teamName}>{team.teamName || `#${team.teamId}`}</Tag>
              ))}
            </Space>
          )
        },
      },
      {
        title: t('adminSubsystems.colStatus'),
        key: 'status',
        width: 90,
        render: (_, record) =>
          record.status === 'disabled' ? (
            <Tag color="error">{t('adminSubsystems.statusDisabled')}</Tag>
          ) : (
            <Tag color="success">{t('adminSubsystems.statusEnabled')}</Tag>
          ),
      },
      {
        title: t('adminSubsystems.colOnline'),
        dataIndex: 'online',
        width: 90,
        render: (online: boolean | null) =>
          online ? <Tag color="processing">{t('adminSubsystems.online')}</Tag> : <Tag>{t('adminSubsystems.offline')}</Tag>,
      },
      {
        title: t('adminSubsystems.colCreateTime'),
        dataIndex: 'createTime',
        width: 150,
        render: (value: string | null) => <span style={{ whiteSpace: 'nowrap' }}>{formatDateTime(value)}</span>,
      },
      {
        title: t('adminSubsystems.colActions'),
        key: 'actions',
        width: 160,
        fixed: 'right',
        render: (_, record) => (
          <Space size={0}>
            <Tooltip title={t('adminSubsystems.edit')}>
              <Button
                type="text"
                size="small"
                icon={<EditOutlined />}
                aria-label={t('adminSubsystems.edit')}
                onClick={() => void openEdit(record)}
              />
            </Tooltip>
            <Popconfirm
              title={t(record.status === 'enabled' ? 'adminSubsystems.disableConfirm' : 'adminSubsystems.enableConfirm')}
              okButtonProps={{ danger: record.status === 'enabled' }}
              onConfirm={() => void handleToggleStatus(record)}
            >
              <Tooltip title={t(record.status === 'enabled' ? 'adminSubsystems.disable' : 'adminSubsystems.enable')}>
                <Button
                  type="text"
                  size="small"
                  danger={record.status === 'enabled'}
                  icon={record.status === 'enabled' ? <StopOutlined /> : <CheckCircleOutlined />}
                  aria-label={t(record.status === 'enabled' ? 'adminSubsystems.disable' : 'adminSubsystems.enable')}
                />
              </Tooltip>
            </Popconfirm>
            <Popconfirm
              title={t('adminSubsystems.resetSecretConfirm')}
              onConfirm={() => void handleResetSecret(record)}
            >
              <Tooltip title={t('adminSubsystems.resetSecret')}>
                <Button
                  type="text"
                  size="small"
                  icon={<KeyOutlined />}
                  aria-label={t('adminSubsystems.resetSecret')}
                />
              </Tooltip>
            </Popconfirm>
            <Popconfirm
              title={t('adminSubsystems.deleteConfirm')}
              okButtonProps={{ danger: true }}
              onConfirm={() => void handleDelete(record)}
            >
              <Tooltip title={t('adminSubsystems.delete')}>
                <Button
                  type="text"
                  size="small"
                  danger
                  icon={<DeleteOutlined />}
                  aria-label={t('adminSubsystems.delete')}
                />
              </Tooltip>
            </Popconfirm>
          </Space>
        ),
      },
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, keyword],
  )

  return (
    <>
    <div style={{ display: 'flex', flexDirection: 'column', height: adminSubsystemsPageHeight, minHeight: 0 }}>
        <PageToolbar
          filters={
            <Space size={spacing.xs} wrap>
              <Input
                value={keyword}
                onChange={(e) => setKeyword(e.target.value)}
                onPressEnter={() => handleSearch()}
                allowClear
                maxLength={50}
                prefix={<SearchOutlined style={{ color: 'inherit' }} />}
                placeholder={t('adminSubsystems.searchPlaceholder')}
                style={{ width: 260 }}
              />
              <Button type="primary" icon={<SearchOutlined />} onClick={() => handleSearch()}>
                {t('adminSubsystems.search')}
              </Button>
            </Space>
          }
          actions={
            <Space size={spacing.xs}>
              <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
                {t('adminSubsystems.create')}
              </Button>
              <Tooltip title={t('adminSubsystems.refresh')}>
                <Button
                  icon={<ReloadOutlined />}
                  aria-label={t('adminSubsystems.refresh')}
                  onClick={() => void load()}
                />
              </Tooltip>
            </Space>
          }
        />
        <div style={{ flex: 1, minHeight: 0 }}>
          <DataTable<SubsystemItem>
            rowKey="subsystemId"
            columns={columns}
            dataSource={items}
            loading={loading}
            sticky
            scroll={{ x: 1100, y: adminSubsystemsTableScrollY }}
          />
        </div>
      </div>

      <Modal
        open={modalOpen}
        title={t(editing ? 'adminSubsystems.editTitle' : 'adminSubsystems.createTitle')}
        onCancel={() => setModalOpen(false)}
        confirmLoading={submitting}
        destroyOnHidden
        maskClosable={false}
        onOk={() => void handleSubmit()}
        okText={t('adminSubsystems.save')}
        cancelText={t('adminSubsystems.cancel')}
      >
        <Form form={form} layout="vertical" initialValues={{ status: 'enabled', teamScope: 'allTeams' }}>
          <Form.Item
            name="key"
            label={t('adminSubsystems.formKey')}
            rules={[
              { required: true, message: t('adminSubsystems.formKeyRequired') },
              { pattern: /^[a-zA-Z][a-zA-Z0-9_-]{1,39}$/, message: t('adminSubsystems.formKeyHint') },
            ]}
            extra={t('adminSubsystems.formKeyHint')}
          >
            <Input placeholder={t('adminSubsystems.formKeyPlaceholder')} maxLength={40} disabled={!!editing} />
          </Form.Item>
          <Form.Item
            name="name"
            label={t('adminSubsystems.formName')}
            rules={[
              { required: true, message: t('adminSubsystems.formNameRequired') },
              { max: 50, message: t('adminSubsystems.formNameMax') },
            ]}
          >
            <Input placeholder={t('adminSubsystems.formNamePlaceholder')} maxLength={50} />
          </Form.Item>
          <Form.Item name="description" label={t('adminSubsystems.formDescription')} rules={[{ max: 255 }]}>
            <Input.TextArea placeholder={t('adminSubsystems.formDescriptionPlaceholder')} maxLength={255} rows={3} />
          </Form.Item>
          <Form.Item
            name="callbackUrl"
            label={t('adminSubsystems.formCallbackUrl')}
            rules={[
              { max: 255, message: t('adminSubsystems.formCallbackUrlMax') },
              { pattern: /^$|^https?:\/\/.+/, message: t('adminSubsystems.formCallbackUrlPattern') },
            ]}
            extra={t('adminSubsystems.formCallbackUrlHint')}
          >
            <Input placeholder={t('adminSubsystems.formCallbackUrlPlaceholder')} maxLength={255} />
          </Form.Item>
          <Form.Item name="status" label={t('adminSubsystems.formStatus')}>
            <Radio.Group>
              <Radio value="enabled">{t('adminSubsystems.statusEnabled')}</Radio>
              <Radio value="disabled">{t('adminSubsystems.statusDisabled')}</Radio>
            </Radio.Group>
          </Form.Item>
          <Form.Item name="teamScope" label={t('adminSubsystems.formScope')}>
            <Radio.Group>
              <Radio value="allTeams">{t('adminSubsystems.scopeAllTeams')}</Radio>
              <Radio value="specifiedTeams">{t('adminSubsystems.scopeSpecifiedTeamsOption')}</Radio>
            </Radio.Group>
          </Form.Item>
          {teamScope === 'specifiedTeams' && (
            <Form.Item
              name="teamIds"
              label={t('adminSubsystems.formTeamIds')}
              rules={[{ required: true, message: t('adminSubsystems.formTeamIdsRequired') }]}
              extra={t('adminSubsystems.formTeamIdsHint')}
            >
              <Select
                mode="multiple"
                options={teamOptions}
                loading={teamLoading}
                placeholder={t('adminSubsystems.formTeamIdsPlaceholder')}
                showSearch
                optionFilterProp="label"
                style={{ width: '100%' }}
              />
            </Form.Item>
          )}
        </Form>
      </Modal>

      <Modal
        title={t('adminSubsystems.secretTitle')}
        open={secretShown !== null}
        maskClosable={false}
        onCancel={() => setSecretShown(null)}
        footer={[
          <Button key="copy" icon={<CopyOutlined />} onClick={() => void copyText(secretShown ?? '')}>
            {t('common.copy')}
          </Button>,
          <Button key="ok" type="primary" onClick={() => setSecretShown(null)}>
            {t('common.ok')}
          </Button>,
        ]}
      >
        <Alert type="warning" showIcon message={t('adminSubsystems.secretWarning')} style={{ marginBottom: spacing.md }} />
        <Paragraph code copyable={false} style={{ marginBottom: 0, wordBreak: 'break-all' }}>
          {secretShown}
        </Paragraph>
      </Modal>
    </>
  )
}
