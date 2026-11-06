import { useCallback, useEffect, useMemo, useState } from 'react'
import { DeleteOutlined, DownloadOutlined, EditOutlined, EyeOutlined, SearchOutlined, SendOutlined, UndoOutlined } from '@ant-design/icons'
import { Avatar, Button, Col, Empty, Form, Input, Modal, Pagination, Popconfirm, Row, Space, Spin, Tag, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { Card, QueryBar, feedback, useNeutralColors } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { formatDateTime } from '@/utils/datetime'
import { resolveStorageUrl } from '@/utils/storage'
import { applyPublication, withdrawPublication } from '@/api/publication'
import {
  deleteSkill,
  downloadSkillFiles,
  getSkill,
  getTeamSkills,
  type SkillDetail,
  type SkillListItem,
} from '@/api/skills'
import { SkillDetailModal } from '@/pages/skills/SkillDetailModal'
import { SkillEditModal } from '@/pages/skills/SkillEditModal'

const { Text, Paragraph } = Typography

const CARD_PAGE_DEFAULT_SIZE = 12
const CARD_PAGE_SIZE_OPTIONS = [12, 24, 48]

interface SkillFilters extends Record<string, unknown> {
  keywords?: string
}

/** 团队技能：团队管理员创建管理，团队成员全部可见可用；卡片列表（同提示词/智能体），支持申请上架到技能市场 */
export function TeamSkills({ teamId, canManage }: { teamId: number; canManage: boolean }) {
  const { t } = useTranslation()
  const neutral = useNeutralColors()

  const [loading, setLoading] = useState(false)
  const [items, setItems] = useState<SkillListItem[]>([])
  const [keywords, setKeywords] = useState<string | undefined>(undefined)
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(CARD_PAGE_DEFAULT_SIZE)
  const [detail, setDetail] = useState<SkillDetail | null>(null)
  const [detailOpen, setDetailOpen] = useState(false)
  const [editOpen, setEditOpen] = useState(false)
  const [editTarget, setEditTarget] = useState<string | null>(null)
  const [applyRecord, setApplyRecord] = useState<SkillListItem | null>(null)
  const [applying, setApplying] = useState(false)
  const [applyForm] = Form.useForm<{ applyReason?: string }>()
  const [filterForm] = Form.useForm<SkillFilters>()

  const load = useCallback(async () => {
    if (!teamId) return
    setLoading(true)
    try {
      setItems(await getTeamSkills(teamId, { keywords }))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [teamId, keywords])

  useEffect(() => {
    void load()
  }, [load])

  const openDetail = async (record: SkillListItem) => {
    try {
      setDetail(await getSkill(record.id ?? ''))
      setDetailOpen(true)
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleDelete = async (record: SkillListItem) => {
    try {
      await deleteSkill(record.id ?? '')
      feedback.success(t('skills.deleteSuccess'))
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleDownload = async (record: SkillListItem) => {
    try {
      await downloadSkillFiles(record.id ?? '')
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
        resourceType: 'skill',
        resourceId: String(applyRecord.id),
        applyReason: values.applyReason,
      })
      feedback.success(t('skills.applySuccess'))
      setApplyRecord(null)
      applyForm.resetFields()
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setApplying(false)
    }
  }

  const handleWithdraw = async (record: SkillListItem) => {
    if (!record.pendingPublicationId) return
    try {
      await withdrawPublication(String(record.pendingPublicationId))
      feedback.success(t('skills.withdrawSuccess'))
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const renderStatus = (record: SkillListItem) => {
    if (record.isPublic) return <Tag color="success">{t('skills.statusPublic')}</Tag>
    if (record.pendingPublicationId) return <Tag color="processing">{t('skills.statusPending')}</Tag>
    return <Tag>{t('skills.statusPrivate')}</Tag>
  }

  /** 查看/编辑/上架/撤回/删除/下载操作，卡片底栏使用（紧凑卡片场景：纯图标 + aria-label） */
  const renderActions = (record: SkillListItem) => (
    <Space size={0}>
      <Button type="text" size="small" icon={<EyeOutlined />} aria-label={t('skills.detail')} onClick={() => void openDetail(record)} />
      {canManage && (
        <>
          <Button
            type="text"
            size="small"
            icon={<EditOutlined />}
            aria-label={t('skills.edit')}
            onClick={() => {
              setEditTarget(record.id ?? null)
              setEditOpen(true)
            }}
          />
          {record.isPublic ? null : record.pendingPublicationId ? (
            <Popconfirm title={t('skills.withdrawConfirm')} onConfirm={() => void handleWithdraw(record)}>
              <Button type="text" size="small" icon={<UndoOutlined />} aria-label={t('skills.withdraw')} />
            </Popconfirm>
          ) : (
            <Button
              type="text"
              size="small"
              icon={<SendOutlined />}
              aria-label={t('skills.apply')}
              onClick={() => {
                setApplyRecord(record)
                applyForm.resetFields()
              }}
            />
          )}
          <Popconfirm title={t('skills.deleteConfirm')} onConfirm={() => void handleDelete(record)}>
            <Button type="text" size="small" danger icon={<DeleteOutlined />} aria-label={t('skills.delete')} />
          </Popconfirm>
        </>
      )}
      <Button
        type="text"
        size="small"
        icon={<DownloadOutlined />}
        aria-label={t('skills.download')}
        onClick={() => void handleDownload(record)}
      />
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
      {/* 工具栏单行：搜索框 → 查询 → | → 新建技能；仅一个搜索框不传 onReset（规范 §7），查询按钮自带刷新语义不另设刷新 */}
      <QueryBar
        form={filterForm}
        onSearch={applyFilters}
        loading={loading}
        extra={
          canManage ? (
            <Button type="primary" onClick={() => {
              setEditTarget(null)
              setEditOpen(true)
            }}>
              {t('skills.create')}
            </Button>
          ) : undefined
        }
      >
        <Form.Item name="keywords">
          <Input
            placeholder={t('skills.searchPlaceholder')}
            prefix={<SearchOutlined style={{ color: 'inherit' }} />}
            allowClear
            maxLength={100}
            style={{ width: 280 }}
          />
        </Form.Item>
      </QueryBar>
      <Spin spinning={loading}>
        {pagedItems.length === 0 ? (
          <Empty description={t('skills.empty')} />
        ) : (
          <Row gutter={[spacing.md, spacing.md]}>
            {pagedItems.map((record) => {
              const name = record.name || record.key || '-'
              return (
                <Col key={String(record.id ?? '')} xs={24} sm={12} md={8} lg={6} xl={4} xxl={4}>
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
                          <div style={{ marginTop: 2, display: 'flex', alignItems: 'center', gap: 4 }}>
                            {renderStatus(record)}
                          </div>
                        </div>
                      </div>
                      <div style={{ display: 'flex', alignItems: 'center', gap: spacing.xs, flexWrap: 'wrap' }}>
                        <Tag>{t('skills.fileCountBadge', { count: record.fileCount ?? 0 })}</Tag>
                        <Tag>{t('skills.useCountBadge', { count: record.useCount ?? 0 })}</Tag>
                        <Text code style={{ fontSize: 12 }}>
                          {record.key}
                        </Text>
                      </div>
                      <Paragraph type="secondary" style={{ fontSize: 13, marginBottom: 0, minHeight: 38 }} ellipsis={{ rows: 2 }}>
                        {record.description || '-'}
                      </Paragraph>
                      <div
                        style={{
                          marginTop: 'auto',
                          paddingTop: spacing.sm,
                          borderTop: `1px solid ${neutral.border}`,
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'space-between',
                          gap: spacing.sm,
                        }}
                      >
                        {canManage ? (
                          renderActions(record)
                        ) : (
                          <Space size={0}>
                            <Button type="text" size="small" icon={<EyeOutlined />} aria-label={t('skills.detail')} onClick={() => void openDetail(record)} />
                            <Button
                              type="text"
                              size="small"
                              icon={<DownloadOutlined />}
                              aria-label={t('skills.download')}
                              onClick={() => void handleDownload(record)}
                            />
                          </Space>
                        )}
                        <span title={t('skills.colUpdateTime')} style={{ whiteSpace: 'nowrap', fontSize: 12, color: neutral.textSecondary }}>
                          {formatDateTime(record.updateTime)}
                        </span>
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
      <SkillDetailModal open={detailOpen} detail={detail} onClose={() => setDetailOpen(false)} />
      <SkillEditModal
        open={editOpen}
        skillId={editTarget}
        teamId={teamId}
        onSaved={() => {
          setEditOpen(false)
          setEditTarget(null)
          void load()
        }}
        onCancel={() => {
          setEditOpen(false)
          setEditTarget(null)
        }}
      />
      <Modal
        open={!!applyRecord}
        title={t('skills.applyTitle')}
        onOk={() => void handleApply()}
        onCancel={() => setApplyRecord(null)}
        okText={t('skills.applyOk')}
        cancelText={t('skills.cancel')}
        confirmLoading={applying}
        destroyOnHidden
        maskClosable={false}
      >
        <Form form={applyForm} layout="vertical">
          <Form.Item name="applyReason" label={t('skills.applyReason')} rules={[{ max: 255 }]}>
            <Input.TextArea placeholder={t('skills.applyReasonPlaceholder')} rows={3} maxLength={255} />
          </Form.Item>
        </Form>
      </Modal>
    </>
  )
}
