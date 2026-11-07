import { useCallback, useEffect, useMemo, useState } from 'react'
import { DeleteOutlined, EditOutlined, EyeOutlined, SearchOutlined } from '@ant-design/icons'
import { Avatar, Button, Col, Empty, Form, Input, Pagination, Popconfirm, Row, Space, Spin, Switch, Tag, Tooltip, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { Card, QueryBar, feedback, useNeutralColors } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { formatDateTime } from '@/utils/datetime'
import { resolveStorageUrl } from '@/utils/storage'
import { deleteTeamAgent, getTeamAgent, getTeamAgents, setTeamAgentEnabled, type TeamAgentDetail, type TeamAgentListItem } from '@/api/team-agent'
import { TeamAgentDetailModal } from './TeamAgentDetailModal'

const CARD_PAGE_DEFAULT_SIZE = 12
const CARD_PAGE_SIZE_OPTIONS = [12, 24, 48]

interface TeamAgentFilters extends Record<string, unknown> {
  searchText?: string
}

/**
 * 团队智能体：主智能体 + 多个子智能体的编排定义资产。
 * 团队成员可查看，Owner/Admin 可创建管理；卡片列表（同团队提示词），编辑走独立页；
 * 系统内置智能体（isSystem）不可删除，可编辑。
 */
export function TeamAgents({ teamId, canManage }: { teamId: number; canManage: boolean }) {
  const { t } = useTranslation()
  const neutral = useNeutralColors()
  const navigate = useNavigate()

  const [loading, setLoading] = useState(false)
  const [items, setItems] = useState<TeamAgentListItem[]>([])
  const [searchText, setSearchText] = useState<string | undefined>(undefined)
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(CARD_PAGE_DEFAULT_SIZE)
  const [detail, setDetail] = useState<TeamAgentDetail | null>(null)
  const [detailOpen, setDetailOpen] = useState(false)
  const [filterForm] = Form.useForm<TeamAgentFilters>()

  const load = useCallback(async () => {
    if (!teamId) return
    setLoading(true)
    try {
      setItems((await getTeamAgents(teamId, { searchText })).items)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [teamId, searchText])

  useEffect(() => {
    void load()
  }, [load])

  const openDetail = async (record: TeamAgentListItem) => {
    try {
      setDetail(await getTeamAgent(record.id ?? ''))
      setDetailOpen(true)
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleDelete = async (record: TeamAgentListItem) => {
    try {
      await deleteTeamAgent(record.id ?? '')
      feedback.success(t('teamAgents.deleteSuccess'))
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  /** 卡片直切启停：乐观更新本地状态，失败回滚（错误由全局中间件提示） */
  const handleEnabledChange = async (record: TeamAgentListItem, enabled: boolean) => {
    const previous = items
    setItems((list) => list.map((x) => (x.id === record.id ? { ...x, enabled } : x)))
    try {
      await setTeamAgentEnabled(record.id ?? '', enabled)
    } catch {
      setItems(previous)
    }
  }

  const applyFilters = () => {
    const values = filterForm.getFieldsValue()
    setSearchText(typeof values.searchText === 'string' && values.searchText.trim() ? values.searchText.trim() : undefined)
    setPage(1)
  }

  /** 查看/编辑/删除操作，卡片底栏使用（紧凑卡片场景：纯图标 + Tooltip + aria-label） */
  const renderActions = (record: TeamAgentListItem) => (
    <Space size={0}>
      <Tooltip title={t('teamAgents.detail')}>
        <Button type="text" size="small" icon={<EyeOutlined />} aria-label={t('teamAgents.detail')} onClick={() => void openDetail(record)} />
      </Tooltip>
      <Tooltip title={t('teamAgents.edit')}>
        <Button
          type="text"
          size="small"
          icon={<EditOutlined />}
          aria-label={t('teamAgents.edit')}
          onClick={() => navigate(`/team/${teamId}/agent/${record.id}/edit`)}
        />
      </Tooltip>
      {!record.isSystem && (
        <Popconfirm title={t('teamAgents.deleteConfirm')} onConfirm={() => void handleDelete(record)}>
          <Tooltip title={t('teamAgents.delete')}>
            <Button type="text" size="small" danger icon={<DeleteOutlined />} aria-label={t('teamAgents.delete')} />
          </Tooltip>
        </Popconfirm>
      )}
    </Space>
  )

  const total = items.length
  const cardPageCount = Math.max(1, Math.ceil(total / pageSize))
  const safePage = Math.min(page, cardPageCount)
  const pagedItems = useMemo(
    () => items.slice((safePage - 1) * pageSize, safePage * pageSize),
    [items, safePage, pageSize],
  )

  return (
    <>
      {/* 工具栏一行靠左：搜索框 → 查询 → | → 新建智能体；仅一个搜索框不传 onReset（规范 §7），查询按钮自带刷新语义不另设刷新 */}
      <QueryBar
        form={filterForm}
        onSearch={applyFilters}
        loading={loading}
        extra={
          canManage ? (
            <Button type="primary" onClick={() => navigate(`/team/${teamId}/agent/new`)}>
              {t('teamAgents.create')}
            </Button>
          ) : undefined
        }
      >
        <Form.Item name="searchText">
          <Input
            placeholder={t('teamAgents.searchPlaceholder')}
            prefix={<SearchOutlined style={{ color: 'inherit' }} />}
            allowClear
            maxLength={100}
            style={{ width: 280 }}
          />
        </Form.Item>
      </QueryBar>
      <Spin spinning={loading}>
        {pagedItems.length === 0 ? (
          <Empty description={t('teamAgents.empty')} />
        ) : (
          <Row gutter={[spacing.md, spacing.md]}>
            {pagedItems.map((record) => {
              const name = record.name || '-'
              return (
                <Col key={String(record.id ?? '')} xs={24} sm={12} md={8} lg={6} xl={4} xxl={4}>
                  <Card
                    style={{ height: '100%' }}
                    styles={{ body: { padding: spacing.md, display: 'flex', flexDirection: 'column', height: '100%' } }}
                  >
                    <div style={{ display: 'flex', flexDirection: 'column', gap: spacing.sm, height: '100%' }}>
                      <div style={{ display: 'flex', alignItems: 'flex-start', gap: spacing.sm }}>
                        <Avatar shape="square" size={44} src={record.avatarObjectKey ? resolveStorageUrl(record.avatarObjectKey) : undefined} alt={name}>
                          {name.slice(0, 1).toUpperCase()}
                        </Avatar>
                        <div style={{ minWidth: 0, flex: 1, alignSelf: 'center' }}>
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
                        </div>
                        <div style={{ display: 'flex', alignItems: 'center', gap: 4, flexShrink: 0, marginTop: 2 }}>
                          {canManage ? (
                            <Switch
                              size="small"
                              checked={record.enabled ?? false}
                              checkedChildren={t('teamAgents.statusEnabled')}
                              unCheckedChildren={t('teamAgents.statusDisabled')}
                              onChange={(checked) => void handleEnabledChange(record, checked)}
                            />
                          ) : record.enabled ? (
                            <Tag color="success">{t('teamAgents.statusEnabled')}</Tag>
                          ) : (
                            <Tag>{t('teamAgents.statusDisabled')}</Tag>
                          )}
                          {record.isSystem && <Tag color="purple">{t('teamAgents.builtin')}</Tag>}
                        </div>
                      </div>
                      <Typography.Paragraph type="secondary" style={{ fontSize: 13, marginBottom: 0, minHeight: 38 }} ellipsis={{ rows: 2 }}>
                        {record.description || '-'}
                      </Typography.Paragraph>
                      <div>{<Tag>{t('teamAgents.subCountBadge', { count: record.subCount ?? 0 })}</Tag>}</div>
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
                          <Button type="text" size="small" icon={<EyeOutlined />} onClick={() => void openDetail(record)}>
                            {t('teamAgents.detail')}
                          </Button>
                        )}
                        <span title={t('teamAgents.colUpdate')} style={{ whiteSpace: 'nowrap', fontSize: 12, color: neutral.textSecondary }}>
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
      <TeamAgentDetailModal
        open={detailOpen}
        detail={detail}
        teamId={teamId}
        onClose={() => {
          setDetailOpen(false)
          setDetail(null)
        }}
      />
    </>
  )
}
