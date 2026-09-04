import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  BookOutlined,
  DeleteOutlined,
  EditOutlined,
  FileTextOutlined,
} from '@ant-design/icons'
import { Avatar, Button, Form, Input, Modal, Popconfirm, Space, Tooltip, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router'
import { Page, DataTable, feedback } from '@/design-system'
import { useAppStore } from '@/store/app'
import { createWiki, deleteWiki, getWikis, updateWiki, type WikiItem } from '@/api/wiki'
import { formatDateTime } from '@/utils/datetime'

const { Text } = Typography

/** 角色：0=Owner 1=Admin 2=Member */
const ROLE_MEMBER = 2

interface WikiFormValues {
  name: string
  description?: string
}

export function Wiki() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const currentTeamId = useAppStore((state) => state.currentTeamId)

  const [loading, setLoading] = useState(false)
  const [wikis, setWikis] = useState<WikiItem[]>([])
  const [myRole, setMyRole] = useState<number | null>(null)
  const [formOpen, setFormOpen] = useState(false)
  const [editing, setEditing] = useState<WikiItem | null>(null)
  const [saving, setSaving] = useState(false)
  const [form] = Form.useForm<WikiFormValues>()

  const isAdminPlus = myRole !== null && myRole !== ROLE_MEMBER

  const load = useCallback(async () => {
    if (!currentTeamId) {
      setWikis([])
      setMyRole(null)
      return
    }
    setLoading(true)
    try {
      const res = await getWikis(Number(currentTeamId))
      setWikis(res.items ?? [])
      setMyRole(res.myRole ?? null)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [currentTeamId])

  useEffect(() => {
    void load()
  }, [load])

  const openCreate = () => {
    setEditing(null)
    form.resetFields()
    setFormOpen(true)
  }

  const openEdit = (record: WikiItem) => {
    setEditing(record)
    form.setFieldsValue({ name: record.name ?? '', description: record.description ?? undefined })
    setFormOpen(true)
  }

  const handleSubmit = async () => {
    const values = await form.validateFields()
    setSaving(true)
    try {
      if (editing) {
        await updateWiki(Number(editing.wikiId), { name: values.name, description: values.description })
      } else if (currentTeamId) {
        await createWiki({ teamId: Number(currentTeamId), name: values.name, description: values.description })
      }
      feedback.success(t(editing ? 'wiki.saveSuccess' : 'wiki.createSuccess'))
      setFormOpen(false)
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSaving(false)
    }
  }

  const handleDelete = async (record: WikiItem) => {
    try {
      await deleteWiki(Number(record.wikiId))
      feedback.success(t('wiki.deleteSuccess'))
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  /** 图标 + 名称合并展示 */
  const renderName = (record: WikiItem) => {
    const name = record.name || '-'
    return (
      <Space>
        <Avatar shape="square" size={36} icon={<BookOutlined />} />
        <div style={{ lineHeight: 1.4, minWidth: 0 }}>
          <div>{name}</div>
          {record.description && (
            <Text type="secondary" style={{ fontSize: 12 }} ellipsis>
              {record.description}
            </Text>
          )}
        </div>
      </Space>
    )
  }

  const columns: TableColumnsType<WikiItem> = useMemo(
    () => [
      {
        title: t('wiki.colName'),
        key: 'name',
        render: (_, record) => renderName(record),
      },
      {
        title: t('wiki.colCreateTime'),
        dataIndex: 'createTime',
        width: 150,
        render: (v: string | null) => (
          <span style={{ whiteSpace: 'nowrap' }}>{formatDateTime(v)}</span>
        ),
      },
      ...(isAdminPlus
        ? [
            {
              title: t('wiki.colActions'),
              key: 'actions',
              width: 128,
              fixed: 'right' as const,
              render: (_: unknown, record: WikiItem) => (
                <Space size={0}>
                  <Tooltip title={t('wiki.documents')}>
                    <Button
                      type="text"
                      size="small"
                      icon={<FileTextOutlined />}
                      aria-label={t('wiki.documents')}
                      onClick={() => navigate(`/wiki/${record.wikiId}`)}
                    />
                  </Tooltip>
                  <Tooltip title={t('wiki.edit')}>
                    <Button
                      type="text"
                      size="small"
                      icon={<EditOutlined />}
                      aria-label={t('wiki.edit')}
                      onClick={() => openEdit(record)}
                    />
                  </Tooltip>
                  <Popconfirm title={t('wiki.deleteConfirm')} onConfirm={() => void handleDelete(record)}>
                    <Tooltip title={t('wiki.delete')}>
                      <Button type="text" size="small" danger icon={<DeleteOutlined />} aria-label={t('wiki.delete')} />
                    </Tooltip>
                  </Popconfirm>
                </Space>
              ),
            } as TableColumnsType<WikiItem>[number],
          ]
        : []),
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, isAdminPlus],
  )

  if (!currentTeamId) {
    return (
      <Page>
        <div style={{ padding: '48px 0', textAlign: 'center' }}>
          <Text type="secondary">
            {t('wiki.selectTeamFirst')}{' '}
            <Link to="/team">{t('wiki.goCreateTeam')}</Link>
          </Text>
        </div>
      </Page>
    )
  }

  return (
    <Page>
      <DataTable<WikiItem>
        rowKey="wikiId"
        columns={columns}
        dataSource={wikis}
        loading={loading}
        sticky
        scroll={{ x: 700 }}
        toolbar={
          <Space size={12}>
            {isAdminPlus && (
              <Button type="primary" onClick={openCreate}>
                {t('wiki.create')}
              </Button>
            )}
            <Text type="secondary">{t('ds.table.total', { total: wikis.length })}</Text>
          </Space>
        }
        onRefresh={() => void load()}
        refreshLoading={loading}
      />
      <Modal
        open={formOpen}
        title={editing ? t('wiki.editTitle') : t('wiki.createTitle')}
        onOk={() => void handleSubmit()}
        onCancel={() => setFormOpen(false)}
        okText={editing ? t('wiki.save') : t('wiki.confirm')}
        cancelText={t('wiki.cancel')}
        confirmLoading={saving}
        destroyOnHidden
        maskClosable={false}
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="name"
            label={t('wiki.name')}
            rules={[
              { required: true, message: t('wiki.namePlaceholder') },
              { max: 50, message: `${t('wiki.name')} ≤ 50` },
            ]}
          >
            <Input placeholder={t('wiki.namePlaceholder')} maxLength={50} />
          </Form.Item>
          <Form.Item name="description" label={t('wiki.desc')} rules={[{ max: 255 }]}>
            <Input.TextArea placeholder={t('wiki.descPlaceholder')} maxLength={255} rows={3} />
          </Form.Item>
        </Form>
      </Modal>
    </Page>
  )
}
