import type { OrgRoleItem } from '@/api/org'

/** 组织授权方向：self=仅该组织 up=该组织及以上 down=该组织及以下（规范定义，api 层与组件层统一引用） */
export type GrantScopeDirection = 'self' | 'up' | 'down'

/** 组织角色树节点（授权范围编辑器用） */
export interface GrantScopeRoleNode {
  title: string
  value: number
  children?: GrantScopeRoleNode[]
}

/** 组织平铺列表（按 path 层级序）组树（全局复用：子系统授权/应用上架可视范围等） */
export function buildGrantScopeRoleTree(roles: OrgRoleItem[]): GrantScopeRoleNode[] {
  const nodes = new Map<number, GrantScopeRoleNode>()
  for (const role of roles) {
    nodes.set(Number(role.orgRoleId), {
      title: role.name || `#${role.orgRoleId}`,
      value: Number(role.orgRoleId),
    })
  }
  const roots: GrantScopeRoleNode[] = []
  for (const role of roles) {
    const node = nodes.get(Number(role.orgRoleId))!
    const parentId = role.parentId == null ? null : Number(role.parentId)
    const parent = parentId == null ? null : nodes.get(parentId)
    if (parent) {
      parent.children = [...(parent.children ?? []), node]
    } else {
      roots.push(node)
    }
  }
  return roots
}
