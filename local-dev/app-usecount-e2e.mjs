// 应用使用次数计数 E2E（场景 @AU-Sn）
// 验证：会话创建推 Redis 计数、每分钟批量累加到 app.use_count、删除会话不回退、累计口径与存活会话口径区分、
//       使用分析趋势端点（近 N 天使用次数/消息条数按日聚合，AU-07）。
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

  // AU-07 使用趋势端点（近 N 天使用次数/消息条数按日聚合，来自统计日表）
  const trend = await api('GET', `/api/app/${APP_ID}/usage/trend`, { token: owner.token })
  const trendItems = trend.json?.items ?? []
  const today = new Date().toISOString().slice(0, 10)
  check('AU-07a 趋势 200 且默认 30 天升序含今日', trend.status === 200 && Array.isArray(trendItems) && trendItems.length === 30 && trendItems.some((x) => String(x.date).startsWith(today)), `${trend.status} items=${trendItems.length}`)
  check('AU-07b 趋势项含日期/使用次数/消息条数字段', trendItems.length > 0 && trendItems.every((x) => x.date != null && x.useCount !== undefined && x.messageCount !== undefined), JSON.stringify(trendItems[0] ?? {}).slice(0, 120))
  const todayItem = trendItems.find((x) => String(x.date).startsWith(today))
  check('AU-07c 今日使用次数为 3（日表已落库）', Number(todayItem?.useCount) === 3, `useCount=${todayItem?.useCount}`)
  check('AU-07d 今日消息条数为 0（本用例未对话）', Number(todayItem?.messageCount) === 0, `messageCount=${todayItem?.messageCount}`)
  const trend7 = await api('GET', `/api/app/${APP_ID}/usage/trend?days=7`, { token: owner.token })
  check('AU-07e days=7 返回 7 天', trend7.status === 200 && (trend7.json?.items ?? []).length === 7, `${trend7.status} items=${(trend7.json?.items ?? []).length}`)
  check('AU-07f days=0 参数非法 400', (await api('GET', `/api/app/${APP_ID}/usage/trend?days=0`, { token: owner.token })).status === 400)
  check('AU-07g days=91 超上限 400', (await api('GET', `/api/app/${APP_ID}/usage/trend?days=91`, { token: owner.token })).status === 400)
  check('AU-07h 未登录 401', (await api('GET', `/api/app/${APP_ID}/usage/trend`)).status === 401)
  check('AU-07i 不存在应用 404', (await api('GET', `/api/app/01924f5e-0000-7000-8000-00000000ffff/usage/trend`, { token: owner.token })).status === 404)
  check('AU-07j 趋势含 modelTokenSeries 字段（未对话应用为空数组）', Array.isArray(trend.json?.modelTokenSeries) && trend.json.modelTokenSeries.length === 0, JSON.stringify(trend.json?.modelTokenSeries ?? null))

  console.log(`\n结果：PASS=${PASS} FAIL=${FAIL}`)
  process.exit(FAIL > 0 ? 1 : 0)
}

main().catch((e) => { console.error('E2E 异常:', e); process.exit(1) })
