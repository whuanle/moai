import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import type { Key, MouseEvent as ReactMouseEvent, ReactNode } from 'react'
import {
  DeleteOutlined,
  EditOutlined,
  PlusOutlined,
  ReloadOutlined,
  SearchOutlined,
  TeamOutlined,
} from '@ant-design/icons'
import { Avatar, Button, Divider, Empty, Form, Input, Modal, Popconfirm, Select, Space, Tag, Tooltip, Tree, theme as antdTheme, Typography } from 'antd'
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
  updateOrgRoleOrder,
  updateUserOrgRoles,
  type OrgRoleItem,
  type OrgRoleUserItem,
} from '@/api/org'

const { Text } = Typography

// 与 TeamsPanel/UsersPanel 同公式：页面上下内边距（spacing.lg×2）+ Tab 栏高度 + Tab 导航 margin-bottom（16），
// 面板恰好铺满视口不产生整页滚动（少算 16px 会让整页多出一条滚动条）
const pageVerticalPadding = spacing.lg * 2
const tabsBarHeight = 46
const tabsNavMargin = 16
const rolesPanelHeight = `calc(100vh - ${pageVerticalPadding + tabsBarHeight + tabsNavMargin}px)`

// 左栏宽度：默认随树内容自适应（fit-content，杜绝横向滚动条），拖拽手柄可手动调整并记忆
const ORG_PANEL_MIN_WIDTH = 320
const ORG_PANEL_MAX_WIDTH = 560
const ORG_PANEL_WIDTH_KEY = 'moai-org-panel-width'

/** 读取左栏手动宽度记忆；无记忆或越界返回 null（走自适应） */
function readStoredPanelWidth(): number | null {
  const v = Number(localStorage.getItem(ORG_PANEL_WIDTH_KEY))
  return Number.isFinite(v) && v >= ORG_PANEL_MIN_WIDTH && v <= ORG_PANEL_MAX_WIDTH ? v : null
}

/** 虚拟根节点 key（固定「默认根」，顶级组织挂其下，顶级组织 parentId=0） */
const ROOT_NODE_KEY = '__root__'

/** 新建/编辑表单值（只输入名称，层级由入口上下文/拖拽决定） */
interface RoleFormValues {
  name: string
}

