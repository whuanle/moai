import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import type { Key, ReactNode } from 'react'
import {
  DeleteOutlined,
  EditOutlined,
  PlusOutlined,
  ReloadOutlined,
  SearchOutlined,
  StarFilled,
  TeamOutlined,
  UserOutlined,
} from '@ant-design/icons'
import { Avatar, Button, Empty, Form, Input, Modal, Popconfirm, Select, Space, Tag, Tooltip, Tree, theme as antdTheme, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import type { DataNode } from 'antd/es/tree'
import { useTranslation } from 'react-i18next'
import { DataTable, PageToolbar, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { useNeutralColors } from '@/design-system/theme/useNeutralColors'
import { formatDateTime } from '@/utils/datetime'
import { resolveStorageUrl } from '@/utils/storage'
import { getUsers, type UserListItem } from '@/api/usermanage'
import {
  createOrgRole,
  deleteOrgRole,
  getOrgRoleUsers,
  getOrgRoles,
  getUserOrgRoles,
  updateOrgRole,
  updateOrgRoleDefault,
  updateUserOrgRoles,
  type OrgRoleItem,
  type OrgRoleUserItem,
} from '@/api/org'

const { Text } = Typography

const rolesPanelHeight = 'calc(100vh - 180px)'

/** 虚拟根节点 key（固定「默认根」，顶级组织挂其下，顶级组织 parentId=0） */
const ROOT_NODE_KEY = '__root__'

/** 新建/编辑表单值（只输入名称，层级由入口上下文/拖拽决定） */
interface RoleFormValues {
  name: string
}

/** 组织角色管理面板（组织角色页第二个 Tab）：左侧组织树（虚拟默认根 + 拖拽挂层级）+ 右侧选中组织成员列表 */
export function OrgRolesPanel() {
  const { t } = useTranslation()
  const { token } = antdTheme.useToken()
  const neutral = useNeutralColors()

  const [roles, setRoles] = useState<OrgRoleItem[]>([])
  const [keyword, setKeyword] = useState('')
  const [expandedKeys, setExpandedKeys] = useState<Key[]>([ROOT_NODE_KEY])
  const [selectedRoleId, setSelectedRoleId] = useState<number | null>(null)

  const [form] = Form.useForm<RoleFormValues>()
  const [modalOpen, setModalOpen] = useState(false)
  /** 编辑中的组织；null=新建 */
  const [editing, setEditing] = useState<OrgRoleItem | null>(null)
  /** 新建子的上级；null=在默认根下新建顶级组织 */
  const [presetParent, setPresetParent] = useState<OrgRoleItem | null>(null)
  const [submitting, setSubmitting] = useState(false)

  /** 右栏成员列表（随选中组织加载） */
  const [members, setMembers] = useState<OrgRoleUserItem[]>([])
  const [membersLoading, setMembersLoading] = useState(false)
  const [membersKeyword, setMembersKeyword] = useState('')
  const [addMemberUserId, setAddMemberUserId] = useState<string | undefined>(undefined)
  const [userOptions, setUserOptions] = useState<UserListItem[]>([])
  const [userLoading, setUserLoading] = useState(false)

  /** 按用户分配弹窗 */
  const [assignOpen, setAssignOpen] = useState(false)
  const [assignUserId, setAssignUserId] = useState<string | undefined>(undefined)
  const [assignRoleIds, setAssignRoleIds] = useState<number[]>([])
  const [assignSubmitting, setAssignSubmitting] = useState(false)
  const searchTimer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)

  const selectedRole = useMemo(() => roles.find((r) => r.orgRoleId === selectedRoleId) ?? null, [roles, selectedRoleId])

  const load = useCallback(async (search = keyword) => {
    try {
      const list = await getOrgRoles(search)
      setRoles(list)
      // 默认展开默认根与全部节点；搜索态只展开命中链路（由 treeData 过滤时驱动）
      if (!search.trim()) {
        setExpandedKeys([ROOT_NODE_KEY, ...list.map((x) => x.orgRoleId ?? 0)])
      }
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }, [keyword])

  useEffect(() => {
    void load('')
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  useEffect(() => () => {
    if (searchTimer.current) clearTimeout(searchTimer.current)
  }, [])

  const loadMembers = useCallback(async (roleId: number, search = '') => {
    setMembersLoading(true)
    try {
      setMembers(await getOrgRoleUsers(roleId, search))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setMembersLoading(false)
    }
  }, [])

  /** 选中组织变化时加载其成员 */
  useEffect(() => {
    if (selectedRoleId != null) {
      setMembersKeyword('')
      setAddMemberUserId(undefined)
      void loadMembers(selectedRoleId)
      void loadUsers('')
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedRoleId])

  /** 按关键字拉取候选用户（全站用户，供添加成员/按用户分配选择） */
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

  const userSelectOptions = useMemo(
    () =>
      userOptions.map((u) => ({
        label:
          u.nickName && u.nickName !== u.userName
            ? `${u.nickName} (${u.userName ?? ''})`
            : String(u.userName ?? ''),
        value: String(u.id),
      })),
    [userOptions],
  )

  /** 左侧树数据：固定「默认根」+ 真实组织树（path 序保证父先于子）；搜索时保留命中节点及其祖先链 */
  const treeData = useMemo<DataNode[]>(() => {
    interface BuildNode {
      key: number
      role: OrgRoleItem
      matched: boolean
      children: BuildNode[]
    }

    const nodes = new Map<number, BuildNode>()
    const roots: BuildNode[] = []
    roles.forEach((r) => {
      const node: BuildNode = {
        key: r.orgRoleId ?? 0,
        role: r,
        matched: !keyword.trim() || (r.name ?? '').includes(keyword.trim()),
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

    const filterTree = (list: BuildNode[]): BuildNode[] =>
      list
        .map((node) => {
          const children = node.children.length > 0 ? filterTree(node.children) : []
          // 自身命中保留其命中子树；自身未命中但子树有命中则作为祖先链保留
          if (!node.matched && children.length === 0) return null
          return { ...node, children }
        })
        .filter((x): x is BuildNode => x != null)

    const createButton = (parent: OrgRoleItem | null): ReactNode => (
      <Tooltip title={t(parent ? 'adminOrgRoles.addChild' : 'adminOrgRoles.createTop')}>
        <Button
          type="text"
          size="small"
          icon={<PlusOutlined />}
          aria-label={t(parent ? 'adminOrgRoles.addChild' : 'adminOrgRoles.createTop')}
          onClick={(e) => {
            e.stopPropagation()
            openCreate(parent)
          }}
        />
      </Tooltip>
    )

    const renderTitle = (r: OrgRoleItem): ReactNode => (
      <span style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: spacing.xs, minWidth: 0 }}>
        <Space size={spacing.xs} style={{ minWidth: 0, flex: 1 }}>
          <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{r.name ?? '-'}</span>
          <Tag style={{ marginInlineEnd: 0 }}>{r.userCount ?? 0}</Tag>
          {r.isDefault === true && (
            <Tooltip title={t('adminOrgRoles.defaultRoleBadge')}>
              <StarFilled aria-label={t('adminOrgRoles.defaultRoleBadge')} style={{ color: token.colorWarning }} />
            </Tooltip>
          )}
        </Space>
        {createButton(r)}
      </span>
    )

    const toDataNode = (node: BuildNode): DataNode => ({
      key: node.key,
      title: renderTitle(node.role),
      children: node.children.length > 0 ? node.children.map(toDataNode) : undefined,
    })

    const filtered = keyword.trim() ? filterTree(roots) : roots
    return [
      {
        key: ROOT_NODE_KEY,
        selectable: false,
        title: (
          <span style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: spacing.xs }}>
            <Text strong>{t('adminOrgRoles.rootNode')}</Text>
            {createButton(null)}
          </span>
        ),
        children: filtered.map(toDataNode),
      },
    ]
    // openCreate 为每次渲染重建的闭包，入依赖会击穿 memo
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [roles, keyword, t, token.colorWarning])

  /** 新建组织：只输入名称，上级由入口决定（默认根=顶级组织 parentId=0，节点按钮=其下级） */
  const openCreate = (parent: OrgRoleItem | null) => {
    setEditing(null)
    setPresetParent(parent)
    form.resetFields()
    form.setFieldsValue({ name: '' })
    setModalOpen(true)
  }

  /** 编辑=改名（层级调整走拖拽） */
  const openEdit = (record: OrgRoleItem) => {
    setEditing(record)
    setPresetParent(null)
    form.resetFields()
    form.setFieldsValue({ name: record.name ?? '' })
    setModalOpen(true)
  }

  const handleSubmit = async () => {
    const values = await form.validateFields()
    setSubmitting(true)
    try {
      if (editing) {
        await updateOrgRole(editing.orgRoleId ?? 0, { name: values.name, parentId: editing.parentId ?? null })
        feedback.success(t('adminOrgRoles.updateSuccess'))
      } else {
        await createOrgRole({ name: values.name, parentId: presetParent?.orgRoleId ?? null })
        feedback.success(t('adminOrgRoles.createSuccess'))
      }
      setModalOpen(false)
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSubmitting(false)
    }
  }

  /** 拖拽重新挂层级：拖到节点内=成为其下级；拖到节点间=同级换挂；拖到默认根=升为顶级 */
  const handleDrop = async (info: {
    node: { key: Key | { key?: Key } }
    dragNode: { key: Key | { key?: Key } }
    dropToGap: boolean
  }) => {
    const dragKey = Number(typeof info.dragNode.key === 'object' ? info.dragNode.key.key : info.dragNode.key)
    const dropKeyRaw = typeof info.node.key === 'object' ? info.node.key.key : info.node.key
    const dragRole = roles.find((r) => r.orgRoleId === dragKey)
    if (!dragRole || Number.isNaN(dragKey)) return

    let newParentId: number | null
    if (dropKeyRaw === ROOT_NODE_KEY) {
      newParentId = null
    } else {
      const dropRole = roles.find((r) => r.orgRoleId === Number(dropKeyRaw))
      if (!dropRole) return
      newParentId = info.dropToGap ? (dropRole.parentId ?? null) : (dropRole.orgRoleId ?? null)
    }

    if (newParentId === (dragRole.parentId ?? null)) {
      // 同级内拖动不改变层级（组织间无独立排序，按 id 数值序展示）
      return
    }

    if (newParentId != null) {
      const target = roles.find((r) => r.orgRoleId === newParentId)
      if (target && (target.path ?? '').startsWith(dragRole.path ?? '')) {
        feedback.warning(t('adminOrgRoles.dropBlocked'))
        return
      }
    }

    try {
      await updateOrgRole(dragRole.orgRoleId ?? 0, { name: dragRole.name ?? '', parentId: newParentId })
      feedback.success(t('adminOrgRoles.moveSuccess'))
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示（含后端防环兜底）
    }
  }

  const handleDelete = async () => {
    if (!selectedRole) return
    try {
      await deleteOrgRole(selectedRole.orgRoleId ?? 0)
      feedback.success(t('adminOrgRoles.deleteSuccess'))
      setSelectedRoleId(null)
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleToggleDefault = async () => {
    if (!selectedRole) return
    try {
      await updateOrgRoleDefault(selectedRole.isDefault === true ? null : (selectedRole.orgRoleId ?? 0))
      feedback.success(t(selectedRole.isDefault === true ? 'adminOrgRoles.clearDefaultSuccess' : 'adminOrgRoles.setDefaultSuccess'))
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  /** 添加成员：在用户现有组织集合上并入当前组织 */
  const handleAddMember = async () => {
    if (!selectedRole || !addMemberUserId) return
    const userId = Number(addMemberUserId)
    try {
      const current = await getUserOrgRoles(userId)
      const roleIds = current.items.map((x) => x.orgRoleId ?? 0).filter((id) => id > 0)
      if (!roleIds.includes(selectedRole.orgRoleId ?? 0)) {
        roleIds.push(selectedRole.orgRoleId ?? 0)
        await updateUserOrgRoles(userId, roleIds)
      }
      feedback.success(t('adminOrgRoles.addMemberSuccess'))
      setAddMemberUserId(undefined)
      void loadMembers(selectedRole.orgRoleId ?? 0, membersKeyword)
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  /** 移除成员：在用户现有组织集合上去掉当前组织 */
  const handleRemoveMember = async (userId: number) => {
    if (!selectedRole) return
    try {
      const current = await getUserOrgRoles(userId)
      const roleIds = current.items
        .map((x) => x.orgRoleId ?? 0)
        .filter((id) => id > 0 && id !== selectedRole.orgRoleId)
      await updateUserOrgRoles(userId, roleIds)
      feedback.success(t('adminOrgRoles.removeMemberSuccess'))
      void loadMembers(selectedRole.orgRoleId ?? 0, membersKeyword)
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  /** 按用户分配：选中用户后回显其组织集合，整体覆盖保存 */
  const handleAssignUserChange = async (value: string) => {
    setAssignUserId(value)
    setAssignRoleIds([])
    if (!value) return
    try {
      const current = await getUserOrgRoles(Number(value))
      setAssignRoleIds(current.items.map((x) => x.orgRoleId ?? 0).filter((id) => id > 0))
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const openAssign = () => {
    setAssignUserId(undefined)
    setAssignRoleIds([])
    setAssignOpen(true)
    void loadUsers('')
  }

  const handleAssignSave = async () => {
    if (!assignUserId) return
    setAssignSubmitting(true)
    try {
      await updateUserOrgRoles(Number(assignUserId), assignRoleIds)
      feedback.success(t('adminOrgRoles.assignSuccess'))
      setAssignOpen(false)
      void load()
      if (selectedRoleId != null) void loadMembers(selectedRoleId)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setAssignSubmitting(false)
    }
  }

  /** 成员列渲染：头像 + 昵称（主显示名）+ @用户名（与昵称相同时不重复展示），另附邮箱/手机号/角色/状态资料列 */
  const renderMemberUser = (record: OrgRoleUserItem) => {
    const userName = record.userName || '-'
    const nickName = record.nickName?.trim()
    const displayName = nickName || userName
    const avatarUrl = resolveStorageUrl(record.avatar ?? null)
    return (
      <Space>
        <Avatar size={36} src={avatarUrl || undefined} alt={userName}>
          {displayName.slice(0, 1).toUpperCase()}
        </Avatar>
        <div style={{ lineHeight: 1.4, minWidth: 0 }}>
          <div>{displayName}</div>
          {nickName && nickName !== userName && (
            <Text type="secondary" style={{ fontSize: 12 }}>
              @{userName}
            </Text>
          )}
        </div>
      </Space>
    )
  }

  const renderMemberRole = (record: OrgRoleUserItem) => {
    if (record.isRoot === true) return <Tag color="gold">{t('adminOrgRoles.roleRoot')}</Tag>
    if (record.isAdmin === true) return <Tag color="blue">{t('adminOrgRoles.roleAdmin')}</Tag>
    return <Tag>{t('adminOrgRoles.roleMember')}</Tag>
  }

  const renderMemberStatus = (record: OrgRoleUserItem) =>
    record.isDisable === true ? (
      <Tag color="error">{t('adminOrgRoles.statusDisabled')}</Tag>
    ) : (
      <Tag color="success">{t('adminOrgRoles.statusNormal')}</Tag>
    )

  const memberColumns: TableColumnsType<OrgRoleUserItem> = useMemo(
    () => [
      {
        title: t('adminOrgRoles.colUser'),
        key: 'user',
        render: (_, record) => renderMemberUser(record),
      },
      {
        title: t('adminOrgRoles.colEmail'),
        dataIndex: 'email',
        key: 'email',
        width: 240,
        ellipsis: true,
        render: (v: string | null) => v || '-',
      },
      {
        title: t('adminOrgRoles.colPhone'),
        dataIndex: 'phone',
        key: 'phone',
        width: 130,
        render: (v: string | null) => v || '-',
      },
      { title: t('adminOrgRoles.colRole'), key: 'role', width: 100, render: (_, record) => renderMemberRole(record) },
      { title: t('adminOrgRoles.colStatus'), key: 'status', width: 85, render: (_, record) => renderMemberStatus(record) },
      {
        title: t('adminOrgRoles.colBindTime'),
        dataIndex: 'createTime',
        key: 'createTime',
        width: 150,
        render: (v: string | null) => <span style={{ whiteSpace: 'nowrap' }}>{formatDateTime(v)}</span>,
      },
      {
        title: t('adminOrgRoles.colActions'),
        key: 'actions',
        width: 90,
        render: (_, record) => (
          <Popconfirm
            title={t('adminOrgRoles.removeMemberConfirm')}
            okButtonProps={{ danger: true }}
            onConfirm={() => void handleRemoveMember(Number(record.userId ?? 0))}
          >
            <Button type="text" size="small" danger aria-label={t('adminOrgRoles.removeMember')}>
              {t('adminOrgRoles.removeMember')}
            </Button>
          </Popconfirm>
        ),
      },
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, selectedRole, membersKeyword],
  )

  return (
    <div style={{ display: 'flex', gap: spacing.md, height: rolesPanelHeight, minHeight: 0 }}>
      {/* 左栏：组织树（固定默认根 + 节点新建 + 拖拽挂层级） */}
      <div style={{ width: 340, minWidth: 340, display: 'flex', flexDirection: 'column', minHeight: 0 }}>
        <PageToolbar
          filters={
            <Input
              value={keyword}
              onChange={(e) => setKeyword(e.target.value)}
              allowClear
              maxLength={50}
              prefix={<SearchOutlined style={{ color: 'inherit' }} />}
              placeholder={t('adminOrgRoles.searchPlaceholder')}
              style={{ width: 160 }}
            />
          }
          actions={
            <Space size={spacing.xs}>
              <Button icon={<UserOutlined />} aria-label={t('adminOrgRoles.assign')} onClick={openAssign}>
                {t('adminOrgRoles.assign')}
              </Button>
              <Tooltip title={t('adminOrgRoles.refresh')}>
                <Button icon={<ReloadOutlined />} aria-label={t('adminOrgRoles.refresh')} onClick={() => void load()} />
              </Tooltip>
            </Space>
          }
        />
        <div
          style={{
            flex: 1,
            minHeight: 0,
            overflow: 'auto',
            border: `1px solid ${neutral.border}`,
            borderRadius: 8,
            padding: spacing.sm,
            marginTop: spacing.xs,
          }}
        >
          <Tree
            blockNode
            showIcon={false}
            treeData={treeData}
            expandedKeys={expandedKeys}
            onExpand={(keys) => setExpandedKeys(keys)}
            selectedKeys={selectedRoleId != null ? [selectedRoleId] : []}
            onSelect={(keys) => setSelectedRoleId(keys.length > 0 && keys[0] !== ROOT_NODE_KEY ? Number(keys[0]) : null)}
            draggable
            onDrop={(info) => void handleDrop(info)}
          />
        </div>
      </div>

      {/* 右栏：选中组织的成员列表 */}
      <div style={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column', minHeight: 0 }}>
        {selectedRole == null ? (
          <div style={{ flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
            <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('adminOrgRoles.emptySelection')} />
          </div>
        ) : (
          <>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: spacing.sm, flexWrap: 'wrap' }}>
              <Space size={spacing.xs} wrap>
                <TeamOutlined />
                <Text strong>{selectedRole.name}</Text>
                <Text type="secondary" style={{ fontSize: 12 }}>
                  {selectedRole.path}
                </Text>
                {selectedRole.isDefault === true && <Tag color="warning">{t('adminOrgRoles.defaultRoleBadge')}</Tag>}
              </Space>
              <Space size={0}>
                <Tooltip title={t('adminOrgRoles.edit')}>
                  <Button
                    type="text"
                    size="small"
                    icon={<EditOutlined />}
                    aria-label={t('adminOrgRoles.edit')}
                    onClick={() => openEdit(selectedRole)}
                  />
                </Tooltip>
                {selectedRole.isDefault === true ? (
                  <Popconfirm
                    title={t('adminOrgRoles.clearDefaultConfirm')}
                    okButtonProps={{ danger: true }}
                    onConfirm={() => void handleToggleDefault()}
                  >
                    <Tooltip title={t('adminOrgRoles.clearDefault')}>
                      <Button type="text" size="small" icon={<StarFilled />} aria-label={t('adminOrgRoles.clearDefault')} />
                    </Tooltip>
                  </Popconfirm>
                ) : (
                  <Tooltip title={t('adminOrgRoles.setDefault')}>
                    <Button
                      type="text"
                      size="small"
                      icon={<StarFilled style={{ color: neutral.textTertiary }} />}
                      aria-label={t('adminOrgRoles.setDefault')}
                      onClick={() => void handleToggleDefault()}
                    />
                  </Tooltip>
                )}
                <Popconfirm
                  title={t('adminOrgRoles.deleteConfirm')}
                  okButtonProps={{ danger: true }}
                  onConfirm={() => void handleDelete()}
                >
                  <Tooltip title={t('adminOrgRoles.delete')}>
                    <Button type="text" size="small" danger icon={<DeleteOutlined />} aria-label={t('adminOrgRoles.delete')} />
                  </Tooltip>
                </Popconfirm>
              </Space>
            </div>
            <div style={{ marginTop: spacing.xs }}>
              <PageToolbar
                filters={
                  <Space size={spacing.xs} wrap>
                    <Input
                      value={membersKeyword}
                      onChange={(e) => {
                        setMembersKeyword(e.target.value)
                        if (selectedRole) void loadMembers(selectedRole.orgRoleId ?? 0, e.target.value)
                      }}
                      allowClear
                      maxLength={50}
                      prefix={<SearchOutlined style={{ color: 'inherit' }} />}
                      placeholder={t('adminOrgRoles.membersSearchPlaceholder')}
                      style={{ width: 220 }}
                    />
                    <Select
                      showSearch
                      value={addMemberUserId}
                      onChange={(value: string) => setAddMemberUserId(value)}
                      onSearch={handleUserSearch}
                      filterOption={false}
                      loading={userLoading}
                      options={userSelectOptions}
                      placeholder={t('adminOrgRoles.addMemberPlaceholder')}
                      style={{ width: 260 }}
                      notFoundContent={userLoading ? null : t('adminOrgRoles.addMemberEmpty')}
                    />
                    <Button type="primary" disabled={!addMemberUserId} onClick={() => void handleAddMember()}>
                      {t('adminOrgRoles.addMember')}
                    </Button>
                  </Space>
                }
              />
            </div>
            <div style={{ flex: 1, minHeight: 0, marginTop: spacing.xs }}>
              <DataTable<OrgRoleUserItem>
                rowKey="userId"
                columns={memberColumns}
                dataSource={members}
                loading={membersLoading}
                pagination={false}
                sticky
                scroll={{ x: 900, y: 'calc(100vh - 400px)' }}
              />
            </div>
          </>
        )}
      </div>

      <Modal
        open={modalOpen}
        title={t(editing ? 'adminOrgRoles.editTitle' : 'adminOrgRoles.createTitle')}
        onCancel={() => setModalOpen(false)}
        confirmLoading={submitting}
        destroyOnHidden
        maskClosable={false}
        onOk={() => void handleSubmit()}
        okText={t('adminOrgRoles.save')}
        cancelText={t('adminOrgRoles.cancel')}
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="name"
            label={t('adminOrgRoles.formName')}
            extra={!editing ? t('adminOrgRoles.formNameExtra', { name: presetParent?.name ?? t('adminOrgRoles.rootNode') }) : undefined}
            rules={[{ required: true, message: t('adminOrgRoles.formNameRequired'), whitespace: true }, { max: 50, message: t('adminOrgRoles.formNameMax') }]}
          >
            <Input maxLength={50} placeholder={t('adminOrgRoles.formNamePlaceholder')} autoFocus />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        open={assignOpen}
        title={t('adminOrgRoles.assignTitle')}
        onCancel={() => setAssignOpen(false)}
        confirmLoading={assignSubmitting}
        destroyOnHidden
        maskClosable={false}
        onOk={() => void handleAssignSave()}
        okText={t('adminOrgRoles.assignSave')}
        cancelText={t('adminOrgRoles.cancel')}
      >
        <Space direction="vertical" size={spacing.sm} style={{ width: '100%' }}>
          <Select
            showSearch
            value={assignUserId}
            onChange={(value: string) => void handleAssignUserChange(value)}
            onSearch={handleUserSearch}
            filterOption={false}
            loading={userLoading}
            options={userSelectOptions}
            placeholder={t('adminOrgRoles.assignUserPlaceholder')}
            style={{ width: '100%' }}
            notFoundContent={userLoading ? null : t('adminOrgRoles.assignUserEmpty')}
          />
          <Select
            mode="multiple"
            value={assignRoleIds}
            onChange={(value) => setAssignRoleIds(value as number[])}
            optionFilterProp="label"
            options={roles.map((r) => ({ label: r.name ?? '', value: r.orgRoleId ?? 0 }))}
            placeholder={t('adminOrgRoles.assignRolesPlaceholder')}
            style={{ width: '100%' }}
            disabled={!assignUserId}
          />
          <Text type="secondary" style={{ fontSize: 12 }}>
            {t('adminOrgRoles.assignHint')}
          </Text>
        </Space>
      </Modal>
    </div>
  )
}
