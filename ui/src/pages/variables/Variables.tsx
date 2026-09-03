import { useCallback, useEffect, useMemo, useState } from 'react'
import { Button, Form, Input, Modal, Popconfirm, Select, Space, Switch, Tag, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { Page, DataTable, QueryBar, feedback } from '@/design-system'
import { useAppStore } from '@/store/app'
import { formatDateTime } from '@/utils/datetime'
import {
  createVariable,
  deleteVariable,
  getVariableDetail,
  getVariables,
  updateVariable,
  type TeamVariableItem,
} from '@/api/variable'

const { Text } = Typography

/** 角色：0=Owner 1=Admin 2=Member */
const ROLE_MEMBER = 2

interface VariableFormValues {
  key?: string
  groupName?: string
  isSecret?: boolean
  value?: string
  description?: string
}

interface VariableFilters extends Record<string, unknown> {
  groupName?: string
  keyword?: string
}

export function Variables() {
  const { t } = useTranslation()
  const currentTeamId = useAppStore((state) => state.currentTeamId)

  const [loading, setLoading] = useState(false)
  const [items, setItems] = useState<TeamVariableItem[]>([])
  const [myRole, setMyRole] = useState<number | null>(null)
  const [groupName, setGroupName] = useState<string | undefined>(undefined)
  const [keyword, setKeyword] = useState<string | undefined>(undefined)
  const [formOpen, setFormOpen] = useState(false)
  const [editing, setEditing] = useState<TeamVariableItem | null>(null)
  const [saving, setSaving] = useState(false)
  const [form] = Form.useForm<VariableFormValues>()
  const [filterForm] = Form.useForm()

  const isAdminPlus = myRole !== null && myRole !== ROLE_MEMBER

  const load = useCallback(async () => {
    if (!currentTeamId) {
      setItems([])
      setMyRole(null)
      return
    }
    setLoading(true)
    try {
      const res = await getVariables(Number(currentTeamId), { groupName, keyword })
      setItems(res.items ?? [])
      setMyRole(res.myRole ?? null)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [currentTeamId, groupName, keyword])

  useEffect(() => {
    void load()
  }, [load])

  const groupOptions = useMemo(() => {
    const groups = Array.from(new Set(items.map(i => i.groupName).filter((g): g is string => !!g)))
    return groups.map(g => ({ value: g, label: g }))
  }, [items])

  const openCreate = () => {
    setEditing(null)
    form.resetFields()
    setFormOpen(true)
  }

  const openEdit = async (record: TeamVariableItem) => {
    try {
      // 私密变量的值仅详情接口（管理员）可见，但不回填编辑框（留空 = 保持不变）
      const detail = await getVariableDetail(Number(record.variableId))
      setEditing(record)
      form.setFieldsValue({
        groupName: detail?.groupName ?? '',
        isSecret: record.isSecret ?? false,
        value: record.isSecret ? undefined : (detail?.value ?? undefined),
        description: detail?.description ?? '',
      })
      setFormOpen(true)
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleSubmit = async () => {
    const values = await form.validateFields()
    setSaving(true)
    try {
      if (editing) {
        // 私密变量编辑时留空 = 保持不变
        const keepSecretValue = editing.isSecret && !values.value
        await updateVariable(Number(editing.variableId), {
          groupName: values.groupName,
          value: keepSecretValue ? undefined : values.value,
          description: values.description,
        })
      } else if (currentTeamId) {
        await createVariable({
          teamId: Number(currentTeamId),
          key: values.key!,
          groupName: values.groupName,
          isSecret: values.isSecret === true,
          value: values.value!,
          description: values.description,
        })
      }
      feedback.success(t(editing ? 'variable.saveSuccess' : 'variable.createSuccess'))
      setFormOpen(false)
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSaving(false)
    }
  }

  const handleDelete = async (record: TeamVariableItem) => {
    try {
      await deleteVariable(Number(record.variableId))
      feedback.success(t('variable.deleteSuccess'))
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  /** 应用筛选（搜索与重置共用：重置后表单已清空，读到的即空值） */
  const applyFilters = () => {
    const values = filterForm.getFieldsValue() as VariableFilters
    setGroupName(typeof values.groupName === 'string' && values.groupName ? values.groupName : undefined)
    setKeyword(typeof values.keyword === 'string' && values.keyword.trim() ? values.keyword.trim() : undefined)
  }

  const columns: TableColumnsType<TeamVariableItem> = useMemo(
    () => [
      { title: t('variable.colKey'), dataIndex: 'key', width: 160, render: (v: string) => <Text code>{v}</Text> },
      { title: t('variable.colGroup'), dataIndex: 'groupName', width: 110, render: (v: string | null) => v || '-' },
      {
        title: t('variable.colType'),
        dataIndex: 'isSecret',
        width: 90,
        render: (v: boolean) => (v ? <Tag color="red">{t('variable.secret')}</Tag> : <Tag>{t('variable.plain')}</Tag>),
      },
      {
        title: t('variable.colValue'),
        key: 'value',
        ellipsis: true,
        render: (_, record) =>
          record.isSecret ? <Text type="secondary">••••••••</Text> : <Text copyable={!!record.value}>{record.value}</Text>,
      },
      { title: t('variable.colDesc'), dataIndex: 'description', width: 150, ellipsis: true, render: (v: string | null) => v || '-' },
      {
        title: t('variable.colUpdateTime'),
        dataIndex: 'updateTime',
        width: 160,
        render: (v: string | null) => (v ? formatDateTime(v) : '-'),
      },
      ...(isAdminPlus
        ? [
            {
              title: t('variable.colActions'),
              key: 'actions',
              width: 130,
              render: (_: unknown, record: TeamVariableItem) => (
                <Space size={0} wrap>
                  <Button type="link" size="small" onClick={() => void openEdit(record)}>
                    {t('variable.edit')}
                  </Button>
                  <Popconfirm title={t('variable.deleteConfirm')} onConfirm={() => void handleDelete(record)}>
                    <Button type="link" size="small" danger>
                      {t('variable.delete')}
                    </Button>
                  </Popconfirm>
                </Space>
              ),
            } as TableColumnsType<TeamVariableItem>[number],
          ]
        : []),
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, isAdminPlus],
  )

  if (!currentTeamId) {
    return (
      <Page title={t('variable.title')}>
        <div style={{ padding: '48px 0', textAlign: 'center' }}>
          <Text type="secondary">
            {t('wiki.selectTeamFirst')} <Link to="/team">{t('wiki.goCreateTeam')}</Link>
          </Text>
        </div>
      </Page>
    )
  }

  return (
    <Page
      title={t('variable.title')}
      subtitle={isAdminPlus ? undefined : t('variable.memberReadOnly')}
      extra={
        isAdminPlus ? (
          <Button type="primary" onClick={openCreate}>
            {t('variable.create')}
          </Button>
        ) : undefined
      }
    >
      <QueryBar
        form={filterForm}
        onSearch={applyFilters}
        onReset={applyFilters}
        loading={loading}
      >
        <Form.Item name="groupName" label={t('variable.colGroup')}>
          <Select allowClear placeholder={t('variable.groupAll')} style={{ width: 140 }} options={groupOptions} />
        </Form.Item>
        <Form.Item name="keyword" label={t('variable.searchLabel')}>
          <Input allowClear placeholder={t('variable.searchPlaceholder')} maxLength={100} style={{ width: 200 }} />
        </Form.Item>
      </QueryBar>
      <DataTable<TeamVariableItem>
        rowKey="variableId"
        columns={columns}
        dataSource={items}
        loading={loading}
        onRefresh={() => void load()}
        refreshLoading={loading}
      />
      <Modal
        open={formOpen}
        title={editing ? t('variable.editTitle') : t('variable.createTitle')}
        onOk={() => void handleSubmit()}
        onCancel={() => setFormOpen(false)}
        okText={editing ? t('variable.save') : t('variable.confirm')}
        cancelText={t('variable.cancel')}
        confirmLoading={saving}
        destroyOnHidden
        maskClosable={false}
      >
        <Form form={form} layout="vertical" initialValues={{ isSecret: false }}>
          {editing ? (
            <Form.Item label={t('variable.key')} required>
              <Input value={editing.key ?? undefined} disabled />
            </Form.Item>
          ) : (
            <Form.Item
              name="key"
              label={t('variable.key')}
              rules={[
                { required: true, message: t('variable.keyPlaceholder') },
                { pattern: /^[A-Za-z][A-Za-z0-9_]{0,99}$/, message: t('variable.keyRule') },
              ]}
            >
              <Input placeholder={t('variable.keyPlaceholder')} maxLength={100} />
            </Form.Item>
          )}
          <Form.Item name="groupName" label={t('variable.groupName')} rules={[{ max: 50 }]}>
            <Input placeholder={t('variable.groupNamePlaceholder')} maxLength={50} />
          </Form.Item>
          <Form.Item name="isSecret" label={t('variable.type')} valuePropName="checked">
            <Switch checkedChildren={t('variable.secret')} unCheckedChildren={t('variable.plain')} disabled={!!editing} />
          </Form.Item>
          <Form.Item
            name="value"
            label={t('variable.value')}
            rules={editing && editing.isSecret ? [] : [{ required: true, message: t('variable.valuePlaceholder') }]}
            extra={editing && editing.isSecret ? t('variable.valueKeepHint') : undefined}
          >
            <Input.TextArea
              placeholder={t('variable.valuePlaceholder')}
              rows={3}
              styles={{ textarea: { fontFamily: 'monospace' } }}
            />
          </Form.Item>
          <Form.Item name="description" label={t('variable.description')} rules={[{ max: 255 }]}>
            <Input placeholder={t('variable.descriptionPlaceholder')} maxLength={255} />
          </Form.Item>
        </Form>
      </Modal>
    </Page>
  )
}
