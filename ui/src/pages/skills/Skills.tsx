import { useCallback, useEffect, useMemo, useState } from 'react'
import { SearchOutlined, PlusOutlined, EyeOutlined, EditOutlined, DeleteOutlined, ReloadOutlined } from '@ant-design/icons'
import { Button, Form, Input, Modal, Popconfirm, Space, Switch, Tag, Tooltip, Typography, Upload } from 'antd'
import type { TableColumnsType, UploadFile } from 'antd'
import { useTranslation } from 'react-i18next'
import { Navigate } from 'react-router'
import { Page, DataTable, QueryBar, feedback } from '@/design-system'
import { controlHeight, spacing } from '@/design-system/theme'
import { useAppStore } from '@/store/app'
import { formatDateTime } from '@/utils/datetime'
import {
  createSkill,
  deleteSkill,
  getSkill,
  getSkills,
  setSkillDisable,
  updateSkill,
  uploadSkillFile,
  type SkillDetail,
  type SkillFileItem,
  type SkillListItem,
} from '@/api/skills'

const { Text } = Typography

const pageVerticalPadding = spacing.lg * 2
const queryBarHeight = controlHeight + spacing.lg
const tableHeaderHeight = 55
const tablePaginationHeight = 57
const tableScrollY = `calc(100vh - ${pageVerticalPadding + queryBarHeight + tableHeaderHeight + tablePaginationHeight}px)`

const keyPattern = /^[a-z][a-z0-9_]{0,29}$/

interface SkillFormValues {
  key: string
  name: string
  description?: string
  instructions?: string
  /** 搜索栏字段（与编辑表单共用实例） */
  searchText?: string
}

interface SkillFileEntry extends SkillFileItem {
  uid: string
}

