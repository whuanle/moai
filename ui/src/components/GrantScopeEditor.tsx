import { useEffect, useMemo, useState } from 'react'
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import { Avatar, Button, Radio, Select, Space, Switch, Tag, theme, TreeSelect, Typography } from 'antd'
import type { TreeDataNode } from 'antd'
import { useTranslation } from 'react-i18next'
import { spacing } from '@/design-system/theme'
import { getOrgRoles } from '@/api/org'
import { buildGrantScopeRoleTree, type GrantScopeDirection, type GrantScopeRoleNode } from '@/utils/orgScopeTree'
import { resolveStorageUrl } from '@/utils/storage'

// 组织角色树节点与授权方向类型收敛在 @/utils/orgScopeTree（组树辅助 buildGrantScopeRoleTree 同在彼处）
export type { GrantScopeDirection, GrantScopeRoleNode }

const { Text } = Typography

/** 团队选项（带头像） */
export interface GrantScopeTeamOption {
  label: string
  value: number
  avatar?: string | null
}

/** 组织授权行：一个组织角色一个方向规则 */
export interface GrantScopeOrgGrant {
  orgRoleId: number
  scope: GrantScopeDirection
}

/** 授权范围值：全员可用开启后忽略团队/组织细粒度授权 */
export interface GrantScopeValue {
  allUsers: boolean
  teamIds: number[]
  orgGrants: GrantScopeOrgGrant[]
}

/** 下拉树节点：rawName 供搜索过滤，title 承载覆盖高亮样式 */
type ScopeTreeNode = TreeDataNode & { rawName: string }

interface GrantScopeEditorProps {
  value?: GrantScopeValue
  onChange?: (value: GrantScopeValue) => void
  /** 团队选项（带头像），由调用方拉取传入 */
  teamOptions?: GrantScopeTeamOption[]
  teamLoading?: boolean
  /** 组织角色树（按 path 层级序）；不传时组件 mount 自行拉取（getOrgRoles → 组树），调用方零样板接入 */
  roleTree?: GrantScopeRoleNode[]
  disabled?: boolean
  /** 隐藏授权团队区（仅全员开关 + 组织授权，如应用上架可视范围场景） */
  hideTeams?: boolean
}

/**
 * 授权范围编辑器（全局复用组件）：标题行为「全员可用」开关，关闭后展开
 * 授权团队（带头像多选）与授权组织（组织角色 + 方向规则 self/up/down）两组配置；
 * hideTeams 时不渲染授权团队区（仅全员开关 + 组织授权）；
 * 组织角色树不传 roleTree 时组件自行拉取（mount 一次），调用方无需各自维护加载样板；
 * 组织下拉跨行共享覆盖高亮（现有授权并集覆盖的组织以主题色高亮，已选组织禁选），
 * 作为受控组件配合 Form.Item 使用，保存语义由调用方决定（惯例为整体覆盖，全员可用时清空细粒度授权）。
 */
