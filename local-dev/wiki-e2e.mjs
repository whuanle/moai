// 知识库模块 E2E（真实 HTTP，依赖 team-e2e 同款用户工厂；后端 127.0.0.1:5210）
// 场景编号与 docs/wiki/bdd.md 对应（@WK-Sn）
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

  const owner = await mkuser('wo')
  const alice = await mkuser('wa')
  const bob = await mkuser('wb')
  const outsider = await mkuser('wx')

  // 准备团队：owner 建，alice=Admin，bob=Member
  const t = await api('POST', '/api/team', { token: owner.token, body: { name: 'wiki-team-' + TS } })
  const TID = Number(t.json.value)
  await api('POST', `/api/team/${TID}/users`, { token: owner.token, body: { userId: alice.userId, role: 1 } })
  await api('POST', `/api/team/${TID}/users`, { token: owner.token, body: { userId: bob.userId, role: 2 } })

  // WK-01 无 token 401
  check('WK-01 无 token 查列表 401', (await api('GET', `/api/wiki/list?teamId=${TID}`)).status === 401)

  // WK-02 参数校验
  check('WK-02a 空名称 400', (await api('POST', '/api/wiki', { token: owner.token, body: { teamId: TID, name: '' } })).status === 400)
  check('WK-02b 超长名称 400', (await api('POST', '/api/wiki', { token: owner.token, body: { teamId: TID, name: 'x'.repeat(51) } })).status === 400)
  check('WK-02c 非法 teamId 400', (await api('POST', '/api/wiki', { token: owner.token, body: { teamId: 0, name: 'ok1' } })).status === 400)

  // WK-03 权限：Member 403 / 非成员 404
  check('WK-03a Member 创建 403', (await api('POST', '/api/wiki', { token: bob.token, body: { teamId: TID, name: 'nope' } })).status === 403)
  check('WK-03b 非成员创建 404', (await api('POST', '/api/wiki', { token: outsider.token, body: { teamId: TID, name: 'nope' } })).status === 404)

  // WK-04 Admin 创建成功 + 列表
  const WNAME = 'wk-' + TS
  const cr = await api('POST', '/api/wiki', { token: alice.token, body: { teamId: TID, name: WNAME, description: 'e2e wiki' } })
  check('WK-04a Admin 创建 200 且返回 id', cr.status === 200 && Number(cr.json?.value) > 0, `${cr.status} ${cr.text.slice(0, 120)}`)
  const WID = Number(cr.json?.value)
  {
    const r = await api('GET', `/api/wiki/list?teamId=${TID}`, { token: bob.token })
    const item = (r.json?.items ?? []).find(i => Number(i.wikiId) === WID)
    check('WK-04b Member 查列表 200 且含 myRole', r.status === 200 && r.json.myRole === 2 && !!item, JSON.stringify(r.json).slice(0, 150))
  }
  check('WK-04c 非成员查列表 404', (await api('GET', `/api/wiki/list?teamId=${TID}`, { token: outsider.token })).status === 404)

  // WK-05 同团队重名 409（另一团队同名不冲突）
  check('WK-05a 同团队重名 409', (await api('POST', '/api/wiki', { token: owner.token, body: { teamId: TID, name: WNAME } })).status === 409)
  {
    const t2 = await api('POST', '/api/team', { token: outsider.token, body: { name: 'wiki-team2-' + TS } })
    const TID2 = Number(t2.json.value)
    const r2 = await api('POST', '/api/wiki', { token: outsider.token, body: { teamId: TID2, name: WNAME } })
    check('WK-05b 不同团队同名 200', r2.status === 200, `${r2.status}`)
    await api('DELETE', `/api/wiki/${Number(r2.json?.value)}`, { token: outsider.token })
    await api('DELETE', `/api/team/${TID2}`, { token: outsider.token })
  }

  // WK-06 详情
  check('WK-06a 成员查详情 200', (await api('GET', `/api/wiki/${WID}`, { token: bob.token })).json?.name === WNAME)
  check('WK-06b 非成员查详情 404', (await api('GET', `/api/wiki/${WID}`, { token: outsider.token })).status === 404)
  check('WK-06c 不存在 404', (await api('GET', '/api/wiki/999999', { token: bob.token })).status === 404)

  // WK-07 更新
  check('WK-07a Member 更新 403', (await api('PUT', `/api/wiki/${WID}`, { token: bob.token, body: { name: 'new-' + WNAME } })).status === 403)
  check('WK-07b Admin 更新 200', (await api('PUT', `/api/wiki/${WID}`, { token: alice.token, body: { name: 'new-' + WNAME, description: '改过' } })).status === 200)
  check('WK-07c 详情回显', (await api('GET', `/api/wiki/${WID}`, { token: alice.token })).json?.name === 'new-' + WNAME)
  check('WK-07d 更新为已有重名 409', await (async () => {
    await api('POST', '/api/wiki', { token: owner.token, body: { teamId: TID, name: 'second-' + TS } })
    return (await api('PUT', `/api/wiki/${WID}`, { token: alice.token, body: { name: 'second-' + TS } })).status
  })() === 409)

  // WK-08 删除
  check('WK-08a Member 删除 403', (await api('DELETE', `/api/wiki/${WID}`, { token: bob.token })).status === 403)
  check('WK-08b Admin 删除 200', (await api('DELETE', `/api/wiki/${WID}`, { token: alice.token })).status === 200)
  check('WK-08c 删除后详情 404', (await api('GET', `/api/wiki/${WID}`, { token: alice.token })).status === 404)
  check('WK-08d 删除后列表不含', !(await api('GET', `/api/wiki/list?teamId=${TID}`, { token: alice.token })).json.items.some(i => i.name === 'new-' + WNAME))
  check('WK-08e 删除后同名可重建', (await api('POST', '/api/wiki', { token: alice.token, body: { teamId: TID, name: 'new-' + WNAME } })).status === 200)

  console.log(`\n===== 知识库 E2E 汇总: PASS=${PASS} FAIL=${FAIL} =====`)
  process.exit(FAIL > 0 ? 1 : 0)
}

main().catch(e => { console.error('脚本异常:', e); process.exit(2) })
