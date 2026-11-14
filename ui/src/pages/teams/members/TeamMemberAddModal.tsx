import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import type { Key, ReactNode } from 'react'
import { Avatar, Checkbox, Empty, Input, Modal, Space, Spin, Tag, Tree, Typography } from 'antd'
import type { DataNode } from 'antd/es/tree'
import { SearchOutlined, TeamOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { useNeutralColors } from '@/design-system/theme/useNeutralColors'
import { resolveStorageUrl } from '@/utils/storage'
import {
  addTeamUsers,
  getTeamCandidates,
  getTeamOrgRoleUsers,
  getTeamOrgRoles,
  type AddTeamUsersResult,
  type TeamCandidateItem,
} from '@/api/team'
import type { OrgRoleItem } from '@/api/org'

const { Text } = Typography

/** 虚拟根节点 key（顶级组织挂其下，与角色组织管理面板一致） */
const ROOT_NODE_KEY = '__root__'

/** 右栏可选用户行（组织用户与全站搜索结果统一形态） */
interface PickableUser {
  userId: string
  userName: string
  nickName: string
  avatar: string
}

interface TeamMemberAddModalProps {
  open: boolean
  teamId: number
  /** 团队现有成员 userId 集合（字符串化 long），命中则标记「已是成员」并禁选 */
  memberUserIds: Set<string>
  onCancel: () => void
  /** 提交成功后回调（父组件负责关闭弹窗并刷新成员列表） */
  onSuccess: (result: AddTeamUsersResult) => void
}

/**
 * 添加成员弹窗：左侧组织架构树（团队侧只读视图）按组织批量选人，
 * 顶部关键字实时搜索全站用户（已入团成员被候选接口排除）。
 */
export function TeamMemberAddModal({ open, teamId, memberUserIds, onCancel, onSuccess }: TeamMemberAddModalProps) {
  const { t } = useTranslation()
  const neutral = useNeutralColors()

  const [orgRoles, setOrgRoles] = useState<OrgRoleItem[]>([])
  const [orgLoading, setOrgLoading] = useState(false)
  const [expandedKeys, setExpandedKeys] = useState<Key[]>([])
  const [selectedRoleId, setSelectedRoleId] = useState<number | null>(null)

  const [orgUsers, setOrgUsers] = useState<PickableUser[]>([])
  const [orgUsersLoading, setOrgUsersLoading] = useState(false)

  const [keyword, setKeyword] = useState('')
  const [searchResults, setSearchResults] = useState<PickableUser[]>([])
  const [searching, setSearching] = useState(false)

  const [selectedUsers, setSelectedUsers] = useState<PickableUser[]>([])
  const [submitting, setSubmitting] = useState(false)
  const searchTimer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)

  /** 关键字非空时右栏切换为全站搜索结果，清空后回落到选中组织的成员 */
  const searchingMode = keyword.trim().length > 0
  const listItems = searchingMode ? searchResults : orgUsers
  const listLoading = searchingMode ? searching : orgUsersLoading

  const loadOrgRoles = useCallback(async () => {
    setOrgLoading(true)
    try {
      const list = await getTeamOrgRoles(teamId)
      setOrgRoles(list)
      setExpandedKeys([ROOT_NODE_KEY, ...list.map((x) => x.orgRoleId ?? 0)])
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setOrgLoading(false)
    }
  }, [teamId])

  const loadOrgUsers = useCallback(async (roleId: number) => {
    setOrgUsersLoading(true)
    try {
      const items = await getTeamOrgRoleUsers(teamId, roleId)
      setOrgUsers(
        items.map((x) => ({
          userId: String(x.userId ?? ''),
          userName: x.userName ?? '',
          nickName: x.nickName ?? '',
          avatar: x.avatar ?? '',
        })),
      )
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setOrgUsersLoading(false)
    }
  }, [teamId])

  const searchUsers = useCallback(async (text: string) => {
    setSearching(true)
    try {
      const items: TeamCandidateItem[] = await getTeamCandidates(teamId, text)
      setSearchResults(
        items.map((x) => ({
          userId: String(x.userId ?? ''),
          userName: x.userName ?? '',
          nickName: x.nickName ?? '',
          avatar: x.avatar ?? '',
        })),
      )
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSearching(false)
    }
  }, [teamId])

  /** 打开时重置选择并加载组织树；关闭时由父组件收起，状态留待下次打开重置 */
  useEffect(() => {
    if (!open) return undefined
    setSelectedUsers([])
    setSelectedRoleId(null)
    setOrgUsers([])
    setKeyword('')
    setSearchResults([])
    void loadOrgRoles()
    return undefined
  }, [open, loadOrgRoles])

  /** 选中组织变化时加载该组织成员（搜索态下仅切换左侧选中，不打断搜索结果） */
  useEffect(() => {
    if (!open || selectedRoleId == null) return
    void loadOrgUsers(selectedRoleId)
  }, [open, selectedRoleId, loadOrgUsers])

  useEffect(() => () => {
    if (searchTimer.current) clearTimeout(searchTimer.current)
  }, [])

  /** 输入即搜（300ms 防抖），清空关键字立即回落组织视图 */
  const handleKeywordChange = (value: string) => {
    setKeyword(value)
    if (searchTimer.current) clearTimeout(searchTimer.current)
    const text = value.trim()
    if (!text) {
      setSearching(false)
      setSearchResults([])
      return
    }
    searchTimer.current = setTimeout(() => void searchUsers(text), 300)
  }

  const isSelected = (user: PickableUser) => selectedUsers.some((x) => x.userId === user.userId)
  const isMember = (user: PickableUser) => memberUserIds.has(user.userId)

  const toggleUser = (user: PickableUser) => {
    setSelectedUsers((prev) => (prev.some((x) => x.userId === user.userId) ? prev.filter((x) => x.userId !== user.userId) : [...prev, user]))
  }

  /** 全选当前右栏可选项（已是成员的行不参与） */
  const selectableItems = useMemo(() => listItems.filter((x) => !memberUserIds.has(x.userId)), [listItems, memberUserIds])
  const allChecked = selectableItems.length > 0 && selectableItems.every((x) => isSelected(x))
  const someChecked = selectableItems.some((x) => isSelected(x))

  const toggleAll = () => {
    if (allChecked) {
      const ids = new Set(selectableItems.map((x) => x.userId))
      setSelectedUsers((prev) => prev.filter((x) => !ids.has(x.userId)))
      return
    }
    setSelectedUsers((prev) => [...prev, ...selectableItems.filter((x) => !prev.some((p) => p.userId === x.userId))])
  }

  const handleSubmit = async () => {
    if (selectedUsers.length === 0) return
    setSubmitting(true)
    try {
      const result = await addTeamUsers(
        teamId,
        selectedUsers.map((u) => Number(u.userId)),
      )
      if (result.addedCount > 0) feedback.success(t('team.batchAddSuccess', { count: result.addedCount }))
      if (result.skippedUserIds.length > 0) feedback.warning(t('team.batchAddSkipped', { count: result.skippedUserIds.length }))
      if (result.notFoundUserIds.length > 0) feedback.warning(t('team.batchAddNotFound', { count: result.notFoundUserIds.length }))
      onSuccess(result)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSubmitting(false)
    }
  }

  const renderUserLabel = (user: PickableUser): ReactNode => (
    <span style={{ display: 'inline-flex', alignItems: 'center', gap: spacing.sm, minWidth: 0 }}>
      <Avatar size={26} src={resolveStorageUrl(user.avatar) || undefined}>
        {(user.userName || '?').slice(0, 1).toUpperCase()}
      </Avatar>
      <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
        {user.userName || '-'}
        {user.nickName && user.nickName !== user.userName && (
          <Text type="secondary" style={{ fontSize: 12, marginLeft: spacing.xs }}>
            {user.nickName}
          </Text>
        )}
      </span>
    </span>
  )

  /** 左侧组织树（虚拟根不可选），节点带直属人数；数据与角色组织管理面板同构 */
  const treeData = useMemo<DataNode[]>(() => {
    interface BuildNode {
      key: number
      title: ReactNode
      children: BuildNode[]
    }

    const nodes = new Map<number, BuildNode>()
    const roots: BuildNode[] = []
    orgRoles.forEach((r) => {
      const node: BuildNode = {
        key: r.orgRoleId ?? 0,
        title: (
          <span style={{ display: 'flex', alignItems: 'center', gap: spacing.xs, minWidth: 0 }}>
            <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{r.name ?? '-'}</span>
            <Tag style={{ marginInlineEnd: 0 }}>{r.userCount ?? 0}</Tag>
          </span>
        ),
        children: [],
      }
      nodes.set(r.orgRoleId ?? 0, node)
      const parent = r.parentId != null ? nodes.get(r.parentId) : undefined
      if (parent && parent !== node) {
        parent.children.push(node)
      } else {
        roots.push(node)
      }
    })

    const toDataNode = (node: BuildNode): DataNode => ({
      key: node.key,
      title: node.title,
      children: node.children.length > 0 ? node.children.map(toDataNode) : undefined,
    })

    return [
      {
        key: ROOT_NODE_KEY,
        selectable: false,
        title: (
          <span style={{ display: 'inline-flex', alignItems: 'center', gap: spacing.xs }}>
            <TeamOutlined />
            <Text strong>{t('team.orgPanelTitle')}</Text>
          </span>
        ),
        children: roots.map(toDataNode),
      },
    ]
  }, [orgRoles, t])

  const selectedRole = orgRoles.find((r) => r.orgRoleId === selectedRoleId) ?? null

  return (
    <Modal
      open={open}
      title={t('team.addMember')}
      width={780}
      onCancel={onCancel}
      onOk={() => void handleSubmit()}
      okText={t('team.confirm')}
      cancelText={t('team.cancel')}
      okButtonProps={{ disabled: selectedUsers.length === 0 }}
      confirmLoading={submitting}
      maskClosable={false}
    >
      <Space direction="vertical" size={spacing.sm} style={{ width: '100%' }}>
        <Input
          value={keyword}
          onChange={(e) => handleKeywordChange(e.target.value)}
          allowClear
          maxLength={50}
          prefix={<SearchOutlined style={{ color: 'inherit' }} />}
          placeholder={t('team.searchUserPlaceholder')}
        />
        <Text type="secondary" style={{ fontSize: 12 }}>
          {t('team.candidateSearchTip')}
        </Text>
        <div style={{ display: 'flex', gap: spacing.md, minHeight: 0 }}>
          {/* 左栏：组织架构树 */}
          <div
            style={{
              width: 240,
              minWidth: 240,
              height: 380,
              overflow: 'auto',
              border: `1px solid ${neutral.border}`,
              borderRadius: 8,
              padding: spacing.sm,
            }}
          >
            {orgLoading ? (
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '100%' }}>
                <Spin />
              </div>
            ) : orgRoles.length === 0 ? (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('team.orgEmpty')} />
            ) : (
              <Tree
                blockNode
                showIcon={false}
                treeData={treeData}
                expandedKeys={expandedKeys}
                onExpand={(keys) => setExpandedKeys(keys)}
                selectedKeys={selectedRoleId != null ? [selectedRoleId] : []}
                onSelect={(keys) => {
                  const key = keys.length > 0 ? Number(keys[0]) : null
                  setSelectedRoleId(key != null && !Number.isNaN(key) ? key : null)
                  if (!searchingMode) setOrgUsers([])
                }}
              />
            )}
          </div>
          {/* 右栏：可选用户列表（选中组织成员 或 全站搜索结果） */}
          <div
            style={{
              flex: 1,
              minWidth: 0,
              height: 380,
              overflow: 'auto',
              border: `1px solid ${neutral.border}`,
              borderRadius: 8,
              padding: spacing.sm,
            }}
          >
            {searchingMode ? (
              <div style={{ marginBottom: spacing.xs }}>
                <Checkbox checked={allChecked} indeterminate={someChecked && !allChecked} disabled={selectableItems.length === 0} onChange={toggleAll}>
                  {t('team.selectCurrentList')}
                </Checkbox>
              </div>
            ) : selectedRole ? (
              <div style={{ marginBottom: spacing.xs, display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: spacing.sm }}>
                <Checkbox checked={allChecked} indeterminate={someChecked && !allChecked} disabled={selectableItems.length === 0} onChange={toggleAll}>
                  {t('team.selectCurrentList')}
                </Checkbox>
                <Text type="secondary" style={{ fontSize: 12 }}>
                  {selectedRole.name}
                </Text>
              </div>
            ) : null}
            {listLoading ? (
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: 300 }}>
                <Spin />
              </div>
            ) : listItems.length === 0 ? (
              <Empty
                image={Empty.PRESENTED_IMAGE_SIMPLE}
                description={searchingMode || selectedRole ? t('team.addMemberNoResult') : t('team.userListEmptyHint')}
              />
            ) : (
              listItems.map((user) => {
                const member = isMember(user)
                return (
                  <div key={user.userId} style={{ padding: `${spacing.xs} 0` }}>
                    <Checkbox checked={isSelected(user)} disabled={member} onChange={() => toggleUser(user)} style={{ width: '100%' }}>
                      <span style={{ display: 'inline-flex', alignItems: 'center', gap: spacing.sm, maxWidth: '100%' }}>
                        {renderUserLabel(user)}
                        {member && <Tag style={{ marginInlineEnd: 0 }}>{t('team.alreadyMember')}</Tag>}
                      </span>
                    </Checkbox>
                  </div>
                )
              })
            )}
          </div>
        </div>
        {/* 已选明细：跨组织/搜索累积，可单个移除 */}
        <div style={{ display: 'flex', alignItems: 'flex-start', gap: spacing.sm, flexWrap: 'wrap' }}>
          <Text type="secondary" style={{ fontSize: 12, whiteSpace: 'nowrap', lineHeight: '22px' }}>
            {t('team.selectedCount', { count: selectedUsers.length })}
          </Text>
          {selectedUsers.map((user) => (
            <Tag
              key={user.userId}
              closable
              onClose={() => setSelectedUsers((prev) => prev.filter((x) => x.userId !== user.userId))}
              style={{ display: 'inline-flex', alignItems: 'center', gap: spacing.xs }}
            >
              {user.userName}
            </Tag>
          ))}
        </div>
      </Space>
    </Modal>
  )
}
