// 角色组织管理 · E2E（场景 @ORG-S1..）
// 覆盖：管理员门禁 / 角色树创建（path 物化路径计算）/ 同父重名 / 改名不动 path /
//       子树整体迁移（path 重写）/ 防环 / 删除保护与级联解绑 / 用户角色分配与整体覆盖。
// 前置：后端运行中；种子账号 admin / abcd123456（root 即管理员）。全部用例零真实外部依赖。
// 用法：node local-dev/org-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5000，可用 ORG_BASE 覆盖）
import crypto from 'node:crypto'

const BASE = process.env.ORG_BASE ?? process.argv[2] ?? 'http://127.0.0.1:5000'
let PASS = 0
let FAIL = 0
const check = (name, cond, detail = '') => {
  if (cond) {
    PASS++
    console.log(`PASS | ${name}`)
  } else {
    FAIL++
    console.log(`FAIL | ${name}${detail ? ' — ' + detail : ''}`)
  }
}

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
  return { status: res.status, json, text }
}

const TS = Date.now().toString().slice(-8)
let seq = 0
const uname = (p) => `${p}${TS}${String(seq++).padStart(2, '0')}`
const phone = () => `15${Date.now().toString().slice(-8)}${String(seq++).padStart(2, '0')}`.slice(0, 11)

async function mkuser(p) {
  const name = uname(p)
  const r = await api('POST', '/api/auth/register', { body: { userName: name, email: `${name}@test.local`, nickName: name, phone: phone(), password: rsa('Test1234') } })
  if (r.status !== 200) throw new Error(`注册 ${name} 失败: ${r.status} ${r.text.slice(0, 120)}`)
  const l = await api('POST', '/api/auth/login', { body: { userName: name, password: rsa('Test1234') } })
  return { name, userId: Number(l.json.userId), token: l.json.accessToken }
}

