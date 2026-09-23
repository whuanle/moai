import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  DeleteOutlined,
  DownloadOutlined,
  EditOutlined,
  EyeOutlined,
  PlusOutlined,
  ReloadOutlined,
  SearchOutlined,
  SendOutlined,
  ThunderboltOutlined,
  UndoOutlined,
} from '@ant-design/icons'
import { Button, Col, Empty, Form, Input, Modal, Pagination, Popconfirm, Row, Space, Spin, Tabs, Tag, Typography, Avatar } from 'antd'
import { useTranslation } from 'react-i18next'
import { useLocation, useNavigate } from 'react-router'
import { Card, Page, feedback, useNeutralColors } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { formatDateTime } from '@/utils/datetime'
import { resolveStorageUrl } from '@/utils/storage'
import { applyPublication, withdrawPublication } from '@/api/publication'
import { classifyApi, ClassifyType, classifyLabel, type Classify } from '@/api/classify'
import {
  deleteSkill,
  downloadSkillFiles,
  getMySkills,
  getSkill,
  getSkillMarketList,
  type SkillDetail,
  type SkillListItem,
} from '@/api/skills'
import { SkillDetailModal } from './SkillDetailModal'
import { SkillEditModal } from './SkillEditModal'

const { Text, Paragraph } = Typography

type SkillTab = 'market' | 'mine'

const PAGE_DEFAULT_SIZE = 12
const PAGE_SIZE_OPTIONS = [12, 24, 48]

