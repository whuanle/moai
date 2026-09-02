// 用户管理 E2E 验证脚本（对应 docs/user-management/bdd.md 后端场景）
// 用法: node user-management-e2e.mjs [baseUrl]
const BASE = process.argv[2] ?? 'http://127.0.0.1:5210'
const crypto = await import('node:crypto')

let pass = 0
let fail = 0

function check(name, cond, detail = '') {
  if (cond) {
    pass++
    console.log(`PASS ${name}`)
  } else {
    fail++
    console.log(`FAIL ${name} ${detail}`)
  }
}

const { rsaPublic } = await fetch(`${BASE}/api/common/serverinfo`).then((r) => r.json())

function enc(text) {
  const key = crypto.createPublicKey({ key: Buffer.from(rsaPublic, 'base64'), format: 'der', type: 'spki' })
  return crypto.publicEncrypt({ key, padding: crypto.constants.RSA_PKCS1_PADDING }, Buffer.from(text)).toString('base64')
}

async function register(userName, password) {
  const r = await fetch(`${BASE}/api/auth/register`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      userName,
      email: `${userName}@test.local`,
      nickName: userName,
      phone: `138${String(Math.floor(Math.random() * 1e8)).padStart(8, '0')}`,
      password: enc(password),
    }),
  })
  return r.status
}

