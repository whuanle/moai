import crypto from 'node:crypto'

const BASE = 'http://127.0.0.1:5000'
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
  try { json = JSON.parse(text) } catch {}
  return { status: res.status, json, text }
}

const TS = Date.now().toString().slice(-8)
let seq = 0
const uname = (p) => `${p}${TS}${String(seq++).padStart(2, '0')}`

async function mkuser(p) {
  const name = uname(p)
  const r = await api('POST', '/api/auth/register', { body: { userName: name, email: `${name}@test.local`, nickName: name, phone: '15' + Date.now().toString().slice(-8) + String(seq++).padStart(2, '0'), password: rsa('Test1234') } })
  if (r.status !== 200) throw new Error(`注册 ${name} 失败: ${r.status} ${r.text.slice(0, 160)}`)
  const l = await api('POST', '/api/auth/login', { body: { userName: name, password: rsa('Test1234') } })
  return { name, userId: Number(l.json.userId), token: l.json.accessToken }
}

let PASS = 0, FAIL = 0
const check = (name, cond, detail = '') => {
  if (cond) { PASS++; console.log(`PASS | ${name}`) }
  else { FAIL++; console.log(`FAIL | ${name} ${detail ? '— ' + detail : ''}`) }
}

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const owner = await mkuser('ow')
  const alice = await mkuser('al')
  const bob = await mkuser('bo')

  const cr = await api('POST', '/api/team', { token: owner.token, body: { name: 'cand-team-' + TS } })
  check('创建团队', cr.status === 200)
  const TID = Number(cr.json.value)

  // alice 入团（新枚举：Member=0）
  {
    const add = await api('POST', `/api/team/${TID}/users`, { token: owner.token, body: { userId: alice.userId, role: 0 } })
    check('加入 alice 为成员', add.status === 200)
  }

  // CAND-01 管理员可搜候选（排除已入团 alice，命中 bob）
  {
    const r = await api('GET', `/api/team/${TID}/candidates?keyword=${encodeURIComponent('bo')}`, { token: owner.token })
    const items = (r.json?.items ?? [])
    check('CAND-01 Owner 搜索候选 200 且含 bob', r.status === 200 && items.some(i => Number(i.userId) === bob.userId), JSON.stringify(items))
    check('CAND-02 排除已入团成员（不含 alice）', !items.some(i => Number(i.userId) === alice.userId))
  }

  // CAND-03 空关键字返回候选（按 id 升序取前 20，仅需 200 且有 items）
  {
    const r = await api('GET', `/api/team/${TID}/candidates`, { token: owner.token })
    check('CAND-03 空关键字返回 200 且有候选', r.status === 200 && (r.json?.items ?? []).length > 0)
  }

  // CAND-04 成员(非管理员)不可访问 → 403
  {
    const r = await api('GET', `/api/team/${TID}/candidates?keyword=bo`, { token: alice.token })
    check('CAND-04 Member 访问候选 403', r.status === 403, `${r.status}`)
  }

  // CAND-05 非成员不可访问 → 404
  {
    const r = await api('GET', `/api/team/${TID}/candidates?keyword=bo`, { token: bob.token })
    check('CAND-05 非成员访问候选 404', r.status === 404, `${r.status}`)
  }

  console.log(`\n===== 候选搜索 E2E: PASS=${PASS} FAIL=${FAIL} =====`)
  process.exit(FAIL > 0 ? 1 : 0)
}

main().catch(e => { console.error('脚本异常:', e); process.exit(2) })
