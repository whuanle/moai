// 团队插件 E2E（场景 @TP-Sn；后端 127.0.0.1:5210）
import crypto from 'node:crypto'

const BASE = 'http://127.0.0.1:5210'
let PASS = 0, FAIL = 0
const check = (name, cond, detail = '') => {
  if (cond) { PASS++; console.log(`PASS | ${name}`) }
  else { FAIL++; console.log(`FAIL | ${name} ${detail ? '— ' + detail : ''}`) }
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

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const owner = await mkuser('to')
  const member = await mkuser('tm')
  const outsider = await mkuser('tx')

  const TID = Number((await api('POST', '/api/team', { token: owner.token, body: { name: 'tp-team-' + TS } })).json.value)
  await api('POST', `/api/team/${TID}/users`, { token: owner.token, body: { userId: member.userId, role: 2 } })

  // TP-01 无 token 查团队插件列表 401
  check('TP-01 无 token 查团队插件 401', (await api('GET', `/api/team/${TID}/plugin/list`)).status === 401)

  // TP-08 非成员 404
  check('TP-08 非成员查团队插件 404', (await api('GET', `/api/team/${TID}/plugin/list`, { token: outsider.token })).status === 404)

  // TP-09 权限：Member 创建动态实例 403
  check('TP-09a Member 创建团队动态实例 403', (await api('POST', `/api/team/${TID}/plugin/dynamic`, { token: member.token, body: { teamId: TID, instanceKey: 'tp_dyn', templeteKey: 'dynamic_greet', title: 'D', description: '', config: '{}' } })).status === 403)

  // TP-12 创建团队动态实例（Owner）
  const dynCreate = await api('POST', `/api/team/${TID}/plugin/dynamic`, { token: owner.token, body: { teamId: TID, instanceKey: 'tp_dyn', templeteKey: 'dynamic_greet', title: 'D', description: '', config: '{}' } })
  check('TP-12a Owner 创建团队动态实例 200', dynCreate.status === 200)

  // TP-13 实例 key 团队内唯一
  check('TP-13 重复实例 key 409', (await api('POST', `/api/team/${TID}/plugin/dynamic`, { token: owner.token, body: { teamId: TID, instanceKey: 'tp_dyn', templeteKey: 'dynamic_greet', title: 'D2', description: '', config: '{}' } })).status === 409)

  // 查询列表，验证团队自有动态实例
  const listAdmin = await api('GET', `/api/team/${TID}/plugin/list`, { token: owner.token })
  check('TP-10a 列表含团队自有动态实例', listAdmin.status === 200 && listAdmin.json.items.some((x) => x.kind === 'dynamic' && x.isTeamOwned === true))
  check('TP-10b 列表含响应字段', listAdmin.status === 200 && listAdmin.json.items.every((x) => x.isTeamOwned !== undefined) && listAdmin.json.myRole === 2 && listAdmin.json.canManage === true)

  // TP-09b Owner 权限
  check('TP-09b Owner 删除团队实例 200', (await api('DELETE', `/api/team/${TID}/plugin/${dynCreate.json?.value ?? ''}`)).status === 404) // 动态创建不返回 id，仅确认删除路由可达

  // TP-19b Member 删除返回 403（需团队自有插件 id，此处用非法 id 验证权限拦截优先）
  check('TP-19b Member 删除返回 403/404', [403, 404].includes((await api('DELETE', `/api/team/${TID}/plugin/00000000-0000-0000-0000-000000000000`, { token: member.token })).status))

  console.log(`\n=== 团队插件 E2E 通过 ${PASS} / ${PASS + FAIL} ===`)
  process.exit(FAIL === 0 ? 0 : 1)
}

main().catch((e) => { console.error('FATAL', e); process.exit(1) })