export function Skills() {
  const { t } = useTranslation()
  const isAdmin = useAppStore((state) => state.userInfo?.isAdmin === true)

  const [form] = Form.useForm<SkillFormValues>()
  const [loading, setLoading] = useState(true)
  const [items, setItems] = useState<SkillListItem[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [pageNo, setPageNo] = useState(1)
  const [pageSize, setPageSize] = useState(20)
  const [searchText, setSearchText] = useState('')
  const [querySearchText, setQuerySearchText] = useState('')

  const [modalOpen, setModalOpen] = useState(false)
  const [editTarget, setEditTarget] = useState<SkillListItem | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [files, setFiles] = useState<SkillFileEntry[]>([])
  const [uploading, setUploading] = useState(false)

  const [detailOpen, setDetailOpen] = useState(false)
  const [detailLoading, setDetailLoading] = useState(false)
  const [detail, setDetail] = useState<SkillDetail | null>(null)

  const load = useCallback(
    async (page: number, size: number, search: string) => {
      setLoading(true)
      try {
        const res = await getSkills({ pageNo: page, pageSize: size, searchText: search || undefined })
        setItems(res.items)
        setTotalCount(res.totalCount)
      } catch {
        // 错误已由全局请求中间件统一提示
      } finally {
        setLoading(false)
      }
    },
    [],
  )

  useEffect(() => {
    void load(pageNo, pageSize, querySearchText)
  }, [load, pageNo, pageSize, querySearchText])

  const handleSearch = (values: { searchText?: string }) => {
    setPageNo(1)
    setQuerySearchText((values.searchText ?? '').trim())
  }

  const handleReset = () => {
    setPageNo(1)
    setQuerySearchText('')
    form.setFieldValue('searchText', undefined)
  }

  const openCreate = () => {
    setEditTarget(null)
    setFiles([])
    form.resetFields()
    setModalOpen(true)
  }

  const openEdit = async (record: SkillListItem) => {
    setEditTarget(record)
    setFiles([])
    form.resetFields()
    setModalOpen(true)
    setDetailLoading(true)
    try {
      const res = await getSkill(record.id ?? '')
      form.setFieldsValue({
        key: res.key ?? '',
        name: res.name ?? '',
        description: res.description ?? '',
        instructions: res.instructions ?? '',
      })
      setFiles(
        (res.files ?? []).map((f, index) => ({
          uid: `loaded-${index}`,
          path: f.path ?? '',
          fileId: f.fileId ?? 0,
          fileName: f.fileName ?? '',
        })),
      )
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setDetailLoading(false)
    }
  }

  const closeModal = () => {
    setModalOpen(false)
    setEditTarget(null)
    setFiles([])
    form.resetFields()
  }

  const handleSubmit = async () => {
    const values = await form.validateFields()
    setSubmitting(true)
    try {
      const payload = {
        name: values.name,
        description: values.description ?? '',
        instructions: values.instructions ?? '',
        files: files.map((f) => ({ path: f.path ?? '', fileId: f.fileId ?? 0, fileName: f.fileName ?? '' })),
      }
      if (editTarget) {
        await updateSkill(editTarget.id ?? '', payload)
        feedback.success(t('skills.updateSuccess'))
      } else {
        await createSkill({ key: values.key, ...payload })
        feedback.success(t('skills.createSuccess'))
      }
      closeModal()
      void load(pageNo, pageSize, querySearchText)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSubmitting(false)
    }
  }

  const handleUpload = async (file: File) => {
    setUploading(true)
    try {
      const fileId = await uploadSkillFile(file)
      setFiles((prev) => [
        ...prev,
        { uid: `f-${fileId}`, path: file.name, fileId, fileName: file.name },
      ])
    } catch {
      feedback.error(t('skills.uploadFailed'))
    } finally {
      setUploading(false)
    }
  }

  const openDetail = async (record: SkillListItem) => {
    setDetailOpen(true)
    setDetailLoading(true)
    setDetail(null)
    try {
      setDetail(await getSkill(record.id ?? ''))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setDetailLoading(false)
    }
  }

  const handleDisable = async (record: SkillListItem, isDisable: boolean) => {
    try {
      await setSkillDisable(record.id ?? '', isDisable)
      feedback.success(t('skills.updateSuccess'))
      void load(pageNo, pageSize, querySearchText)
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleDelete = async (record: SkillListItem) => {
    try {
      await deleteSkill(record.id ?? '')
      feedback.success(t('skills.deleteSuccess'))
      void load(pageNo, pageSize, querySearchText)
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const columns: TableColumnsType<SkillListItem> = useMemo(
    () => [
      {
        title: t('skills.colKey'),
        dataIndex: 'key',
        width: 160,
        render: (v: string | null) => <Text code>{v}</Text>,
      },
      {
        title: t('skills.colName'),
        dataIndex: 'name',
        width: 180,
        ellipsis: true,
      },
      {
        title: t('skills.colDescription'),
        dataIndex: 'description',
        width: 260,
        ellipsis: true,
        render: (v: string | null) => v || '-',
      },
      {
        title: t('skills.colIsSystem'),
        dataIndex: 'isSystem',
        width: 100,
        render: (v: boolean | null) =>
          v ? <Tag color="geekblue">{t('skills.isSystem')}</Tag> : <Tag>{t('skills.notSystem')}</Tag>,
      },
      {
        title: t('skills.colEnabled'),
        dataIndex: 'isDisable',
        width: 90,
        render: (v: boolean | null, record) => (
          <Switch
            checked={!(record.isDisable ?? false)}
            size="small"
            onChange={(checked) => void handleDisable(record, !checked)}
            aria-label={t('skills.colEnabled')}
          />
        ),
      },
      {
        title: t('skills.colFileCount'),
        dataIndex: 'fileCount',
        width: 90,
        render: (v: number | null) => v ?? 0,
      },
      {
        title: t('skills.colUpdateTime'),
        dataIndex: 'updateTime',
        width: 160,
        render: (v: string | null) => formatDateTime(v),
      },
      {
        title: t('skills.colActions'),
        key: 'actions',
        width: 130,
        fixed: 'right',
        sticky: true,
        render: (_: unknown, record) => (
          <Space size={0}>
            <Tooltip title={t('skills.detail')}>
              <Button
                type="text"
                size="small"
                icon={<EyeOutlined />}
                aria-label={`${t('skills.detail')}-${record.key ?? ''}`}
                onClick={() => void openDetail(record)}
              />
            </Tooltip>
            <Tooltip title={t('skills.edit')}>
              <Button
                type="text"
                size="small"
                icon={<EditOutlined />}
                aria-label={`${t('skills.edit')}-${record.key ?? ''}`}
                onClick={() => void openEdit(record)}
              />
            </Tooltip>
            {!(record.isSystem ?? false) && (
              <Popconfirm title={t('skills.deleteConfirm')} onConfirm={() => void handleDelete(record)}>
                <Tooltip title={t('skills.delete')}>
                  <Button
                    type="text"
                    size="small"
                    danger
                    icon={<DeleteOutlined />}
                    aria-label={`${t('skills.delete')}-${record.key ?? ''}`}
                  />
                </Tooltip>
              </Popconfirm>
            )}
          </Space>
        ),
      },
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, pageNo, pageSize, querySearchText],
  )

  if (!isAdmin) {
    return <Navigate to="/dashboard" replace />
  }

  const uploadFiles: UploadFile[] = files.map((f) => ({
    uid: f.uid,
    name: f.path ?? f.fileName ?? f.uid,
    status: 'done',
  }))

  return (
    <Page>
      <div style={{ display: 'flex', flexDirection: 'column', height: `calc(100vh - ${pageVerticalPadding}px)`, minHeight: 0 }}>
        <QueryBar onSearch={handleSearch} onReset={handleReset} loading={loading}>
          <Form.Item name="searchText">
            <Input
              placeholder={t('skills.searchPlaceholder')}
              prefix={<SearchOutlined style={{ color: 'inherit' }} />}
              allowClear
              maxLength={100}
              style={{ width: 280 }}
            />
          </Form.Item>
        </QueryBar>
        <div style={{ flex: 1, minHeight: 0 }}>
          <DataTable<SkillListItem>
            rowKey="id"
            columns={columns}
            dataSource={items}
            loading={loading}
            sticky
            scroll={{ x: 1180, y: tableScrollY }}
            pagination={{
              current: pageNo,
              pageSize,
              total: totalCount,
              showSizeChanger: true,
              onChange: (page, size) => {
                setPageNo(page)
                setPageSize(size)
              },
            }}
            toolbar={
              <Space>
                <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
                  {t('skills.create')}
                </Button>
                <Button icon={<ReloadOutlined />} onClick={() => void load(pageNo, pageSize, querySearchText)} loading={loading}>
                  {t('skills.refresh')}
                </Button>
                <Text type="secondary">{t('skills.totalCount', { count: totalCount })}</Text>
              </Space>
            }
          />
        </div>
      </div>

      <Modal
        open={modalOpen}
        title={editTarget ? t('skills.edit') : t('skills.create')}
        onCancel={closeModal}
        onOk={handleSubmit}
        okText={t('skills.save')}
        confirmLoading={submitting}
        maskClosable={false}
        destroyOnClose
        width={640}
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="key"
            label={t('skills.formKey')}
            rules={[
              { required: true, message: t('skills.keyRequired') },
              { pattern: keyPattern, message: t('skills.keyPattern') },
            ]}
            extra={t('skills.keyExtra')}
          >
            <Input maxLength={30} disabled={Boolean(editTarget)} />
          </Form.Item>
          <Form.Item
            name="name"
            label={t('skills.formName')}
            rules={[{ required: true, message: t('skills.nameRequired') }]}
          >
            <Input maxLength={50} />
          </Form.Item>
          <Form.Item name="description" label={t('skills.formDescription')} rules={[{ max: 255, message: t('skills.descMaxLength') }]}>
            <Input.TextArea rows={2} maxLength={255} />
          </Form.Item>
          <Form.Item name="instructions" label={t('skills.formInstructions')}>
            <Input.TextArea rows={6} placeholder={t('skills.instructionsPlaceholder')} />
          </Form.Item>
          {!editTarget?.isSystem && (
            <Form.Item label={t('skills.formFiles')} extra={t('skills.filesExtra')}>
              <Upload
                multiple
                fileList={uploadFiles}
                beforeUpload={(file) => {
                  void handleUpload(file)
                  return false
                }}
                onRemove={(file) => {
                  setFiles((prev) => prev.filter((f) => f.uid !== file.uid))
                }}
                accept=".py,.md,.json,.txt,.csv,.yaml,.yml,.j2,.html,.css,.js"
              >
                <Button loading={uploading}>{t('skills.addFile')}</Button>
              </Upload>
            </Form.Item>
          )}
        </Form>
      </Modal>

      <Modal
        open={detailOpen}
        title={detail ? `${detail.name ?? ''}（${detail.key ?? ''}）` : t('skills.detail')}
        footer={null}
        onCancel={() => setDetailOpen(false)}
        width={720}
        destroyOnClose
      >
        {detailLoading || !detail ? (
          <Text type="secondary">{t('common.loading')}</Text>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: spacing.md }}>
            <div>
              <Text strong>{t('skills.formDescription')}</Text>
              <p style={{ margin: 0 }}>{detail.description || '-'}</p>
            </div>
            <div>
              <Text strong>{t('skills.formInstructions')}</Text>
              <Input.TextArea
                value={detail.instructions ?? ''}
                readOnly
                autoSize={{ minRows: 6, maxRows: 20 }}
                style={{ marginTop: spacing.xs }}
              />
            </div>
            <div>
              <Text strong>{t('skills.formFiles')}</Text>
              {detail.files && detail.files.length > 0 ? (
                <ul style={{ margin: `${spacing.xs}px 0 0`, paddingLeft: spacing.lg }}>
                  {detail.files.map((f, index) => (
                    <li key={index}>
                      <Text code>{f.path}</Text>
                    </li>
                  ))}
                </ul>
              ) : (
                <p style={{ margin: 0 }}>-</p>
              )}
            </div>
            <Text type="secondary">
              {t('skills.colUpdateTime')}: {formatDateTime(detail.updateTime)}
            </Text>
          </div>
        )}
      </Modal>
    </Page>
  )
}
