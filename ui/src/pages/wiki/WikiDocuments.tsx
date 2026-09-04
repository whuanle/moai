import { useCallback, useEffect, useMemo, useState } from 'react'
import { Button, Form, Input, Modal, Popconfirm, Tooltip } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router'
import { DeleteOutlined } from '@ant-design/icons'
import { Page, DataTable, feedback } from '@/design-system'
import { formatDateTime } from '@/utils/datetime'
import {
  createWikiDocument,
  deleteWikiDocument,
  getWikiDocumentDetail,
  getWikiDocuments,
  updateWikiDocument,
  type WikiDocumentItem,
} from '@/api/wiki'

/** 角色：0=Owner 1=Admin 2=Member */
const ROLE_MEMBER = 2

interface DocumentFormValues {
  title: string
  content?: string
}

export function WikiDocuments() {
  const { t } = useTranslation()
  const params = useParams<{ id: string }>()
  const wikiId = Number(params.id)

  const [loading, setLoading] = useState(true)
  const [docs, setDocs] = useState<WikiDocumentItem[]>([])
  const [myRole, setMyRole] = useState<number | null>(null)
  const [formOpen, setFormOpen] = useState(false)
  const [editing, setEditing] = useState<WikiDocumentItem | null>(null)
  const [saving, setSaving] = useState(false)
  const [form] = Form.useForm<DocumentFormValues>()

  const isAdminPlus = myRole !== null && myRole !== ROLE_MEMBER

  const load = useCallback(async () => {
    if (!Number.isFinite(wikiId) || wikiId <= 0) return
    setLoading(true)
    try {
      const res = await getWikiDocuments(wikiId)
      setDocs(res.items ?? [])
      setMyRole(res.myRole ?? null)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [wikiId])

  useEffect(() => {
    void load()
  }, [load])

  const openCreate = () => {
    setEditing(null)
    form.resetFields()
    setFormOpen(true)
  }

  const openEditor = async (record: WikiDocumentItem) => {
    try {
      const detail = await getWikiDocumentDetail(Number(record.documentId))
      setEditing({ ...record, title: detail?.title ?? record.title })
      form.setFieldsValue({ title: detail?.title ?? '', content: detail?.content ?? '' })
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
        await updateWikiDocument(Number(editing.documentId), { title: values.title, content: values.content })
      } else if (Number.isFinite(wikiId)) {
        await createWikiDocument(wikiId, { title: values.title, content: values.content })
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

  const handleDelete = async (record: WikiDocumentItem) => {
    try {
      await deleteWikiDocument(Number(record.documentId))
      feedback.success(t('wiki.deleteSuccess'))
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const columns: TableColumnsType<WikiDocumentItem> = useMemo(
    () => [
      {
        title: t('wiki.colName'),
        dataIndex: 'title',
        render: (v: string | null, record) => (
          <Button type="link" style={{ paddingInline: 0 }} onClick={() => void openEditor(record)}>
            {v}
          </Button>
        ),
      },
      {
        title: t('wiki.colUpdateTime'),
        dataIndex: 'updateTime',
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
              width: 96,
              fixed: 'right' as const,
              render: (_: unknown, record: WikiDocumentItem) => (
                <Popconfirm title={t('wiki.deleteConfirm')} onConfirm={() => void handleDelete(record)}>
                  <Tooltip title={t('wiki.delete')}>
                    <Button type="text" size="small" danger icon={<DeleteOutlined />} aria-label={t('wiki.delete')} />
                  </Tooltip>
                </Popconfirm>
              ),
            } as TableColumnsType<WikiDocumentItem>[number],
          ]
        : []),
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, isAdminPlus],
  )

  return (
    <Page
      breadcrumb={[
        { title: <Link to="/wiki">{t('wiki.title')}</Link> },
        { title: t('wiki.docTitle') },
      ]}
      extra={
        <Button type="primary" onClick={openCreate}>
          {t('wiki.docCreate')}
        </Button>
      }
    >
      <DataTable<WikiDocumentItem>
        rowKey="documentId"
        columns={columns}
        dataSource={docs}
        loading={loading}
        sticky
        scroll={{ x: 600 }}
        onRefresh={() => void load()}
        refreshLoading={loading}
      />
      <Modal
        open={formOpen}
        title={editing ? t('wiki.docEditTitle') : t('wiki.docCreateTitle')}
        onOk={() => void handleSubmit()}
        onCancel={() => setFormOpen(false)}
        okText={editing ? t('wiki.save') : t('wiki.confirm')}
        cancelText={t('wiki.cancel')}
        confirmLoading={saving}
        width={760}
        destroyOnHidden
        maskClosable={false}
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="title"
            label={t('wiki.docTitleLabel')}
            rules={[
              { required: true, message: t('wiki.docTitlePlaceholder') },
              { max: 100, message: `${t('wiki.docTitleLabel')} ≤ 100` },
            ]}
          >
            <Input placeholder={t('wiki.docTitlePlaceholder')} maxLength={100} />
          </Form.Item>
          <Form.Item name="content" label={t('wiki.docContent')}>
            <Input.TextArea placeholder={t('wiki.docContentPlaceholder')} rows={14} styles={{ textarea: { fontFamily: 'monospace' } }} />
          </Form.Item>
        </Form>
      </Modal>
    </Page>
  )
}