/** 组织管理面板（组织页第二个 Tab）：左侧组织树（虚拟默认根 + 拖拽挂层级）+ 右侧选中组织成员列表 */
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

  /** 右栏成员列表（随选中组织加载全量，筛选在前端完成） */
  const [members, setMembers] = useState<OrgRoleUserItem[]>([])
  const [membersLoading, setMembersLoading] = useState(false)
  /** 成员筛选输入框内容（未应用） */
  const [membersKeyword, setMembersKeyword] = useState('')
  /** 已应用的成员筛选词（查询按钮/回车触发） */
  const [membersQuery, setMembersQuery] = useState('')
  const [addMemberUserId, setAddMemberUserId] = useState<string | undefined>(undefined)
  const [userOptions, setUserOptions] = useState<UserListItem[]>([])
  const [userLoading, setUserLoading] = useState(false)
  const searchTimer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)

  /** 左栏宽度：手动像素宽度（拖拽后记忆）优先；null 时用 autoWidth（随树内容自适应，杜绝横向滚动条） */
  const [panelWidth, setPanelWidth] = useState<number | null>(readStoredPanelWidth)
  const [autoWidth, setAutoWidth] = useState<number | null>(null)
  const [dragging, setDragging] = useState(false)
  const [handleHover, setHandleHover] = useState(false)
  const leftColRef = useRef<HTMLDivElement | null>(null)
  const dragStart = useRef<{ x: number; width: number } | null>(null)

  /** 树容器（拖拽组织节点时供边缘自动滚动） */
  const treeBoxRef = useRef<HTMLDivElement | null>(null)
  const [treeDragging, setTreeDragging] = useState(false)
  const treeDragY = useRef<number | null>(null)

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

  const loadMembers = useCallback(async (roleId: number) => {
    setMembersLoading(true)
    try {
      setMembers(await getOrgRoleUsers(roleId))
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
      setMembersQuery('')
      setAddMemberUserId(undefined)
      void loadMembers(selectedRoleId)
      void loadUsers('')
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedRoleId])

  /** 按关键字拉取候选用户（全站用户，供添加成员选择） */
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

  /** 拖拽组织节点时的边缘自动滚动：指针贴近树容器上下边缘即持续滚动（rAF 循环），长树从底部能拉到顶部 */
  useEffect(() => {
    if (!treeDragging) return
    const onDragOver = (e: DragEvent) => {
      treeDragY.current = e.clientY
    }
    window.addEventListener('dragover', onDragOver)
    let raf = 0
    const step = () => {
      const box = treeBoxRef.current
      const y = treeDragY.current
      if (box && y != null) {
        const rect = box.getBoundingClientRect()
        const edge = 48
        if (y < rect.top + edge) {
          box.scrollTop -= 12
        } else if (y > rect.bottom - edge) {
          box.scrollTop += 12
        }
      }
      raf = requestAnimationFrame(step)
    }
    raf = requestAnimationFrame(step)
    return () => {
      window.removeEventListener('dragover', onDragOver)
      cancelAnimationFrame(raf)
    }
  }, [treeDragging])

  /** 应用成员筛选（查询按钮/回车触发）：只过滤当前组织成员，纯前端完成 */
  const applyMembersFilter = () => setMembersQuery(membersKeyword.trim())

  /** 前端筛选后的成员列表（按用户名/昵称包含匹配，不区分大小写） */
  const filteredMembers = useMemo(() => {
    const q = membersQuery.toLowerCase()
    if (!q) return members
    return members.filter(
      (m) => (m.userName ?? '').toLowerCase().includes(q) || (m.nickName ?? '').toLowerCase().includes(q),
    )
  }, [members, membersQuery])

  /** 拖拽分隔手柄调整左栏宽度：跟手移动、钳制上下限并写入 localStorage */
  useEffect(() => {
    if (!dragging) return
    const onMove = (e: MouseEvent) => {
      const start = dragStart.current
      if (!start) return
      const width = Math.min(ORG_PANEL_MAX_WIDTH, Math.max(ORG_PANEL_MIN_WIDTH, start.width + (e.clientX - start.x)))
      setPanelWidth(width)
      localStorage.setItem(ORG_PANEL_WIDTH_KEY, String(width))
    }
    const onUp = () => {
      dragStart.current = null
      setDragging(false)
    }
    window.addEventListener('mousemove', onMove)
    window.addEventListener('mouseup', onUp)
    // 拖拽期间禁用全局文本选中与恢复默认光标，避免跟手移动时误选文字
    document.body.style.userSelect = 'none'
    document.body.style.cursor = 'col-resize'
    return () => {
      window.removeEventListener('mousemove', onMove)
      window.removeEventListener('mouseup', onUp)
      document.body.style.userSelect = ''
      document.body.style.cursor = ''
    }
  }, [dragging])

  const handleResizeStart = (e: ReactMouseEvent) => {
    e.preventDefault()
    dragStart.current = { x: e.clientX, width: leftColRef.current?.getBoundingClientRect().width ?? 340 }
    setDragging(true)
  }

  /** 双击手柄：清除手动记忆，恢复随内容自适应 */
  const handleResizeReset = () => {
    localStorage.removeItem(ORG_PANEL_WIDTH_KEY)
    setPanelWidth(null)
  }

  /** 自适应宽度：无手动宽度时，按最宽节点行的自然文本宽收纳左栏（滚动条不再出现）。
   *  antd Tree blockNode 让行宽恒等于容器宽，CSS fit-content 取不到自然宽，只能按省略号 span 的 scrollWidth 反推 */
  useEffect(() => {
    if (panelWidth != null) return
    const col = leftColRef.current
    if (!col) return
    let maxRowNatural = 0
    col.querySelectorAll<HTMLElement>('.org-node-name').forEach((span) => {
      const node = span.closest<HTMLElement>('.ant-tree-treenode')
      if (!node) return
      // span.scrollWidth=省略号截断前的自然文本宽；行其余固定部分（缩进/箭头/计数/按钮）=行宽-span已布局宽
      const rowNatural = span.scrollWidth + (node.offsetWidth - span.offsetWidth)
      maxRowNatural = Math.max(maxRowNatural, rowNatural)
    })
    if (maxRowNatural <= 0) return
    const target = Math.ceil(maxRowNatural + spacing.sm * 2 + 4) // 树盒内边距+边框+少量余量
    setAutoWidth(Math.min(ORG_PANEL_MAX_WIDTH, Math.max(ORG_PANEL_MIN_WIDTH, target)))
  }, [roles, keyword, expandedKeys, panelWidth])

  /** 候选用户选项：头像 + 主显示名 + @用户名（昵称与用户名相同时不重复） */
  const userSelectOptions = useMemo(
    () =>
      userOptions.map((u) => {
        const userName = String(u.userName ?? '')
        const nickName = u.nickName?.trim()
        const displayName = nickName || userName
        const avatarUrl = resolveStorageUrl(u.avatar ?? null)
        return {
          label: (
            <Space size={spacing.xs}>
              <Avatar size={22} src={avatarUrl || undefined} alt={userName}>
                {displayName.slice(0, 1).toUpperCase()}
              </Avatar>
              <span>
                {displayName}
                {nickName && nickName !== userName && (
                  <Text type="secondary" style={{ fontSize: 12, marginInlineStart: 4 }}>
                    @{userName}
                  </Text>
                )}
              </span>
            </Space>
          ),
          value: String(u.id),
        }
      }),
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
          style={{ flexShrink: 0 }}
          aria-label={t(parent ? 'adminOrgRoles.addChild' : 'adminOrgRoles.createTop')}
          onClick={(e) => {
            e.stopPropagation()
            openCreate(parent)
          }}
        />
      </Tooltip>
    )

    /** 组织节点行内图标操作组：新建下级 + 编辑 + 删除（点击不触发节点选中） */
    const nodeActions = (r: OrgRoleItem): ReactNode => (
      <span style={{ display: 'flex', alignItems: 'center', flexShrink: 0 }}>
        {createButton(r)}
        <Tooltip title={t('adminOrgRoles.edit')}>
          <Button
            type="text"
            size="small"
            icon={<EditOutlined />}
            style={{ flexShrink: 0 }}
            aria-label={t('adminOrgRoles.edit')}
            onClick={(e) => {
              e.stopPropagation()
              openEdit(r)
            }}
          />
        </Tooltip>
        <Popconfirm title={t('adminOrgRoles.deleteConfirm')} okButtonProps={{ danger: true }} onConfirm={() => void handleDeleteRole(r)}>
          <Tooltip title={t('adminOrgRoles.delete')}>
            <Button
              type="text"
              size="small"
              danger
              icon={<DeleteOutlined />}
              style={{ flexShrink: 0 }}
              aria-label={t('adminOrgRoles.delete')}
              onClick={(e) => e.stopPropagation()}
            />
          </Tooltip>
        </Popconfirm>
      </span>
    )

    const renderTitle = (r: OrgRoleItem): ReactNode => (
      <span style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: spacing.xs, minWidth: 0 }}>
        {/* 名称是唯一收缩点（overflow hidden 使 flex 最小尺寸归零），Tag/按钮不缩，长名省略号收纳、行不再撑破容器 */}
        <span style={{ display: 'flex', alignItems: 'center', gap: spacing.xs, flex: 1, minWidth: 0, overflow: 'hidden' }}>
          <span className="org-node-name" style={{ display: 'block', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{r.name ?? '-'}</span>
          <Tag style={{ marginInlineEnd: 0, flexShrink: 0 }}>{r.userCount ?? 0}</Tag>
        </span>
        {nodeActions(r)}
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
  }, [roles, keyword, t])

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

  /** 拖拽挂层级与同级排序：拖到节点内=成为其下级（同级末尾）；拖到节点间隙=排到它前/后；拖到默认根=升为顶级并置顶 */
  const handleDrop = async (info: {
    node: { key: Key | { key?: Key }; pos?: string }
    dragNode: { key: Key | { key?: Key } }
    dropToGap: boolean
    dropPosition?: number
  }) => {
    const dragKey = Number(typeof info.dragNode.key === 'object' ? info.dragNode.key.key : info.dragNode.key)
    const dropKeyRaw = typeof info.node.key === 'object' ? info.node.key.key : info.node.key
    const dragRole = roles.find((r) => r.orgRoleId === dragKey)
    if (!dragRole || Number.isNaN(dragKey)) return

    /** 某层级的同级列表（后端列表序即排序结果；null=根层） */
    const siblingsOf = (parentId: number | null) => roles.filter((r) => (r.parentId ?? null) === parentId)
    const lastIdOf = (list: OrgRoleItem[]) => (list.length > 0 ? list[list.length - 1].orgRoleId ?? null : null)
    /** 相对落点方位（rc-tree：info.dropPosition 已含节点展开序，减去落点节点末段得 -1 上方 / 0 内部 / 1 下方） */
    const posSegments = info.node.pos?.split('-') ?? []
    const relative = info.dropPosition != null && posSegments.length > 0
      ? info.dropPosition - Number(posSegments[posSegments.length - 1])
      : 0

    let newParentId: number | null
    let newPrevId: number | null
    if (dropKeyRaw === ROOT_NODE_KEY) {
      // 默认根是树顶的容器行：拖到它上半部/行上（relative≤0）意图都是「放到开头」→ 置顶；
      // 仅深层左移上提落成「默认根之后」（relative=1，罕见）= 追加为最后一个顶级
      newParentId = null
      newPrevId = relative === 1
        ? lastIdOf(siblingsOf(null).filter((x) => x.orgRoleId !== dragRole.orgRoleId))
        : null
    } else {
      const dropRole = roles.find((r) => r.orgRoleId === Number(dropKeyRaw))
      if (!dropRole) return
      if (!info.dropToGap || relative === 0) {
        // 拖入节点内：成为其子级，排在子级末尾（不含被拖节点自身，同级内拖动时除外）
        newParentId = dropRole.orgRoleId ?? null
        newPrevId = lastIdOf(siblingsOf(newParentId).filter((x) => x.orgRoleId !== dragRole.orgRoleId))
      } else {
        // 节点间隙：落为其同级；上方间隙=排到它前面（prev=它前面的兄弟），下方间隙=排到它后面
        newParentId = dropRole.parentId ?? null
        const siblings = siblingsOf(newParentId).filter((x) => x.orgRoleId !== dragRole.orgRoleId)
        if (relative < 0) {
          const idx = siblings.findIndex((x) => x.orgRoleId === dropRole.orgRoleId)
          newPrevId = idx > 0 ? siblings[idx - 1].orgRoleId ?? null : null
        } else {
          newPrevId = dropRole.orgRoleId ?? null
        }
      }
    }

    // 落位与现状一致（层级与插入位置均未变化）则不提交
    const currentSiblings = siblingsOf(dragRole.parentId ?? null)
    const currentIdx = currentSiblings.findIndex((x) => x.orgRoleId === dragRole.orgRoleId)
    const currentPrevId = currentIdx > 0 ? currentSiblings[currentIdx - 1].orgRoleId ?? null : null
    if (newParentId === (dragRole.parentId ?? null) && newPrevId === currentPrevId) {
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
      await updateOrgRoleOrder(dragRole.orgRoleId ?? 0, { parentId: newParentId, prevOrgRoleId: newPrevId })
      feedback.success(t('adminOrgRoles.moveSuccess'))
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示（含后端防环兜底）
    }
  }

  /** 删除组织（节点行内删除图标触发）；删的是当前选中组织时清空右栏 */
  const handleDeleteRole = async (role: OrgRoleItem) => {
    try {
      await deleteOrgRole(role.orgRoleId ?? 0)
      feedback.success(t('adminOrgRoles.deleteSuccess'))
      if (selectedRoleId === (role.orgRoleId ?? 0)) setSelectedRoleId(null)
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
      void loadMembers(selectedRole.orgRoleId ?? 0)
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
      void loadMembers(selectedRole.orgRoleId ?? 0)
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
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
        width: 110,
        render: (_, record) => (
          <Popconfirm
            title={t('adminOrgRoles.removeMemberConfirm')}
            okButtonProps={{ danger: true }}
            onConfirm={() => void handleRemoveMember(Number(record.userId ?? 0))}
          >
            <Button type="link" size="small" danger icon={<DeleteOutlined />} aria-label={t('adminOrgRoles.removeMember')}>
              {t('adminOrgRoles.removeMember')}
            </Button>
          </Popconfirm>
        ),
      },
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, selectedRole],
  )

  return (
    // overflow hidden：组织树再长也只在左栏树容器内滚动，杜绝任何溢出把整页撑出滚动条
    <div style={{ display: 'flex', gap: spacing.xs, height: rolesPanelHeight, minHeight: 0, overflow: 'hidden' }}>
      {/* 左栏：组织树（固定默认根 + 节点新建 + 拖拽挂层级）；宽度默认随内容自适应，分隔手柄可拖拽调整 */}
      <div
        ref={leftColRef}
        style={{
          width: panelWidth ?? autoWidth ?? ORG_PANEL_MIN_WIDTH,
          minWidth: ORG_PANEL_MIN_WIDTH,
          maxWidth: ORG_PANEL_MAX_WIDTH,
          flexShrink: 0,
          display: 'flex',
          flexDirection: 'column',
          minHeight: 0,
        }}
      >
        <PageToolbar
          filters={
            <Input
              value={keyword}
              onChange={(e) => setKeyword(e.target.value)}
              allowClear
              maxLength={50}
              prefix={<SearchOutlined style={{ color: 'inherit' }} />}
              placeholder={t('adminOrgRoles.searchPlaceholder')}
              style={{ width: 260 }}
            />
          }
          queries={
            <Tooltip title={t('adminOrgRoles.refresh')}>
              <Button icon={<ReloadOutlined />} aria-label={t('adminOrgRoles.refresh')} onClick={() => void load()} />
            </Tooltip>
          }
        />
        <div
          ref={treeBoxRef}
          style={{
            flex: 1,
            minHeight: 0,
            overflowY: 'auto',
            overflowX: 'hidden',
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
            onDragStart={() => setTreeDragging(true)}
            onDragEnd={() => {
              setTreeDragging(false)
              treeDragY.current = null
            }}
            onDrop={(info) => void handleDrop(info)}
          />
        </div>
      </div>

      {/* 分隔手柄：拖拽调整左栏宽度（宽 8px 热区），双击清除手动宽度恢复自适应 */}
      <div
        role="separator"
        aria-orientation="vertical"
        aria-label={t('adminOrgRoles.resizeHandleHint')}
        title={t('adminOrgRoles.resizeHandleHint')}
        onMouseDown={handleResizeStart}
        onDoubleClick={handleResizeReset}
        onMouseEnter={() => setHandleHover(true)}
        onMouseLeave={() => setHandleHover(false)}
        style={{
          width: 8,
          flexShrink: 0,
          cursor: 'col-resize',
          display: 'flex',
          alignItems: 'stretch',
          justifyContent: 'center',
          touchAction: 'none',
        }}
      >
        <div
          style={{
            width: 2,
            borderRadius: 1,
            background: dragging || handleHover ? token.colorPrimary : neutral.border,
            transition: 'background 0.2s',
          }}
        />
      </div>

      {/* 右栏：选中组织的成员列表 */}
      <div style={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column', minHeight: 0 }}>
        {selectedRole == null ? (
          <div style={{ flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
            <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('adminOrgRoles.emptySelection')} />
          </div>
        ) : (
          <>
            <div style={{ display: 'flex', alignItems: 'center', gap: spacing.sm, flexWrap: 'wrap', minHeight: 32 }}>
              <Space size={spacing.xs} wrap>
                <TeamOutlined />
                <Text strong>{selectedRole.name}</Text>
                <Text type="secondary" style={{ fontSize: 12 }}>
                  {selectedRole.path}
                </Text>
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
                        if (!e.target.value) setMembersQuery('')
                      }}
                      onPressEnter={applyMembersFilter}
                      allowClear
                      maxLength={50}
                      prefix={<SearchOutlined style={{ color: 'inherit' }} />}
                      placeholder={t('adminOrgRoles.membersSearchPlaceholder')}
                      style={{ width: 220 }}
                    />
                    <Button icon={<SearchOutlined />} onClick={applyMembersFilter}>
                      {t('adminOrgRoles.membersQuery')}
                    </Button>
                    {/* 竖线分隔：左侧=当前组织成员筛选，右侧=添加成员 */}
                    <Divider type="vertical" style={{ height: 22 }} />
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
                dataSource={filteredMembers}
                loading={membersLoading}
                pagination={false}
                sticky
                scroll={{ x: 900, y: 'calc(100vh - 298px)' }}
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
    </div>
  )
}
