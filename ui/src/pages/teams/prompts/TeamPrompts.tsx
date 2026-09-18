import { useCallback, useEffect, useMemo, useState } from 'react'
import { DeleteOutlined, EditOutlined, EyeOutlined, SearchOutlined, SendOutlined, UndoOutlined } from '@ant-design/icons'
import { Avatar, Button, Col, Empty, Form, Input, Modal, Pagination, Popconfirm, Row, Space, Spin, Tag, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { QueryBar, Card, feedback } from '@/design-system'
import { neutralColors, spacing } from '@/design-system/theme'
import { formatDateTime } from '@/utils/datetime'
import { resolveStorageUrl } from '@/utils/storage'
import { classifyApi, ClassifyType, classifyLabel, type Classify } from '@/api/classify'
import { applyPublication, withdrawPublication } from '@/api/publication'
import {
  deletePrompt,
  getPromptDetail,
  getTeamPrompts,
  type PromptDetail,
  type PromptItem,
} from '@/api/prompt'
import { PromptDetailModal } from '@/pages/prompts/PromptDetailModal'

const { Text, Paragraph } = Typography

const CARD_PAGE_DEFAULT_SIZE = 12
const CARD_PAGE_SIZE_OPTIONS = [12, 24, 48]

interface PromptFilters extends Record<string, unknown> {
  keywords?: string
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
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(CARD_PAGE_DEFAULT_SIZE)
  const [detail, setDetail] = useState<PromptDetail | null>(null)
  const [detailOpen, setDetailOpen] = useState(false)
  const [applyRecord, setApplyRecord] = useState<PromptItem | null>(null)
  const [applying, setApplying] = useState(false)
  const [applyForm] = Form.useForm<{ applyReason?: string }>()
  const [filterForm] = Form.useForm<PromptFilters>()

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
      const list = await getTeamPrompts(teamId, { keywords, promptClassId })
      // 按被使用次数降序，次数相同保持后端返回顺序
      setItems(list.slice().sort((a, b) => (b.counter ?? 0) - (a.counter ?? 0)))
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

  const renderCategory = (record: PromptItem) =>
    record.promptClassId ? (
      <Tag>{classNameMap.get(Number(record.promptClassId)) || '-'}</Tag>
    ) : (
      <Tag>{t('prompt.unclassified')}</Tag>
    )

  /** 查看/编辑/上架/撤回/删除操作，卡片底栏使用 */
  const renderActions = (record: PromptItem) => (
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
  )

  const applyFilters = () => {
    const values = filterForm.getFieldsValue()
    setKeywords(typeof values.keywords === 'string' && values.keywords.trim() ? values.keywords.trim() : undefined)
    setPage(1)
  }

  const total = items.length
  const cardPageCount = Math.max(1, Math.ceil(total / pageSize))
  const safePage = Math.min(page, cardPageCount)
  const pagedItems = useMemo(
    () => items.slice((safePage - 1) * pageSize, safePage * pageSize),
    [items, safePage, pageSize],
  )

  return (
    <>
      <QueryBar
        form={filterForm}
        onSearch={applyFilters}
        onReset={applyFilters}
        loading={loading}
        extra={
          <>
            {canManage && (
              <Button type="primary" onClick={() => navigate(`/team/${teamId}/prompt/new`)}>
                {t('prompt.create')}
              </Button>
            )}
            <Button onClick={() => void load()} loading={loading}>
              {t('ds.table.refresh')}
            </Button>
          </>
        }
      >
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
      <Space size={4} wrap style={{ marginBottom: spacing.md }}>
        <Tag.CheckableTag
          checked={promptClassId === undefined}
          onChange={() => {
            setPromptClassId(undefined)
            setPage(1)
          }}
        >
          {t('prompt.categoryAll')}
        </Tag.CheckableTag>
        {classifies.map((c) => (
          <Tag.CheckableTag
            key={String(c.classifyId ?? '')}
            checked={promptClassId === Number(c.classifyId)}
            onChange={() => {
              setPromptClassId(Number(c.classifyId) || undefined)
              setPage(1)
            }}
          >
            {classifyLabel(c)}
          </Tag.CheckableTag>
        ))}
      </Space>
      <Spin spinning={loading}>
        {pagedItems.length === 0 ? (
          <Empty description={t('prompt.empty')} />
        ) : (
          <Row gutter={[spacing.md, spacing.md]}>
            {pagedItems.map((record) => {
              const name = record.name || '-'
              return (
                <Col key={String(record.promptId ?? '')} xs={24} sm={12} md={8} lg={6} xl={4} xxl={4}>
                  <Card
                    style={{ height: '100%' }}
                    styles={{ body: { padding: spacing.md, display: 'flex', flexDirection: 'column', height: '100%' } }}
                  >
                    <div style={{ display: 'flex', flexDirection: 'column', gap: spacing.sm, height: '100%' }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: spacing.sm }}>
                        <Avatar shape="square" size={44} src={record.avatarPath ? resolveStorageUrl(record.avatarPath) : undefined} alt={name}>
                          {name.slice(0, 1).toUpperCase()}
                        </Avatar>
                        <div style={{ minWidth: 0 }}>
                          <div
                            title={name}
                            style={{
                              fontWeight: 600,
                              fontSize: 15,
                              lineHeight: 1.4,
                              whiteSpace: 'nowrap',
                              overflow: 'hidden',
                              textOverflow: 'ellipsis',
                            }}
                          >
                            {name}
                          </div>
                          <div style={{ marginTop: 2 }}>{renderStatus(record)}</div>
                        </div>
                      </div>
                      <Paragraph type="secondary" style={{ fontSize: 13, marginBottom: 0, minHeight: 38 }} ellipsis={{ rows: 2 }}>
                        {record.description || '-'}
                      </Paragraph>
                      <div>{renderCategory(record)}</div>
                      <div
                        style={{
                          marginTop: 'auto',
                          paddingTop: spacing.sm,
                          borderTop: `1px solid ${neutralColors.border}`,
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'space-between',
                          gap: spacing.sm,
                        }}
                      >
                        {canManage ? (
                          renderActions(record)
                        ) : (
                          <Button type="text" size="small" icon={<EyeOutlined />} onClick={() => void openDetail(record)}>
                            {t('prompt.view')}
                          </Button>
                        )}
                        <Text type="secondary" style={{ fontSize: 12, whiteSpace: 'nowrap' }}>
                          {formatDateTime(record.updateTime)}
                        </Text>
                      </div>
                    </div>
                  </Card>
                </Col>
              )
            })}
          </Row>
        )}
      </Spin>
      {total > 0 && (
        <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: spacing.md }}>
          <Pagination
            current={safePage}
            pageSize={pageSize}
            total={total}
            onChange={(next, nextSize) => {
              setPage(nextSize !== pageSize ? 1 : next)
              setPageSize(nextSize)
            }}
            showSizeChanger
            pageSizeOptions={CARD_PAGE_SIZE_OPTIONS.map(String)}
          />
        </div>
      )}
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