async function login(userName, password) {
  const r = await fetch(`${BASE}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ userName, password: enc(password) }),
  })
  const body = await r.json()
  return body?.accessToken ?? null
}

async function api(token, path, method = 'GET', payload) {
  const r = await fetch(`${BASE}${path}`, {
    method,
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
    },
    body: payload === undefined ? undefined : JSON.stringify(payload),
  })
  let body = null
  try { body = await r.json() } catch { /* no body */ }
  return { status: r.status, body }
}

// ---------- 准备数据 ----------
const ts = Date.now().toString().slice(-6)
const bobName = `bob${ts}`
const aliceName = `alice${ts}`
check('注册 bob', (await register(bobName, 'bob12345')) === 200)
check('注册 alice', (await register(aliceName, 'alice12345')) === 200)

const adminToken = await login('admin', 'abcd123456')
check('admin(root) 登录', Boolean(adminToken))
let bobToken = await login(bobName, 'bob12345')
check('bob 登录', Boolean(bobToken))
const aliceToken = await login(aliceName, 'alice12345')
check('alice 登录', Boolean(aliceToken))

// 用户 id 查询
const list0 = await api(adminToken, '/api/usermanage/users?pageNo=1&pageSize=500')
const adminItem = list0.body?.items?.find((u) => u.userName === 'admin')
const bobItem = list0.body?.items?.find((u) => u.userName === bobName)
const aliceItem = list0.body?.items?.find((u) => u.userName === aliceName)

// ---------- Feature: 用户列表 ----------
check('列表 200', list0.status === 200)
check('列表含 admin 且 isRoot=true', adminItem?.isRoot === true && adminItem?.isAdmin === true)
check('列表含 bob/alice', Boolean(bobItem && aliceItem))
check('列表项字段完整', ['id', 'userName', 'nickName', 'email', 'phone', 'isAdmin', 'isRoot', 'isDisable', 'createTime'].every((k) => k in (list0.body?.items?.[0] ?? {})))

const searched = await api(adminToken, `/api/usermanage/users?pageNo=1&pageSize=20&searchText=${bobName}`)
check('关键字搜索命中 bob', searched.status === 200 && searched.body?.items?.length === 1)

const memberList = await api(bobToken, '/api/usermanage/users')
check('普通用户访问列表 403', memberList.status === 403)

// ---------- Feature: 查看用户信息 ----------
const detail = await api(adminToken, `/api/usermanage/user/${bobItem.id}`)
check('管理员查看用户信息 200', detail.status === 200 && detail.body?.userName === bobName)
const detail404 = await api(adminToken, '/api/usermanage/user/999999')
check('查看不存在用户 404', detail404.status === 404)

// ---------- Feature: 设置管理员 ----------
const grantByMember = await api(bobToken, `/api/usermanage/user/${aliceItem.id}/isadmin`, 'PUT', { isAdmin: true })
check('普通用户授权管理员 403', grantByMember.status === 403)

const grant = await api(adminToken, `/api/usermanage/user/${bobItem.id}/isadmin`, 'PUT', { isAdmin: true })
check('root 授权 bob 管理员', grant.status === 200)
const listAfterGrant = await api(adminToken, '/api/usermanage/users?pageNo=1&pageSize=500')
check('授权后列表 isAdmin=true', listAfterGrant.body?.items?.find((u) => u.userName === bobName)?.isAdmin === true)
bobToken = await login(bobName, 'bob12345')
const bobAsAdminList = await api(bobToken, '/api/usermanage/users')
check('bob 成为管理员后可访问列表', bobAsAdminList.status === 200)

const grantToRoot = await api(adminToken, '/api/usermanage/user/1/isadmin', 'PUT', { isAdmin: false })
check('不能操作 root 账号 400', grantToRoot.status === 400)
const grantSelf = await api(adminToken, `/api/usermanage/user/1/isadmin`, 'PUT', { isAdmin: false })
check('不能操作自己 400', grantSelf.status === 400)

// ---------- Feature: 管理员不能操作管理员 ----------
const adminDisableAdmin = await api(bobToken, `/api/usermanage/user/${aliceItem.id}/isadmin`, 'PUT', { isAdmin: true })
check('管理员授权管理员 403（接口门禁）', adminDisableAdmin.status === 403)

// 将 alice 也设为管理员，验证 admin 不能禁用/重置另一个 admin
await api(adminToken, `/api/usermanage/user/${aliceItem.id}/isadmin`, 'PUT', { isAdmin: true })
const bobDisablesAlice = await api(bobToken, `/api/usermanage/user/${aliceItem.id}/isdisable`, 'PUT', { isDisable: true })
check('管理员禁用另一个管理员 403', bobDisablesAlice.status === 403)
const bobResetsAlice = await api(bobToken, `/api/usermanage/user/${aliceItem.id}/password`, 'PUT', { newPassword: enc('newpass123') })
check('管理员重置另一个管理员密码 403', bobResetsAlice.status === 403)
const rootDisablesAlice = await api(adminToken, `/api/usermanage/user/${aliceItem.id}/isdisable`, 'PUT', { isDisable: true })
check('root 可以禁用管理员', rootDisablesAlice.status === 200)
await api(adminToken, `/api/usermanage/user/${aliceItem.id}/isdisable`, 'PUT', { isDisable: false })
await api(adminToken, `/api/usermanage/user/${aliceItem.id}/isadmin`, 'PUT', { isAdmin: false })

// ---------- Feature: 禁用/启用 ----------
const disableSelf = await api(bobToken, `/api/usermanage/user/${bobItem.id}/isdisable`, 'PUT', { isDisable: true })
check('不能禁用自己 400', disableSelf.status === 400)

const disableBob = await api(adminToken, `/api/usermanage/user/${bobItem.id}/isdisable`, 'PUT', { isDisable: true })
check('root 禁用 bob', disableBob.status === 200)
const bobDisabled = await api(bobToken, '/api/usermanage/users')
check('禁用后请求被拦截 403', bobDisabled.status === 403)

const enableBob = await api(adminToken, `/api/usermanage/user/${bobItem.id}/isdisable`, 'PUT', { isDisable: false })
check('root 启用 bob', enableBob.status === 200)
bobToken = await login(bobName, 'bob12345')
check('启用后恢复', (await api(bobToken, '/api/usermanage/users')).status === 200)

const disableRoot = await api(bobToken, '/api/usermanage/user/1/isdisable', 'PUT', { isDisable: true })
check('不能禁用 root 400', disableRoot.status === 400)

// ---------- Feature: 重置密码 ----------
const weakReset = await api(adminToken, `/api/usermanage/user/${bobItem.id}/password`, 'PUT', { newPassword: enc('1234') })
check('弱密码 400', weakReset.status === 400)

const reset = await api(adminToken, `/api/usermanage/user/${bobItem.id}/password`, 'PUT', { newPassword: enc('newpass123') })
check('root 重置 bob 密码', reset.status === 200)
const oldLogin = await login(bobName, 'bob12345')
check('旧密码登录失败', !oldLogin)
const newLogin = await login(bobName, 'newpass123')
check('新密码登录成功', Boolean(newLogin))

const resetRoot = await api(adminToken, '/api/usermanage/user/1/password', 'PUT', { newPassword: enc('newpass123') })
check('不能重置 root 密码 400', resetRoot.status === 400)

// ---------- 汇总 ----------
console.log(`\n结果: ${pass} 通过, ${fail} 失败`)
process.exit(fail > 0 ? 1 : 0)
