import { useCallback, useEffect, useMemo, useState } from 'react'
import { DeleteOutlined, EditOutlined, EyeOutlined, SearchOutlined, SendOutlined, UndoOutlined } from '@ant-design/icons'
import { Avatar, Button, Form, Input, Modal, Popconfirm, Select, Space, Tag, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { DataTable, QueryBar, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { formatDateTime } from '@/utils/datetime'
import { resolveStorageUrl } from '@/utils/storage'
import { classifyApi, ClassifyType, type Classify } from '@/api/classify'
import { applyPublication, withdrawPublication } from '@/api/publication'
import {
  deletePrompt,
  getPromptDetail,
  getTeamPrompts,
  type PromptDetail,
  type PromptItem,
} from '@/api/prompt'
import { PromptDetailModal } from '@/pages/prompts/PromptDetailModal'

const { Text } = Typography

interface PromptFilters extends Record<string, unknown> {
  keywords?: string
  promptClassId?: number
}

/** 团队提示词：团队管理员创建管理，团队成员全部可见可用，支持申请上架到提示词市场 */
export function TeamPrompts({ teamId, canManage }: { teamId: number; canManage: boolean }) {
  const { t } = useTranslation()
  const navigate = useNavigate()

  const [loading, setLoading] = useState(false)
  const [items, setItems] = useState<PromptItem[]>([])
  const [classifies, setClassifies] = useState<Classify[]>([])
  const [keywords, setKeywords] = useState<string | undefined>(undefined)
  const [promptClassId, setPromptClassId] = useState<number | undefined>(undefined)
  const [detail, setDetail] = useState<PromptDetail | null>(null)
  const [detailOpen, setDetailOpen] = useState(false)
  const [applyRecord, setApplyRecord] = useState<PromptItem | null>(null)
  const [applying, setApplying] = useState(false)
  const [applyForm] = Form.useForm<{ applyReason?: string }>()
  const [filterForm] = Form.useForm<PromptFilters>()

  const classOptions = useMemo(
    () => classifies.map((c) => ({ value: Number(c.classifyId), label: c.name ?? '' })),
    [classifies],
  )
  const classNameMap = useMemo(
    () => new Map(classifies.map((c) => [Number(c.classifyId), c.name ?? ''])),
    [classifies],
  )

  const loadClassifies = useCallback(async () => {
    try {
      setClassifies(await classifyApi.getClassifies(ClassifyType.Prompt))
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }, [])

  const load = useCallback(async () => {
    if (!teamId) return
    setLoading(true)
    try {
      setItems(await getTeamPrompts(teamId, { keywords, promptClassId }))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [teamId, keywords, promptClassId])

  useEffect(() => {
    void loadClassifies()
  }, [loadClassifies])

  useEffect(() => {
    void load()
  }, [load])

  const openDetail = async (record: PromptItem) => {
    try {
      const detailRes = await getPromptDetail(Number(record.promptId))
      setDetail(detailRes ?? null)
      setDetailOpen(true)
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleDelete = async (record: PromptItem) => {
    try {
      await deletePrompt(Number(record.promptId))
      feedback.success(t('prompt.deleteSuccess'))
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleApply = async () => {
    if (!applyRecord) return
    const values = await applyForm.validateFields()
    setApplying(true)
    try {
      await applyPublication({
        resourceType: 'prompt',
        resourceId: String(applyRecord.promptId),
        applyReason: values.applyReason,
      })
      feedback.success(t('prompt.applySuccess'))
      setApplyRecord(null)
      applyForm.resetFields()
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setApplying(false)
    }
  }

  const handleWithdraw = async (record: PromptItem) => {
    if (!record.pendingPublicationId) return
    try {
      await withdrawPublication(String(record.pendingPublicationId))
      feedback.success(t('prompt.withdrawSuccess'))
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const renderStatus = (record: PromptItem) => {
    if (record.isPublic) return <Tag color="success">{t('prompt.statusPublic')}</Tag>
    if (record.pendingPublicationId) return <Tag color="processing">{t('prompt.statusPending')}</Tag>
    return <Tag>{t('prompt.statusPrivate')}</Tag>
  }

  const applyFilters = () => {
    const values = filterForm.getFieldsValue()
    setKeywords(typeof values.keywords === 'string' && values.keywords.trim() ? values.keywords.trim() : undefined)
    setPromptClassId(values.promptClassId || undefined)
  }

  const columns: TableColumnsType<PromptItem> = useMemo(
    () => [
      {
        title: t('prompt.colName'),
        key: 'name',
        render: (_, record) => (
          <Space size={spacing.sm}>
            <Avatar size={32} src={record.avatarPath ? resolveStorageUrl(record.avatarPath) : undefined}>
              {(record.name ?? '?').slice(0, 1).toUpperCase()}
            </Avatar>
            <span style={{ fontWeight: 500 }}>{record.name || '-'}</span>
          </Space>
        ),
      },
      { title: t('prompt.colDesc'), dataIndex: 'description', ellipsis: true, render: (v: string | null) => v || '-' },
      {
        title: t('prompt.colClass'),
        dataIndex: 'promptClassId',
        width: 120,
        render: (v: number | null) => (v ? classNameMap.get(Number(v)) || '-' : <Text type="secondary">{t('prompt.unclassified')}</Text>),
      },
      { title: t('prompt.colStatus'), key: 'status', width: 100, render: (_, record) => renderStatus(record) },
      {
        title: t('prompt.colUpdateTime'),
        dataIndex: 'updateTime',
        width: 160,
        render: (v: string | null) => <span style={{ whiteSpace: 'nowrap' }}>{formatDateTime(v)}</span>,
      },
      {
        title: t('prompt.colActions'),
        key: 'actions',
        width: 200,
        fixed: 'right' as const,
        render: (_, record) => (
          <Space size={0}>
            <Button type="text" size="small" icon={<EyeOutlined />} aria-label={t('prompt.view')} onClick={() => void openDetail(record)} />
            {canManage && (
              <>
                <Button
                  type="text"
                  size="small"
                  icon={<EditOutlined />}
                  aria-label={t('prompt.edit')}
                  onClick={() => navigate(`/team/${teamId}/prompt/${record.promptId}/edit`)}
                />
                {record.isPublic ? null : record.pendingPublicationId ? (
                  <Popconfirm title={t('prompt.withdrawConfirm')} onConfirm={() => void handleWithdraw(record)}>
                    <Button type="text" size="small" icon={<UndoOutlined />} aria-label={t('prompt.withdraw')} />
                  </Popconfirm>
                ) : (
                  <Button
                    type="text"
                    size="small"
                    icon={<SendOutlined />}
                    aria-label={t('prompt.apply')}
                    onClick={() => {
                      setApplyRecord(record)
                      applyForm.resetFields()
                    }}
                  />
                )}
                <Popconfirm title={t('prompt.deleteConfirm')} onConfirm={() => void handleDelete(record)}>
                  <Button type="text" size="small" danger icon={<DeleteOutlined />} aria-label={t('prompt.delete')} />
                </Popconfirm>
              </>
            )}
          </Space>
        ),
      },
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, canManage, classNameMap, navigate, teamId],
  )

  return (
    <>
      <QueryBar form={filterForm} onSearch={applyFilters} onReset={applyFilters} loading={loading}>
        <Form.Item name="promptClassId">
          <Select allowClear placeholder={t('prompt.classAll')} style={{ width: 160 }} options={classOptions} />
        </Form.Item>
        <Form.Item name="keywords">
          <Input
            placeholder={t('prompt.searchPlaceholder')}
            prefix={<SearchOutlined style={{ color: 'inherit' }} />}
            allowClear
            maxLength={100}
            style={{ width: 280 }}
          />
        </Form.Item>
      </QueryBar>
      <DataTable<PromptItem>
        rowKey="promptId"
        columns={columns}
        dataSource={items}
        loading={loading}
        sticky
        scroll={{ x: 900 }}
        toolbar={
          <Space size={12}>
            {canManage && (
              <Button type="primary" onClick={() => navigate(`/team/${teamId}/prompt/new`)}>
                {t('prompt.create')}
              </Button>
            )}
            <Text type="secondary">{t('ds.table.total', { total: items.length })}</Text>
          </Space>
        }
        onRefresh={() => void load()}
        refreshLoading={loading}
      />
      <PromptDetailModal open={detailOpen} detail={detail} onClose={() => setDetailOpen(false)} />
      <Modal
        open={!!applyRecord}
        title={t('prompt.applyTitle')}
        onOk={() => void handleApply()}
        onCancel={() => setApplyRecord(null)}
        okText={t('prompt.applyOk')}
        cancelText={t('prompt.cancel')}
        confirmLoading={applying}
        destroyOnHidden
        maskClosable={false}
      >
        <Form form={applyForm} layout="vertical">
          <Form.Item name="applyReason" label={t('prompt.applyReason')} rules={[{ max: 255 }]}>
            <Input.TextArea placeholder={t('prompt.applyReasonPlaceholder')} rows={3} maxLength={255} />
          </Form.Item>
        </Form>
      </Modal>
    </>
  )
}
