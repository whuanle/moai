// 应用使用次数计数 E2E（场景 @AU-Sn）
// 验证：会话创建推 Redis 计数、每分钟批量累加到 app.use_count、删除会话不回退、累计口径与存活会话口径区分。
// 用法：node local-dev/app-usecount-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5000，可用 APP_BASE 覆盖）
// 前置：后端运行中。计数器为每分钟批量落库，用例含轮询等待（最长约 2 分钟）。
import crypto from 'node:crypto'
import http from 'node:http'

const BASE = process.env.APP_BASE ?? process.argv[2] ?? 'http://127.0.0.1:5000'
let PASS = 0, FAIL = 0
const check = (name, cond, detail = '') => {
  if (cond) { PASS++; console.log(`PASS | ${name}`) }
  else { FAIL++; console.log(`FAIL | ${name} ${detail ? '— ' + detail : ''}`) }
}
const sleep = (ms) => new Promise((r) => setTimeout(r, ms))

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
async function mkuser(p) {
  const name = `${p}${TS}`
  const r = await api('POST', '/api/auth/register', { body: { userName: name, email: `${name}@test.local`, nickName: name, phone: `15${TS}0`.slice(0, 11), password: rsa('Test1234') } })
  if (r.status !== 200) throw new Error(`注册 ${name} 失败: ${r.status} ${r.text.slice(0, 120)}`)
  const l = await api('POST', '/api/auth/login', { body: { userName: name, password: rsa('Test1234') } })
  return { name, userId: Number(l.json.userId), token: l.json.accessToken }
}
const isGuid = (v) => typeof v === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(v)

// 轮询列表里目标应用的 useCount，直到满足条件或超时（timeoutMs=0 表示只查一次）
async function pollUseCount(token, teamId, appId, cond, timeoutMs = 90_000) {
  const deadline = Date.now() + timeoutMs
  let last = -1
  for (;;) {
    const r = await api('GET', `/api/app/list?teamId=${teamId}&pageNo=1&pageSize=50`, { token })
    const item = (r.json?.items ?? []).find((x) => String(x.appId) === appId)
    last = item ? Number(item.useCount ?? 0) : -1
    if (cond(last) || Date.now() >= deadline) return last
    await sleep(5000)
  }
}

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const owner = await mkuser('au')
  const TID = Number((await api('POST', '/api/team', { token: owner.token, body: { name: 'usecount-team-' + TS } })).json.value)
  const c = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '计数应用', appType: 'agent' } })
  const APP_ID = String(c.json?.value ?? '')
  if (!isGuid(APP_ID)) throw new Error(`创建应用失败: ${c.status} ${c.text.slice(0, 140)}`)

  // AU-01 累计使用次数：3 个正式会话创建后推计数，等待计数器批量落库
  const sessionIds = []
  for (let i = 0; i < 3; i++) {
    const s = await api('POST', `/api/app/${APP_ID}/session`, { token: owner.token, body: { title: `usecount-${i}` } })
    check(`AU-01${'abc'[i]} 创建正式会话 ${i + 1} 200`, s.status === 200 && isGuid(s.json?.value), `${s.status} ${s.text.slice(0, 120)}`)
    sessionIds.push(String(s.json?.value ?? ''))
  }

  const afterCreate = await pollUseCount(owner.token, TID, APP_ID, (v) => v >= 3)
  check('AU-02 会话创建后累计使用次数落库为 3', afterCreate === 3, `useCount=${afterCreate}（计数器每分钟批量落库，超时 90s）`)

  // AU-03 删除 1 个会话：列直读不回退
  const del = await api('DELETE', `/api/app/session/${sessionIds[0]}`, { token: owner.token })
  check('AU-03a 删除会话 200', del.status === 200, `${del.status} ${del.text.slice(0, 120)}`)
  const rightAfter = await pollUseCount(owner.token, TID, APP_ID, () => true, 0)
  check('AU-03b 删除后累计使用次数立即不回退（仍为 3）', rightAfter === 3, `useCount=${rightAfter}`)

  // AU-04 跨计数器落库边界：再等约 75s（至少一个 flush 周期），确认无反向扣减
  await sleep(75_000)
  const afterFlush = await pollUseCount(owner.token, TID, APP_ID, () => true, 0)
  check('AU-04 跨计数器落库周期后仍为 3（删除不触发任何扣减）', afterFlush === 3, `useCount=${afterFlush}`)

  // AU-05 双口径区分：使用分析里累计使用次数(3) ≠ 存活会话数(2)
  const usage = await api('GET', `/api/app/${APP_ID}/usage`, { token: owner.token })
  const stats = usage.json?.stats
  check('AU-05a 使用分析 200 且含 stats', usage.status === 200 && stats != null, `${usage.status} ${usage.text.slice(0, 140)}`)
  check('AU-05b 累计使用次数为 3（删除会话不减少）', Number(stats?.useCount) === 3, `useCount=${stats?.useCount}`)
  check('AU-05c 存活会话数为 2（软删除过滤生效）', Number(stats?.sessionCount) === 2, `sessionCount=${stats?.sessionCount}`)
  check('AU-05d 使用用户数为 1（按会话归属去重）', Number(stats?.userCount) === 1, `userCount=${stats?.userCount}`)

  // AU-06 调试会话不计入：创建调试会话后累计使用次数不变
  const dbg = await api('POST', `/api/app/${APP_ID}/debug/session`, { token: owner.token })
  check('AU-06a 创建调试会话 200', dbg.status === 200 && isGuid(dbg.json?.value), `${dbg.status} ${dbg.text.slice(0, 120)}`)
  check('AU-06b 调试会话不进正式会话列表', ((await api('GET', `/api/app/${APP_ID}/session/list`, { token: owner.token })).json?.items ?? []).every((s) => String(s.sessionId) !== String(dbg.json?.value)))

  console.log(`\n结果：PASS=${PASS} FAIL=${FAIL}`)
  process.exit(FAIL > 0 ? 1 : 0)
}

main().catch((e) => { console.error('E2E 异常:', e); process.exit(1) })
