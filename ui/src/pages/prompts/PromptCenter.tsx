import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  DeleteOutlined,
  EditOutlined,
  EyeOutlined,
  FireOutlined,
  ReloadOutlined,
  SearchOutlined,
  SendOutlined,
  UndoOutlined,
} from '@ant-design/icons'
import { Avatar, Button, Col, Empty, Form, Input, Modal, Pagination, Popconfirm, Row, Space, Spin, Tabs, Tag, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { useLocation, useNavigate } from 'react-router'
import { Card, Page, feedback } from '@/design-system'
import { neutralColors, spacing } from '@/design-system/theme'
import { formatDateTime } from '@/utils/datetime'
import { resolveStorageUrl } from '@/utils/storage'
import { classifyApi, ClassifyType, type Classify } from '@/api/classify'
import { applyPublication, withdrawPublication } from '@/api/publication'
import {
  deletePrompt,
  getMyPrompts,
  getPromptDetail,
  getPromptMarketList,
  type PromptDetail,
  type PromptItem,
} from '@/api/prompt'
import { PromptDetailModal } from './PromptDetailModal'

const { Text, Paragraph } = Typography

type PromptTab = 'market' | 'mine'

const PAGE_DEFAULT_SIZE = 12
const PAGE_SIZE_OPTIONS = [12, 24, 48]

/** 提示词中心：菜单单一入口，页头 Tab 切换「提示词市场 / 我的提示词」，分类列表 + 卡片分页展示 */
export function PromptCenter() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const location = useLocation()
  const tab: PromptTab = location.pathname.startsWith('/prompts') ? 'mine' : 'market'

  const [loading, setLoading] = useState(false)
  const [items, setItems] = useState<PromptItem[]>([])
  const [classifies, setClassifies] = useState<Classify[]>([])
  const [searchText, setSearchText] = useState('')
  const [keywords, setKeywords] = useState<string | undefined>(undefined)
  const [promptClassId, setPromptClassId] = useState<number | undefined>(undefined)
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(PAGE_DEFAULT_SIZE)
  const [detail, setDetail] = useState<PromptDetail | null>(null)
  const [detailOpen, setDetailOpen] = useState(false)
  const [applyRecord, setApplyRecord] = useState<PromptItem | null>(null)
  const [applying, setApplying] = useState(false)
  const [applyReason, setApplyReason] = useState('')

  const classOptions = useMemo(
    () => classifies.map((c) => ({ value: Number(c.classifyId), label: c.name ?? '' })),
    [classifies],
  )
  const classNameMap = useMemo(
    () => new Map(classifies.map((c) => [Number(c.classifyId), c.name ?? ''])),
    [classifies],
  )

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const filters = { keywords, promptClassId }
      setItems(tab === 'mine' ? await getMyPrompts(filters) : await getPromptMarketList(filters))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [tab, keywords, promptClassId])

  useEffect(() => {
    classifyApi
      .getClassifies(ClassifyType.Prompt)
      .then(setClassifies)
      .catch(() => {
        // 错误已由全局请求中间件统一提示
      })
  }, [])

  useEffect(() => {
    void load()
  }, [load])

  const handleTabChange = (key: string) => {
    // 切换 tab 重置筛选与分页，两块状态更新与路由跳转合并为一次渲染、一次加载
    setSearchText('')
    setKeywords(undefined)
    setPromptClassId(undefined)
    setPage(1)
    navigate(key === 'mine' ? '/prompts' : '/prompt-market')
  }

  const applySearch = (value: string) => {
    setSearchText(value)
    setKeywords(value.trim() || undefined)
    setPage(1)
  }

  const openDetail = async (record: PromptItem) => {
    try {
      const detailRes = await getPromptDetail(Number(record.promptId))
      setDetail(detailRes ?? null)
      setDetailOpen(true)
      // 市场提示词查看详情会计数，重拉列表刷新使用次数
      if (tab === 'market') void load()
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
    setApplying(true)
    try {
      await applyPublication({
        resourceType: 'prompt',
        resourceId: String(applyRecord.promptId),
        applyReason: applyReason || undefined,
      })
      feedback.success(t('prompt.applySuccess'))
      setApplyRecord(null)
      setApplyReason('')
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

  const renderMineActions = (record: PromptItem) => (
    <Space size={0}>
      <Button type="text" size="small" icon={<EyeOutlined />} aria-label={t('prompt.view')} onClick={() => void openDetail(record)} />
      <Button
        type="text"
        size="small"
        icon={<EditOutlined />}
        aria-label={t('prompt.edit')}
        onClick={() => navigate(`/prompts/${record.promptId}/edit`)}
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
            setApplyReason('')
          }}
        />
      )}
      <Popconfirm title={t('prompt.deleteConfirm')} onConfirm={() => void handleDelete(record)}>
        <Button type="text" size="small" danger icon={<DeleteOutlined />} aria-label={t('prompt.delete')} />
      </Popconfirm>
    </Space>
  )

  const pagedItems = useMemo(
    () => items.slice((page - 1) * pageSize, page * pageSize),
    [items, page, pageSize],
  )

  const tabItems = [
    { key: 'market', label: t('prompt.tabMarket') },
    { key: 'mine', label: t('prompt.tabMine') },
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
          <Button type="primary" onClick={() => navigate('/prompts/new')}>
            {t('prompt.create')}
          </Button>
        )}
        <Input.Search
          value={searchText}
          onChange={(e) => setSearchText(e.target.value)}
          onSearch={applySearch}
          placeholder={t('prompt.searchPlaceholder')}
          prefix={<SearchOutlined style={{ color: 'inherit' }} />}
          allowClear
          maxLength={100}
          style={{ width: 280 }}
        />
        <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
          {t('ds.table.refresh')}
        </Button>
        <Text type="secondary">{t('ds.table.total', { total: items.length })}</Text>
      </div>
      <Space size={4} wrap style={{ marginBottom: spacing.md }}>
        <Tag.CheckableTag checked={promptClassId === undefined} onChange={() => { setPromptClassId(undefined); setPage(1) }}>
          {t('prompt.categoryAll')}
        </Tag.CheckableTag>
        {classOptions.map((option) => (
          <Tag.CheckableTag
            key={option.value}
            checked={promptClassId === option.value}
            onChange={() => { setPromptClassId(option.value); setPage(1) }}
          >
            {option.label}
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
                <Col key={String(record.promptId ?? '')} xs={24} sm={12} md={8} lg={6}>
                  <Card style={{ height: '100%' }} styles={{ body: { padding: spacing.md, display: 'flex', flexDirection: 'column', height: '100%' } }}>
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
                          {tab === 'mine' && <div style={{ marginTop: 2 }}>{renderStatus(record)}</div>}
                        </div>
                      </div>
                      <Paragraph type="secondary" style={{ fontSize: 13, marginBottom: 0, minHeight: 38 }} ellipsis={{ rows: 2 }}>
                        {record.description || '-'}
                      </Paragraph>
                      <div style={{ display: 'flex', alignItems: 'center', gap: spacing.sm, flexWrap: 'wrap' }}>
                        {renderCategory(record)}
                        <Text type="secondary" style={{ fontSize: 12 }}>
                          <FireOutlined /> {t('prompt.useCount', { count: record.counter ?? 0 })}
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
                          borderTop: `1px solid ${neutralColors.border}`,
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'space-between',
                          gap: spacing.sm,
                        }}
                      >
                        {tab === 'mine' ? (
                          renderMineActions(record)
                        ) : (
                          <Text type="secondary" style={{ fontSize: 12, whiteSpace: 'nowrap' }}>
                            {formatDateTime(record.updateTime)}
                          </Text>
                        )}
                        {tab === 'market' ? (
                          <Button type="primary" size="small" icon={<EyeOutlined />} onClick={() => void openDetail(record)}>
                            {t('prompt.use')}
                          </Button>
                        ) : (
                          <Text type="secondary" style={{ fontSize: 12, whiteSpace: 'nowrap' }}>
                            {formatDateTime(record.updateTime)}
                          </Text>
                        )}
                      </div>
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
            showTotal={(total) => t('ds.table.total', { total })}
          />
        )}
      </div>
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
        <Form layout="vertical">
          <Form.Item label={t('prompt.applyReason')} style={{ marginBottom: 0 }}>
            <Input.TextArea
              value={applyReason}
              onChange={(e) => setApplyReason(e.target.value)}
              placeholder={t('prompt.applyReasonPlaceholder')}
              rows={3}
              maxLength={255}
            />
          </Form.Item>
        </Form>
      </Modal>
    </Page>
  )
}
