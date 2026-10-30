// 组织架构种子脚本（一次性/可重跑）
// 作用：登录管理员 → 建一棵集团组织树（按名查重跳过已存在）→ 把现有用户逐个拉进部门（在用户现有组织之上追加，不覆盖）。
// 规则：跳过 admin/root 用户；每个用户分配一个部门（按部门列表轮转，研发组权重更高）。
// 用法：node local-dev/org-seed.mjs [baseUrl]（默认 http://127.0.0.1:5000）
import crypto from 'node:crypto'

const BASE = process.env.ORG_BASE ?? process.argv[2] ?? 'http://127.0.0.1:5000'

let RSA_KEY = ''
const rsa = (plain) => {
  const key = crypto.createPublicKey({ key: Buffer.from(RSA_KEY, 'base64'), format: 'der', type: 'spki' })
  return crypto.publicEncrypt({ key, padding: crypto.constants.RSA_PKCS1_PADDING }, Buffer.from(plain, 'utf8')).toString('base64')
}

async function api(method, path, { token, body } = {}) {
  const headers = {}
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  if (token) headers['Authorization'] = `Bearer ${token}`
  const res = await fetch(BASE + path, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) })
  const text = await res.text()
  let json = null
  try { json = JSON.parse(text) } catch { /* 非 JSON */ }
  if (res.status !== 200) throw new Error(`${method} ${path} -> ${res.status} ${text.slice(0, 160)}`)
  return json
}

/** 组织树定义：key 为唯一标识，name 为部门名，parent 指向父 key（null=顶级） */
const ORG_TREE = [
  { key: 'hq', name: '星河集团', parent: null },
  { key: 'office', name: '总裁办', parent: 'hq' },
  { key: 'hr', name: '人力资源部', parent: 'hq' },
  { key: 'finance', name: '财务部', parent: 'hq' },
  { key: 'tech', name: '技术中心', parent: 'hq' },
  { key: 'backend', name: '后端研发组', parent: 'tech' },
  { key: 'frontend', name: '前端研发组', parent: 'tech' },
  { key: 'ai', name: 'AI 平台组', parent: 'tech' },
  { key: 'qa', name: '测试组', parent: 'tech' },
  { key: 'sales', name: '销售中心', parent: 'hq' },
  { key: 'sales-east', name: '华东销售部', parent: 'sales' },
  { key: 'sales-south', name: '华南销售部', parent: 'sales' },
  { key: 'sales-north', name: '华北销售部', parent: 'sales' },
  { key: 'marketing', name: '市场部', parent: 'hq' },
  { key: 'cs', name: '客户成功部', parent: 'hq' },
]

/** 用户分配轮转表：研发/销售权重高，出现次数即分配权重 */
const ASSIGN_CYCLE = [
  'office',
  'hr',
  'finance',
  'backend', 'backend', 'backend',
  'frontend', 'frontend',
  'ai', 'ai',
  'qa',
  'sales-east', 'sales-east',
  'sales-south',
  'sales-north',
  'marketing',
  'cs',
]

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.rsaPublic

  const login = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
  const token = login.accessToken

  // ---- 1. 建组织树（按名查重，已存在即复用） ----
  const existing = (await api('GET', '/api/org/role/list', { token })).items ?? []
  const byName = new Map(existing.map((x) => [x.name, x]))
  const ids = new Map()

  for (const node of ORG_TREE) {
    if (byName.has(node.name)) {
      ids.set(node.key, byName.get(node.name).orgRoleId)
      console.log(`SKIP | 组织已存在 ${node.name} (id=${byName.get(node.name).orgRoleId})`)
      continue
    }
    const parentId = node.parent == null ? null : ids.get(node.parent)
    if (node.parent != null && parentId == null) throw new Error(`父组织缺失：${node.parent}`)
    const created = await api('POST', '/api/org/role/create', { token, body: { name: node.name, parentId } })
    ids.set(node.key, created.orgRoleId)
    console.log(`CREATE | 组织 ${node.name} (id=${created.orgRoleId}, path=${created.path})`)
  }

  // ---- 2. 拉用户进组织 ----
  const users = []
  for (let pageNo = 1; ; pageNo++) {
    const page = await api('GET', `/api/usermanage/users?pageNo=${pageNo}&pageSize=50`, { token })
    const items = page.items ?? []
    users.push(...items)
    if (users.length >= (page.totalCount ?? 0) || items.length === 0) break
  }
  console.log(`\n用户总数 ${users.length}（跳过 admin/root）`)

  const assignable = users.filter((u) => !u.isRoot && !u.isAdmin)
  const admins = users.filter((u) => u.isRoot || u.isAdmin)
  let cycle = 0
  let assigned = 0
  let untouched = 0

  for (const u of assignable) {
    const targetKey = ASSIGN_CYCLE[cycle % ASSIGN_CYCLE.length]
    cycle++
    const targetId = ids.get(targetKey)
    const current = (await api('GET', `/api/org/user/${u.id}/roles`, { token })).items ?? []
    const currentIds = current.map((x) => x.orgRoleId)
    if (currentIds.includes(targetId)) {
      untouched++
      continue
    }
    // 在现有组织之上追加，不覆盖用户已有归属
    await api('PUT', `/api/org/user/${u.id}/roles`, { token, body: { roleIds: [...currentIds, targetId] } })
    assigned++
    console.log(`ASSIGN | ${u.userName}(${u.id}) -> ${ORG_TREE.find((n) => n.key === targetKey).name}`)
  }
  console.log(`跳过管理员（不参与分配）：${admins.map((u) => u.userName).join(', ') || '无'}`)

  // ---- 3. 汇总 ----
  const finalList = (await api('GET', '/api/org/role/list', { token })).items ?? []
  console.log('\n==== 组织架构总览 ====')
  const nameById = new Map(finalList.map((x) => [x.orgRoleId, x.name]))
  for (const node of ORG_TREE) {
    const id = ids.get(node.key)
    const item = finalList.find((x) => x.orgRoleId === id)
    const depth = (item?.path ?? '').split('/').filter(Boolean).length - 1
    console.log(`${'  '.repeat(Math.max(depth, 0))}${node.name} — 直属成员 ${item?.userCount ?? 0}（id=${id}）`)
  }
  console.log(`\n完成：新建 ${ORG_TREE.length - byName.size} 个组织，新分配 ${assigned} 人，已在其位 ${untouched} 人。`)
}

main().catch((err) => {
  console.error('种子脚本执行失败:', err.message ?? err)
  process.exit(1)
})
