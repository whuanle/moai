import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import {
  CheckCircleOutlined,
  ReloadOutlined,
  SearchOutlined,
  StopOutlined,
  SwapOutlined,
} from '@ant-design/icons'
import { Avatar, Button, Input, Modal, Popconfirm, Select, Space, Tag, Tooltip, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { Navigate } from 'react-router'
import { DataTable, Page, PageToolbar, feedback } from '@/design-system'
import { controlHeight, spacing } from '@/design-system/theme'
import { useAppStore } from '@/store/app'
import { resolveStorageUrl } from '@/utils/storage'
import { formatDateTime } from '@/utils/datetime'
import { getUsers, type UserListItem } from '@/api/usermanage'
import {
  adminTransferTeamOwner,
  getAdminTeams,
  setTeamDisable,
  type AdminTeamItem,
} from '@/api/team'

const { Text } = Typography

const pageVerticalPadding = spacing.lg * 2
const toolbarHeight = controlHeight + spacing.lg
const tableHeaderHeight = 55
const tablePaginationHeight = 57
const adminTeamsPageHeight = `calc(100vh - ${pageVerticalPadding}px)`
const adminTeamsTableScrollY = `calc(100vh - ${pageVerticalPadding + toolbarHeight + tableHeaderHeight + tablePaginationHeight}px)`

/** 禁用状态筛选项 */
type StatusFilter = 'all' | 'normal' | 'disabled'

function toIsDisable(filter: StatusFilter): boolean | undefined {
  if (filter === 'normal') return false
  if (filter === 'disabled') return true
  return undefined
}

export function AdminTeams() {
  const { t } = useTranslation()
  const isAdmin = useAppStore((state) => state.userInfo?.isAdmin === true)

  const [loading, setLoading] = useState(true)
  const [items, setItems] = useState<AdminTeamItem[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [pageNo, setPageNo] = useState(1)
  const [pageSize, setPageSize] = useState(20)
  const [keyword, setKeyword] = useState('')
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('all')

  /** 转让负责人弹窗 */
  const [transferOpen, setTransferOpen] = useState(false)
  const [transferTarget, setTransferTarget] = useState<AdminTeamItem | null>(null)
  const [transferUserId, setTransferUserId] = useState<string | undefined>(undefined)
  const [userOptions, setUserOptions] = useState<UserListItem[]>([])
  const [userLoading, setUserLoading] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const searchTimer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)

  const load = useCallback(
    async (page = pageNo, size = pageSize, search = keyword, filter = statusFilter) => {
      setLoading(true)
      try {
        const res = await getAdminTeams({
          pageNo: page,
          pageSize: size,
          searchText: search.trim() || undefined,
          isDisable: toIsDisable(filter),
        })
        setItems(res.items ?? [])
        setTotalCount(res.totalCount ?? 0)
      } catch {
        // 错误已由全局请求中间件统一提示
      } finally {
        setLoading(false)
      }
    },
    [pageNo, pageSize, keyword, statusFilter],
  )

  useEffect(() => {
    if (isAdmin) {
      void load(1, pageSize, '', 'all')
    }
    // 首次进入按初始条件拉取，后续由交互触发
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isAdmin])

  useEffect(() => () => {
    if (searchTimer.current) clearTimeout(searchTimer.current)
  }, [])

  const handleSearch = () => {
    setPageNo(1)
    void load(1, pageSize, keyword, statusFilter)
  }

  const handleStatusChange = (value: StatusFilter) => {
    setStatusFilter(value)
    setPageNo(1)
    void load(1, pageSize, keyword, value)
  }

  /** 按关键字拉取候选用户（全站用户，供管理员选择新的负责人） */
  const loadUsers = useCallback(async (search: string) => {
    setUserLoading(true)
    try {
      const res = await getUsers({ pageNo: 1, pageSize: 20, searchText: search.trim() || undefined })
      setUserOptions(res.items ?? [])
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setUserLoading(false)
    }
  }, [])

  const handleUserSearch = (value: string) => {
    if (searchTimer.current) clearTimeout(searchTimer.current)
    searchTimer.current = setTimeout(() => void loadUsers(value), 300)
  }

  const openTransfer = (record: AdminTeamItem) => {
    setTransferTarget(record)
    setTransferUserId(undefined)
    setTransferOpen(true)
    void loadUsers('')
  }

  const handleTransfer = async () => {
    if (!transferTarget || !transferUserId) return
    setSubmitting(true)
    try {
      await adminTransferTeamOwner(Number(transferTarget.teamId), Number(transferUserId))
      feedback.success(t('adminTeams.transferSuccess'))
      setTransferOpen(false)
      void load(pageNo, pageSize, keyword, statusFilter)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSubmitting(false)
    }
  }

  const handleToggleDisable = async (record: AdminTeamItem) => {
    try {
      await setTeamDisable(Number(record.teamId), !record.isDisable)
      feedback.success(t(record.isDisable ? 'adminTeams.enableSuccess' : 'adminTeams.disableSuccess'))
      void load(pageNo, pageSize, keyword, statusFilter)
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const renderTeam = (record: AdminTeamItem) => {
    const name = record.name || '-'
    const description = record.description?.trim()
    return (
      <Space>
        <Avatar size={36} src={resolveStorageUrl(record.avatar ?? null) || undefined} alt={name}>
          {name.slice(0, 1).toUpperCase()}
        </Avatar>
        <div style={{ lineHeight: 1.4, minWidth: 0 }}>
          <div>{name}</div>
          {description && (
            <Text type="secondary" style={{ fontSize: 12 }}>
              {description}
            </Text>
          )}
        </div>
      </Space>
    )
  }

  const renderOwner = (record: AdminTeamItem) => {
    if (!record.ownerUserId) return <Text type="secondary">-</Text>
    const displayName = record.ownerNickName || record.ownerUserName || '-'
    return (
      <Space>
        <Avatar size={28} src={resolveStorageUrl(record.ownerAvatar ?? null) || undefined} alt={displayName}>
          {displayName.slice(0, 1).toUpperCase()}
        </Avatar>
        <div style={{ lineHeight: 1.4, minWidth: 0 }}>
          <div>{displayName}</div>
          {record.ownerNickName && record.ownerNickName !== record.ownerUserName && (
            <Text type="secondary" style={{ fontSize: 12 }}>
              {record.ownerUserName}
            </Text>
          )}
        </div>
      </Space>
    )
  }

  const columns: TableColumnsType<AdminTeamItem> = useMemo(
    () => [
      { title: t('adminTeams.colTeam'), key: 'team', width: 240, render: (_, record) => renderTeam(record) },
      { title: t('adminTeams.colOwner'), key: 'owner', width: 190, render: (_, record) => renderOwner(record) },
      { title: t('adminTeams.colMemberCount'), dataIndex: 'memberCount', width: 100, render: (v: number | null) => v ?? 0 },
      {
        title: t('adminTeams.colStatus'),
        key: 'status',
        width: 90,
        render: (_, record) =>
          record.isDisable ? (
            <Tag color="error">{t('adminTeams.statusDisabled')}</Tag>
          ) : (
            <Tag color="success">{t('adminTeams.statusNormal')}</Tag>
          ),
      },
      {
        title: t('adminTeams.colCreateTime'),
        dataIndex: 'createTime',
        width: 150,
        render: (v: string | null) => <span style={{ whiteSpace: 'nowrap' }}>{formatDateTime(v)}</span>,
      },
      {
        title: t('adminTeams.colActions'),
        key: 'actions',
        width: 110,
        fixed: 'right',
        render: (_, record) => (
          <Space size={0}>
            <Tooltip title={t('adminTeams.transferOwner')}>
              <Button
                type="text"
                size="small"
                icon={<SwapOutlined />}
                aria-label={t('adminTeams.transferOwner')}
                onClick={() => openTransfer(record)}
              />
            </Tooltip>
            <Popconfirm
              title={t(record.isDisable ? 'adminTeams.enableConfirm' : 'adminTeams.disableConfirm')}
              okButtonProps={{ danger: !record.isDisable }}
              onConfirm={() => void handleToggleDisable(record)}
            >
              <Tooltip title={t(record.isDisable ? 'adminTeams.enable' : 'adminTeams.disable')}>
                <Button
                  type="text"
                  size="small"
                  danger={!record.isDisable}
                  icon={record.isDisable ? <CheckCircleOutlined /> : <StopOutlined />}
                  aria-label={t(record.isDisable ? 'adminTeams.enable' : 'adminTeams.disable')}
                />
              </Tooltip>
            </Popconfirm>
          </Space>
        ),
      },
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, keyword, pageNo, pageSize, statusFilter],
  )

  /** 当前负责人不允许被再次选为目标 */
  const transferOptions = useMemo(
    () =>
      userOptions
        .filter((u) => String(u.id) !== String(transferTarget?.ownerUserId ?? ''))
        .map((u) => ({
          label: u.nickName ? `${u.nickName} (${u.userName ?? ''})` : String(u.userName ?? ''),
          value: String(u.id),
        })),
    [userOptions, transferTarget],
  )

  if (!isAdmin) {
    return <Navigate to="/dashboard" replace />
  }

  return (
    <Page>
      <div style={{ display: 'flex', flexDirection: 'column', height: adminTeamsPageHeight, minHeight: 0 }}>
        <PageToolbar
          filters={
            <Space size={spacing.xs} wrap>
              <Input
                value={keyword}
                onChange={(e) => setKeyword(e.target.value)}
                onPressEnter={() => handleSearch()}
                allowClear
                maxLength={50}
                prefix={<SearchOutlined style={{ color: 'inherit' }} />}
                placeholder={t('adminTeams.searchPlaceholder')}
                style={{ width: 260 }}
              />
              <Select<StatusFilter>
                value={statusFilter}
                onChange={handleStatusChange}
                style={{ width: 140 }}
                options={[
                  { label: t('adminTeams.statusAll'), value: 'all' },
                  { label: t('adminTeams.statusNormal'), value: 'normal' },
                  { label: t('adminTeams.statusDisabled'), value: 'disabled' },
                ]}
              />
              <Button type="primary" icon={<SearchOutlined />} onClick={() => handleSearch()}>
                {t('adminTeams.search')}
              </Button>
            </Space>
          }
          actions={
            <Tooltip title={t('adminTeams.refresh')}>
              <Button
                icon={<ReloadOutlined />}
                aria-label={t('adminTeams.refresh')}
                onClick={() => void load(pageNo, pageSize, keyword, statusFilter)}
              />
            </Tooltip>
          }
        />
        <div style={{ flex: 1, minHeight: 0 }}>
          <DataTable<AdminTeamItem>
            rowKey="teamId"
            columns={columns}
            dataSource={items}
            loading={loading}
            sticky
            scroll={{ x: 900, y: adminTeamsTableScrollY }}
            pagination={{
              current: pageNo,
              pageSize,
              total: totalCount,
              showSizeChanger: true,
              onChange: (page, size) => {
                setPageNo(page)
                setPageSize(size)
                void load(page, size, keyword, statusFilter)
              },
            }}
          />
        </div>
      </div>
      <Modal
        open={transferOpen}
        title={t('adminTeams.transferTitle', { name: transferTarget?.name ?? '' })}
        onCancel={() => setTransferOpen(false)}
        confirmLoading={submitting}
        destroyOnHidden
        maskClosable={false}
        footer={[
          <Button key="cancel" onClick={() => setTransferOpen(false)}>
            {t('adminTeams.cancel')}
          </Button>,
          <Popconfirm
            key="confirm"
            title={t('adminTeams.transferConfirm')}
            okButtonProps={{ danger: true }}
            onConfirm={() => void handleTransfer()}
          >
            <Button type="primary" danger disabled={!transferUserId} loading={submitting}>
              {t('adminTeams.transferConfirmButton')}
            </Button>
          </Popconfirm>,
        ]}
      >
        <Select
          showSearch
          value={transferUserId}
          onChange={(value: string) => setTransferUserId(value)}
          onSearch={handleUserSearch}
          filterOption={false}
          loading={userLoading}
          options={transferOptions}
          placeholder={t('adminTeams.transferUserPlaceholder')}
          style={{ width: '100%' }}
          notFoundContent={userLoading ? null : t('adminTeams.transferUserEmpty')}
        />
        <Text type="secondary" style={{ display: 'block', marginTop: spacing.xs, fontSize: 12 }}>
          {t('adminTeams.transferHint')}
        </Text>
      </Modal>
    </Page>
  )
}