/** 技能中心：菜单单一入口，页头 Tab 切换「技能市场 / 我的技能」，卡片分页展示 */
export function Skills() {
  const { t } = useTranslation()
  const neutral = useNeutralColors()
  const navigate = useNavigate()
  const location = useLocation()
  const tab: SkillTab = location.pathname.startsWith('/skills') ? 'mine' : 'market'

  const [loading, setLoading] = useState(false)
  const [items, setItems] = useState<SkillListItem[]>([])
  const [classifies, setClassifies] = useState<Classify[]>([])
  const [searchText, setSearchText] = useState('')
  const [keywords, setKeywords] = useState<string | undefined>(undefined)
  const [classifyId, setClassifyId] = useState<number | undefined>(undefined)
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(PAGE_DEFAULT_SIZE)

  const [detail, setDetail] = useState<SkillDetail | null>(null)
  const [detailOpen, setDetailOpen] = useState(false)

  const [editOpen, setEditOpen] = useState(false)
  const [editTarget, setEditTarget] = useState<string | null>(null)

  const [applyRecord, setApplyRecord] = useState<SkillListItem | null>(null)
  const [applying, setApplying] = useState(false)
  const [applyReason, setApplyReason] = useState('')

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const filters = { keywords, classifyId }
      setItems(tab === 'mine' ? await getMySkills(filters) : await getSkillMarketList(filters))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [tab, keywords, classifyId])

  useEffect(() => {
    void load()
  }, [load])

  useEffect(() => {
    classifyApi
      .getClassifies(ClassifyType.Skill)
      .then(setClassifies)
      .catch(() => {
        // 错误已由全局请求中间件统一提示
      })
  }, [])

  const handleTabChange = (key: string) => {
    // 切换 tab 重置筛选与分页，两块状态更新与路由跳转合并为一次渲染、一次加载
    setSearchText('')
    setKeywords(undefined)
    setClassifyId(undefined)
    setPage(1)
    navigate(key === 'mine' ? '/skills' : '/skill-market')
  }

  const applySearch = (value: string) => {
    setSearchText(value)
    setKeywords(value.trim() || undefined)
    setPage(1)
  }

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
    setApplying(true)
    try {
      await applyPublication({
        resourceType: 'skill',
        resourceId: String(applyRecord.id),
        applyReason: applyReason || undefined,
      })
      feedback.success(t('skills.applySuccess'))
      setApplyRecord(null)
      setApplyReason('')
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

  const pagedItems = useMemo(
    () => items.slice((page - 1) * pageSize, page * pageSize),
    [items, page, pageSize],
  )

  const tabItems = [
    { key: 'market', label: t('skills.tabMarket') },
    { key: 'mine', label: t('skills.tabMine') },
  ]

  return (
    <Page>
      <Tabs activeKey={tab} onChange={handleTabChange} items={tabItems} />
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          gap: spacing.sm,
          flexWrap: 'wrap',
          marginBottom: spacing.md,
        }}
      >
        {tab === 'mine' && (
          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={() => {
              setEditTarget(null)
              setEditOpen(true)
            }}
          >
            {t('skills.create')}
          </Button>
        )}
        <Input.Search
          value={searchText}
          onChange={(e) => setSearchText(e.target.value)}
          onSearch={applySearch}
          placeholder={t('skills.searchPlaceholder')}
          prefix={<SearchOutlined style={{ color: 'inherit' }} />}
          allowClear
          maxLength={100}
          style={{ width: 280 }}
        />
        <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
          {t('ds.table.refresh')}
        </Button>
      </div>
      <Space size={4} wrap style={{ marginBottom: spacing.md }}>
        <Tag.CheckableTag
          checked={classifyId === undefined}
          onChange={() => {
            setClassifyId(undefined)
            setPage(1)
          }}
        >
          {t('skills.categoryAll')}
        </Tag.CheckableTag>
        {classifies.map((c) => (
          <Tag.CheckableTag
            key={String(c.classifyId ?? '')}
            checked={classifyId === Number(c.classifyId)}
            onChange={() => {
              setClassifyId(Number(c.classifyId) || undefined)
              setPage(1)
            }}
          >
            {classifyLabel(c)}
          </Tag.CheckableTag>
        ))}
      </Space>
      <Spin spinning={loading}>
        {!loading && pagedItems.length === 0 ? (
          <Empty description={t('skills.empty')} />
        ) : (
          <Row gutter={[spacing.md, spacing.md]}>
            {pagedItems.map((record) => {
              const name = record.name || '-'
              return (
                <Col key={String(record.id ?? '')} xs={24} sm={12} md={8} lg={6} xl={4} xxl={4}>
                  <Card style={{ height: '100%' }} styles={{ body: { padding: spacing.md, display: 'flex', flexDirection: 'column', height: '100%' } }}>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: spacing.sm, height: '100%' }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: spacing.sm }}>
                        <Avatar
                          shape="square"
                          size={44}
                          src={record.avatarPath ? resolveStorageUrl(record.avatarPath) : undefined}
                          icon={!record.avatarPath ? <ThunderboltOutlined /> : undefined}
                        >
                          {!record.avatarPath ? null : name.slice(0, 1).toUpperCase()}
                        </Avatar>
                        <div style={{ minWidth: 0, flex: 1 }}>
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
                          <Text code style={{ fontSize: 12 }}>
                            {record.key}
                          </Text>
                        </div>
                        {record.isSystem ? <Tag color="geekblue">{t('skills.isSystem')}</Tag> : null}
                      </div>
                      <Paragraph type="secondary" style={{ fontSize: 13, marginBottom: 0, minHeight: 38 }} ellipsis={{ rows: 2 }}>
                        {record.description || '-'}
                      </Paragraph>
                      <div style={{ display: 'flex', alignItems: 'center', gap: spacing.sm, flexWrap: 'wrap' }}>
                        {tab === 'mine' && renderStatus(record)}
                        <Text type="secondary" style={{ fontSize: 12 }}>
                          {t('skills.colFileCount')} {record.fileCount ?? 0}
                        </Text>
                        {tab === 'market' && (
                          <Text type="secondary" style={{ fontSize: 12 }}>
                            {record.createUserName || '-'}
                          </Text>
                        )}
                      </div>
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
                        {tab === 'mine' ? (
                          <Space size={0}>
                            <Button type="text" size="small" icon={<EyeOutlined />} aria-label={`${t('skills.detail')}-${record.key ?? ''}`} onClick={() => void openDetail(record)} />
                            <Button
                              type="text"
                              size="small"
                              icon={<EditOutlined />}
                              aria-label={`${t('skills.edit')}-${record.key ?? ''}`}
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
                                  setApplyReason('')
                                }}
                              />
                            )}
                            <Button
                              type="text"
                              size="small"
                              icon={<DownloadOutlined />}
                              aria-label={`${t('skills.download')}-${record.key ?? ''}`}
                              onClick={() => void handleDownload(record)}
                            />
                            <Popconfirm title={t('skills.deleteConfirm')} onConfirm={() => void handleDelete(record)}>
                              <Button type="text" size="small" danger icon={<DeleteOutlined />} aria-label={`${t('skills.delete')}-${record.key ?? ''}`} />
                            </Popconfirm>
                          </Space>
                        ) : (
                          <Text type="secondary" style={{ fontSize: 12, whiteSpace: 'nowrap' }}>
                            {formatDateTime(record.updateTime)}
                          </Text>
                        )}
                        <Space size={spacing.sm}>
                          {tab === 'market' && (
                            <Button
                              type="text"
                              size="small"
                              icon={<DownloadOutlined />}
                              aria-label={`${t('skills.download')}-${record.key ?? ''}`}
                              onClick={() => void handleDownload(record)}
                            />
                          )}
                          <Button type="primary" size="small" icon={<EyeOutlined />} onClick={() => void openDetail(record)}>
                            {t('skills.view')}
                          </Button>
                        </Space>
                      </div>
                      {tab === 'mine' && (
                        <Text type="secondary" style={{ fontSize: 12 }}>
                          {t('skills.colUpdateTime')}: {formatDateTime(record.updateTime)}
                        </Text>
                      )}
                    </div>
                  </Card>
                </Col>
              )
            })}
          </Row>
        )}
      </Spin>
      <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: spacing.md }}>
        {items.length > 0 && (
          <Pagination
            current={page}
            pageSize={pageSize}
            total={items.length}
            onChange={(next, nextSize) => {
              setPage(nextSize !== pageSize ? 1 : next)
              setPageSize(nextSize)
            }}
            showSizeChanger
            pageSizeOptions={PAGE_SIZE_OPTIONS.map(String)}
          />
        )}
      </div>
      <SkillDetailModal open={detailOpen} detail={detail} onClose={() => setDetailOpen(false)} />
      <SkillEditModal
        open={editOpen}
        skillId={editTarget}
        teamId={0}
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
        <Form layout="vertical">
          <Form.Item label={t('skills.applyReason')} style={{ marginBottom: 0 }}>
            <Input.TextArea
              value={applyReason}
              onChange={(e) => setApplyReason(e.target.value)}
              placeholder={t('skills.applyReasonPlaceholder')}
              rows={3}
              maxLength={255}
            />
          </Form.Item>
        </Form>
      </Modal>
    </Page>
  )
}