/** 创建角色并断言返回 path（失败时抛异常终止） */
async function mkrole(token, name, parentId) {
  const r = await api('POST', '/api/org/role/create', { token, body: { name, parentId: parentId ?? null } })
  if (r.status !== 200) throw new Error(`创建角色 ${name} 失败: ${r.status} ${r.text.slice(0, 160)}`)
  return { id: r.json.orgRoleId, path: r.json.path }
}

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const rootLogin = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
  if (rootLogin.status !== 200) throw new Error('种子账号 admin 登录失败，无法执行管理员场景')
  const root = rootLogin.json.accessToken

  const user = await mkuser('ou') // 普通用户（非管理员）
  const member1 = await mkuser('om') // 角色成员 1
  const member2 = await mkuser('on') // 角色成员 2

  // ---- 管理员门禁 ----
  check('ORG-01 非管理员查询角色列表 403', (await api('GET', '/api/org/role/list', { token: user.token })).status === 403)
  check('ORG-02 非管理员创建角色 403', (await api('POST', '/api/org/role/create', { token: user.token, body: { name: 'x', parentId: null } })).status === 403)
  check('ORG-03 非管理员分配用户角色 403', (await api('PUT', `/api/org/user/${member1.userId}/roles`, { token: user.token, body: { roleIds: [1] } })).status === 403)

  // ---- 创建与 path 计算 ----
  const invalid = await api('POST', '/api/org/role/create', { token: root, body: { name: '', parentId: null } })
  check('ORG-04 空名称 400', invalid.status === 400, `${invalid.status} ${invalid.text.slice(0, 120)}`)

  const ghostParent = await api('POST', '/api/org/role/create', { token: root, body: { name: '孤儿', parentId: 999999 } })
  check('ORG-05 上级角色不存在 404', ghostParent.status === 404, `${ghostParent.status} ${ghostParent.text.slice(0, 120)}`)

  // 树：A(root) ── B ── C；D(root)
  const A = await mkrole(root, `集团A${TS}`)
  check('ORG-06 根角色 path=/id/', A.path === `/${A.id}/`, JSON.stringify(A))

  const B = await mkrole(root, `销售部${TS}`, A.id)
  check('ORG-07 子角色 path 追加父链', B.path === `/${A.id}/${B.id}/`, JSON.stringify(B))

  const C = await mkrole(root, `华东组${TS}`, B.id)
  check('ORG-08 孙角色 path 三级链', C.path === `/${A.id}/${B.id}/${C.id}/`, JSON.stringify(C))

  const D = await mkrole(root, `集团D${TS}`)
  check('ORG-09 第二个根角色独立 path', D.path === `/${D.id}/` && D.id !== A.id)

  const dup = await api('POST', '/api/org/role/create', { token: root, body: { name: `销售部${TS}`, parentId: A.id } })
  check('ORG-10 同父重名 409', dup.status === 409)

  const sameNameOtherParent = await mkrole(root, `销售部${TS}`, D.id)
  check('ORG-11 不同父允许同名', sameNameOtherParent.path === `/${D.id}/${sameNameOtherParent.id}/`)

  // ---- 列表与搜索 ----
  const list = await api('GET', '/api/org/role/list', { token: root })
  const items = list.json?.items ?? []
  const idxA = items.findIndex((x) => x.orgRoleId === A.id)
  const idxB = items.findIndex((x) => x.orgRoleId === B.id)
  const idxC = items.findIndex((x) => x.orgRoleId === C.id)
  const idxD = items.findIndex((x) => x.orgRoleId === D.id)
  check(
    'ORG-12 列表按 path 层级序（父先于子、根之间按序）',
    list.status === 200 && idxA >= 0 && idxB > idxA && idxC > idxB && idxD > idxA,
    `idx=${idxA}/${idxB}/${idxC}/${idxD}`,
  )
  const itemA = items[idxA]
  check('ORG-13 列表回传 hasChildren/parentId/path', itemA?.hasChildren === true && itemA?.parentId == null && itemA?.path === A.path, JSON.stringify(itemA ?? {}).slice(0, 160))

  const searched = await api('GET', `/api/org/role/list?search=${encodeURIComponent(`华东组${TS}`)}`, { token: root })
  check('ORG-14 名称搜索只命中目标', searched.status === 200 && (searched.json?.items ?? []).every((x) => x.name === `华东组${TS}`) && (searched.json?.items ?? []).length === 1)

  // ---- 更新：改名与移动 ----
  const renamed = await api('PUT', `/api/org/role/${B.id}`, { token: root, body: { name: `市场部${TS}`, parentId: A.id } })
  const afterRename = (await api('GET', '/api/org/role/list', { token: root })).json.items.find((x) => x.orgRoleId === B.id)
  check('ORG-15 改名成功且 path 不变', renamed.status === 200 && afterRename?.path === B.path && afterRename?.name === `市场部${TS}`, JSON.stringify(afterRename ?? {}))

  // 移动子树：B(+C) 从 A 移到 D 下
  const moved = await api('PUT', `/api/org/role/${B.id}`, { token: root, body: { name: `市场部${TS}`, parentId: D.id } })
  const afterMove = (await api('GET', '/api/org/role/list', { token: root })).json.items
  const bMoved = afterMove.find((x) => x.orgRoleId === B.id)
  const cMoved = afterMove.find((x) => x.orgRoleId === C.id)
  check(
    'ORG-16 子树整体迁移重写 path（本角色与后代）',
    moved.status === 200 && bMoved?.path === `/${D.id}/${B.id}/` && bMoved?.parentId === D.id && cMoved?.path === `/${D.id}/${B.id}/${C.id}/` && cMoved?.parentId === B.id,
    `b=${bMoved?.path} c=${cMoved?.path}`,
  )

  // 移回根
  const toRoot = await api('PUT', `/api/org/role/${B.id}`, { token: root, body: { name: `市场部${TS}`, parentId: null } })
  const afterToRoot = (await api('GET', '/api/org/role/list', { token: root })).json.items
  check(
    'ORG-17 移到根 path 变顶级',
    toRoot.status === 200 && afterToRoot.find((x) => x.orgRoleId === B.id)?.path === `/${B.id}/` && afterToRoot.find((x) => x.orgRoleId === C.id)?.path === `/${B.id}/${C.id}/`,
  )

  // 防环
  check('ORG-18 移到自身之下 400', (await api('PUT', `/api/org/role/${B.id}`, { token: root, body: { name: `市场部${TS}`, parentId: C.id } })).status === 400)
  check('ORG-19 自身设为上级 400', (await api('PUT', `/api/org/role/${B.id}`, { token: root, body: { name: `市场部${TS}`, parentId: B.id } })).status === 400)
  check('ORG-20 更新不存在的角色 404', (await api('PUT', '/api/org/role/999999', { token: root, body: { name: 'x', parentId: null } })).status === 404)

  // ---- 用户角色分配 ----
  check('ORG-21 用户不存在 404', (await api('PUT', '/api/org/user/999999/roles', { token: root, body: { roleIds: [A.id] } })).status === 404)
  check('ORG-22 角色不存在 400', (await api('PUT', `/api/org/user/${member1.userId}/roles`, { token: root, body: { roleIds: [A.id, 999999] } })).status === 400)

  const assign1 = await api('PUT', `/api/org/user/${member1.userId}/roles`, { token: root, body: { roleIds: [A.id, B.id] } })
  check('ORG-23 分配两角色 200', assign1.status === 200, `${assign1.status} ${assign1.text.slice(0, 120)}`)

  const ur1 = await api('GET', `/api/org/user/${member1.userId}/roles`, { token: root })
  check(
    'ORG-24 用户角色回显按 path 序',
    ur1.status === 200 && ur1.json?.items?.map((x) => x.orgRoleId).join(',') === [A.id, B.id].join(',') && ur1.json?.userName === member1.name,
    JSON.stringify(ur1.json ?? {}).slice(0, 160),
  )

  const assignDup = await api('PUT', `/api/org/user/${member2.userId}/roles`, { token: root, body: { roleIds: [B.id, B.id, C.id] } })
  const ur2 = await api('GET', `/api/org/user/${member2.userId}/roles`, { token: root })
  check('ORG-25 分配去重与整体覆盖', assignDup.status === 200 && ur2.json?.items?.length === 2, JSON.stringify(ur2.json?.items ?? []))

  const roleUsers = await api('GET', `/api/org/role/${B.id}/users`, { token: root })
  const roleUserIds = (roleUsers.json?.items ?? []).map((x) => Number(x.userId))
  check(
    'ORG-26 角色下成员清单含两个用户',
    roleUsers.status === 200 && roleUserIds.includes(member1.userId) && roleUserIds.includes(member2.userId),
    JSON.stringify(roleUsers.json ?? {}).slice(0, 160),
  )

  const roleUsersSearch = await api('GET', `/api/org/role/${B.id}/users?search=${encodeURIComponent(member1.name)}`, { token: root })
  check('ORG-27 成员搜索按用户名过滤', (roleUsersSearch.json?.items ?? []).length === 1 && Number(roleUsersSearch.json.items[0].userId) === member1.userId)

  // 整体覆盖：member1 只保留 B
  const reassign = await api('PUT', `/api/org/user/${member1.userId}/roles`, { token: root, body: { roleIds: [B.id] } })
  const ur1b = await api('GET', `/api/org/user/${member1.userId}/roles`, { token: root })
  check('ORG-28 整体覆盖后仅剩一角色', reassign.status === 200 && ur1b.json?.items?.length === 1 && ur1b.json.items[0].orgRoleId === B.id)

  // ---- 删除保护与级联解绑 ----
  check('ORG-29 删除有子角色 400', (await api('DELETE', `/api/org/role/${B.id}`, { token: root })).status === 400)

  const leaf = await mkrole(root, `临时组${TS}`, C.id)
  await api('PUT', `/api/org/user/${member1.userId}/roles`, { token: root, body: { roleIds: [leaf.id] } })
  const delLeaf = await api('DELETE', `/api/org/role/${leaf.id}`, { token: root })
  const urAfterDel = await api('GET', `/api/org/user/${member1.userId}/roles`, { token: root })
  check(
    'ORG-30 删除叶子角色并级联解绑',
    delLeaf.status === 200 && urAfterDel.json?.items?.length === 0,
    `${delLeaf.status} ${JSON.stringify(urAfterDel.json?.items ?? [])}`,
  )
  check('ORG-31 删除后列表不再出现', ((await api('GET', '/api/org/role/list', { token: root })).json?.items ?? []).every((x) => x.orgRoleId !== leaf.id))
  check('ORG-32 重复删除 404', (await api('DELETE', `/api/org/role/${leaf.id}`, { token: root })).status === 404)

  // ---- 默认角色（注册自动赋予）----
  const defRole = await mkrole(root, `默认角色${TS}`, null)

  check('ORG-33 非管理员设置默认角色 403', (await api('PUT', '/api/org/role/default', { token: user.token, body: { orgRoleId: defRole.id } })).status === 403)
  check('ORG-34 设置不存在的默认角色 404', (await api('PUT', '/api/org/role/default', { token: root, body: { orgRoleId: 999999 } })).status === 404)

  const setDefault = await api('PUT', '/api/org/role/default', { token: root, body: { orgRoleId: defRole.id } })
  const listWithDefault = (await api('GET', '/api/org/role/list', { token: root })).json?.items ?? []
  const defItem = listWithDefault.find((x) => x.orgRoleId === defRole.id)
  check(
    'ORG-35 设置默认成功且列表回传 isDefault',
    setDefault.status === 200 && defItem?.isDefault === true && listWithDefault.filter((x) => x.isDefault).length === 1,
    JSON.stringify(defItem ?? {}).slice(0, 120),
  )

  const autoUser = await mkuser('od') // 注册于默认角色生效期间
  const autoUserRoles = await api('GET', `/api/org/user/${autoUser.userId}/roles`, { token: root })
  check(
    'ORG-36 注册新用户自动获得默认角色',
    (autoUserRoles.json?.items ?? []).some((x) => x.orgRoleId === defRole.id),
    JSON.stringify(autoUserRoles.json?.items ?? []).slice(0, 120),
  )

  const clearDefault = await api('PUT', '/api/org/role/default', { token: root, body: { orgRoleId: null } })
  const noDefaultUser = await mkuser('oe') // 注册于默认角色清空后
  const noDefaultRoles = await api('GET', `/api/org/user/${noDefaultUser.userId}/roles`, { token: root })
  check(
    'ORG-37 清空默认后新注册用户无角色',
    clearDefault.status === 200 && (noDefaultRoles.json?.items ?? []).length === 0,
    JSON.stringify(noDefaultRoles.json?.items ?? []).slice(0, 120),
  )

  await api('PUT', '/api/org/role/default', { token: root, body: { orgRoleId: defRole.id } })
  const delDefault = await api('DELETE', `/api/org/role/${defRole.id}`, { token: root })
  const listAfterDel = (await api('GET', '/api/org/role/list', { token: root })).json?.items ?? []
  const afterDelUser = await mkuser('of') // 注册于默认角色被删后
  const afterDelRoles = await api('GET', `/api/org/user/${afterDelUser.userId}/roles`, { token: root })
  check(
    'ORG-38 删除默认角色自动清空设置且新注册无角色',
    delDefault.status === 200 && listAfterDel.every((x) => x.isDefault !== true) && (afterDelRoles.json?.items ?? []).length === 0,
    `${delDefault.status} items=${(afterDelRoles.json?.items ?? []).length}`,
  )

  // ---- 清理（尽力而为）----
  for (const roleId of [C.id, B.id, sameNameOtherParent.id, A.id, D.id]) {
    await api('DELETE', `/api/org/role/${roleId}`, { token: root })
  }
  // 成员用户的角色集合随角色删除自动清空，无需单独清理

  console.log(`\n结果: PASS=${PASS} FAIL=${FAIL}`)
  if (FAIL > 0) process.exit(1)
}

main().catch((err) => {
  console.error('E2E 执行异常:', err)
  process.exit(1)
})