export function GrantScopeEditor({ value, onChange, teamOptions = [], teamLoading = false, roleTree, disabled = false, hideTeams = false }: GrantScopeEditorProps) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const emptyOrgGrants = useMemo<GrantScopeOrgGrant[]>(() => [], [])
  const allUsers = value?.allUsers ?? true
  const teamIds = value?.teamIds ?? []
  const orgGrants = value?.orgGrants ?? emptyOrgGrants
  const update = (patch: Partial<GrantScopeValue>) => onChange?.({ allUsers, teamIds, orgGrants, ...patch })

  // 组织树自加载：未传 roleTree 时 mount 拉取一次（错误已由全局请求中间件统一提示）
  const selfLoadTree = roleTree === undefined
  const [fetchedRoleTree, setFetchedRoleTree] = useState<GrantScopeRoleNode[]>([])
  const [roleTreeLoading, setRoleTreeLoading] = useState(false)
  useEffect(() => {
    if (!selfLoadTree) return
    let cancelled = false
    setRoleTreeLoading(true)
    getOrgRoles()
      .then((roles) => {
        if (!cancelled) setFetchedRoleTree(buildGrantScopeRoleTree(roles))
      })
      .catch(() => {
        // 错误已由全局请求中间件统一提示
      })
      .finally(() => {
        if (!cancelled) setRoleTreeLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [selfLoadTree])
  const tree = useMemo(
    () => (selfLoadTree ? fetchedRoleTree : (roleTree ?? [])),
    [selfLoadTree, fetchedRoleTree, roleTree],
  )

  // 角色父子关系（用于方向展开判定祖先）
  const parentOf = useMemo(() => {
    const parents = new Map<number, number | null>()
    const walk = (nodes: GrantScopeRoleNode[], parent: number | null) => {
      for (const node of nodes) {
        parents.set(node.value, parent)
        if (node.children) walk(node.children, node.value)
      }
    }
    walk(tree, null)
    return parents
  }, [tree])

  // 展开一条授权覆盖的角色集合：self 仅自身、up 自身+祖先、down 自身+子树
  const expandGrant = useMemo(() => {
    return (grant: GrantScopeOrgGrant): Set<number> => {
      const hit = new Set<number>()
      if (grant.scope === 'self') {
        hit.add(grant.orgRoleId)
        return hit
      }
      if (grant.scope === 'up') {
        let current: number | null | undefined = grant.orgRoleId
        while (current != null) {
          hit.add(current)
          current = parentOf.get(current) ?? null
        }
        return hit
      }
      const collectSubtree = (nodes: GrantScopeRoleNode[]): boolean => {
        for (const node of nodes) {
          if (node.value === grant.orgRoleId) {
            const collect = (sub: GrantScopeRoleNode[]) => {
              for (const item of sub) {
                hit.add(item.value)
                if (item.children) collect(item.children)
              }
            }
            collect(node.children ?? [])
            return true
          }
          if (node.children && collectSubtree(node.children)) return true
        }
        return false
      }
      collectSubtree(tree)
      return hit
    }
  }, [parentOf, tree])

  // 全部授权的并集覆盖（跨行共享高亮）
  const coveredRoles = useMemo(() => {
    const set = new Set<number>()
    for (const grant of orgGrants) {
      for (const id of expandGrant(grant)) set.add(id)
    }
    return set
  }, [orgGrants, expandGrant])

  // 每行下拉的树：被其它行选中的组织禁选，被覆盖的组织高亮
  const treeForRow = (rowIndex: number): ScopeTreeNode[] => {
    const pickedByOthers = new Set(
      orgGrants.filter((_, i) => i !== rowIndex).map((row) => row.orgRoleId),
    )
    const mark = (nodes: GrantScopeRoleNode[]): ScopeTreeNode[] =>
      nodes.map((node) => ({
        key: node.value,
        value: node.value,
        rawName: node.title,
        disabled: pickedByOthers.has(node.value),
        title: coveredRoles.has(node.value)
          ? <span style={{ color: token.colorPrimary, fontWeight: 600 }}>{node.title}</span>
          : node.title,
        children: node.children ? mark(node.children) : undefined,
      }))
    return mark(tree)
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: spacing.xs }}>
      <Space size={spacing.sm}>
        <Switch checked={allUsers} disabled={disabled} onChange={(checked) => update({ allUsers: checked })} />
        <Text>{t('grantScope.allUsers')}</Text>
      </Space>
      {!allUsers && (
        <>
          {!hideTeams && (
            <>
              <Text strong style={{ fontSize: 13, marginTop: spacing.xs }}>
                {t('grantScope.teamTitle')}
              </Text>
              <Select
            mode="multiple"
            value={teamIds}
            options={teamOptions}
            loading={teamLoading}
            disabled={disabled}
            placeholder={t('grantScope.teamPlaceholder')}
            showSearch
            optionFilterProp="label"
            onChange={(ids) => update({ teamIds: ids })}
            tagRender={(tagProps) => {
              // 选中标签同样显示团队头像（选项缺失时回退首字母）
              const { label, value, closable, onClose } = tagProps
              const team = teamOptions.find((item) => item.value === value)
              const name = String(label ?? '')
              return (
                <Tag
                  style={{ display: 'inline-flex', alignItems: 'center', gap: 4, marginInlineEnd: 4 }}
                  closable={closable && !disabled}
                  onClose={onClose}
                  onMouseDown={(event) => {
                    event.preventDefault();
                    event.stopPropagation();
                  }}
                >
                  <Avatar shape="square" size={14} src={resolveStorageUrl(team?.avatar ?? null) || undefined} alt={name}>
                    {name.slice(0, 1).toUpperCase()}
                  </Avatar>
                  <span>{name}</span>
                </Tag>
              )
            }}
            optionRender={(option) => {
              // 下拉项带团队头像；label 保持纯文本，选中标签与搜索过滤不受影响
              const team = option as unknown as GrantScopeTeamOption
              const name = team.label || String(team.value)
              return (
                <span style={{ display: 'inline-flex', alignItems: 'center', gap: spacing.sm }}>
                  <Avatar shape="square" size={20} src={resolveStorageUrl(team.avatar ?? null) || undefined} alt={name}>
                    {name.slice(0, 1).toUpperCase()}
                  </Avatar>
                  <span>{name}</span>
                </span>
              )
            }}
            style={{ width: '100%' }}
          />
              <Text type="secondary" style={{ fontSize: 12 }}>
                {t('grantScope.teamHint')}
              </Text>
            </>
          )}
          <Text strong style={{ fontSize: 13, marginTop: spacing.xs }}>
            {t('grantScope.orgTitle')}
          </Text>
          {orgGrants.map((row, index) => (
            <div key={`${row.orgRoleId}-${index}`} style={{ display: 'flex', alignItems: 'center', gap: spacing.xs }}>
              <TreeSelect
                value={row.orgRoleId > 0 ? row.orgRoleId : undefined}
                treeData={treeForRow(index)}
                showSearch
                filterTreeNode={(input, node) =>
                  !input || String((node as ScopeTreeNode | undefined)?.rawName ?? '').toLowerCase().includes(input.toLowerCase())
                }
                // 下拉宽度自适应内容（最小为触发器宽）：窄容器里组织名依然完整可读
                popupMatchSelectWidth={false}
                style={{ flex: 1, minWidth: 220 }}
                loading={roleTreeLoading}
                disabled={disabled}
                placeholder={t('grantScope.orgRolePlaceholder')}
                onChange={(roleId) => {
                  if (!roleId) return
                  const next = [...orgGrants]
                  next[index] = { ...row, orgRoleId: Number(roleId) }
                  update({ orgGrants: next })
                }}
              />
              <Radio.Group
                value={row.scope}
                optionType="button"
                buttonStyle="solid"
                disabled={disabled}
                style={{ flexShrink: 0 }}
                options={[
                  { label: t('grantScope.orgSelf'), value: 'self' },
                  { label: t('grantScope.orgUp'), value: 'up' },
                  { label: t('grantScope.orgDown'), value: 'down' },
                ]}
                onChange={(e) => {
                  const next = [...orgGrants]
                  next[index] = { ...row, scope: e.target.value as GrantScopeDirection }
                  update({ orgGrants: next })
                }}
              />
              <Button
                type="text"
                size="small"
                danger
                icon={<DeleteOutlined />}
                aria-label={t('common.delete')}
                disabled={disabled}
                onClick={() => update({ orgGrants: orgGrants.filter((_, i) => i !== index) })}
              />
            </div>
          ))}
          <div>
            <Button size="small" icon={<PlusOutlined />} disabled={disabled} onClick={() => update({ orgGrants: [...orgGrants, { orgRoleId: 0, scope: 'down' }] })}>
              {t('grantScope.addOrgGrant')}
            </Button>
          </div>
          <Text type="secondary" style={{ fontSize: 12 }}>
            {t('grantScope.orgHint')}
          </Text>
        </>
      )}
    </div>
  )
}
