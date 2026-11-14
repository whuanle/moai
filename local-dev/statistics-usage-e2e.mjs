// 统一资源使用统计模块 E2E（场景 @ST-Sn）
// 验证：应用会话计数 → Redis → 统计模块每分钟批量落库 → resource_usage_daily / resource_usage_total
//       → 管理员查询接口（daily/total/names）；普通用户 403、未登录 401、参数校验 400。
// 用法：node local-dev/statistics-usage-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5000）
// 前置：后端运行中。落库为每分钟批量，用例含轮询等待（最长约 2 分钟）。
import crypto from 'node:crypto'

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
  await api('POST', '/api/auth/register', { body: { userName: name, email: `${name}@test.local`, nickName: name, phone: `15${TS}0`.slice(0, 11), password: rsa('Test1234') } })
  const l = await api('POST', '/api/auth/login', { body: { userName: name, password: rsa('Test1234') } })
  return { name, token: l.json.accessToken }
}
const isGuid = (v) => typeof v === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(v)
// 数据库统一统计表 resource_id 以 "N" 格式（无连字符小写）存储
const nFormat = (guid) => guid.replaceAll('-', '').toLowerCase()
const todayUtc = () => new Date().toISOString().slice(0, 10)
const tomorrowUtc = () => new Date(Date.now() + 86_400_000).toISOString().slice(0, 10)

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const admin = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
  const AT = admin.json.accessToken
  check('ST-00 admin 登录 200', admin.status === 200 && !!AT, `${admin.status}`)

  // ST-01 门禁：普通用户 403，未登录 401
  const user = await mkuser('st')
  check('ST-01a 普通用户查询 names 403', (await api('GET', '/api/statistics/usage/names', { token: user.token })).status === 403)
  check('ST-01b 普通用户查询 daily 403', (await api('GET', '/api/statistics/usage/daily?pageNo=1&pageSize=10', { token: user.token })).status === 403)
  check('ST-01c 普通用户查询 total 403', (await api('GET', '/api/statistics/usage/total?pageNo=1&pageSize=10', { token: user.token })).status === 403)
  check('ST-01d 未登录查询 names 401', (await api('GET', '/api/statistics/usage/names')).status === 401)

  // ST-02 触发 app-usage：创建应用 + 3 个正式会话（会话创建推 Redis 计数）
  const TID = Number((await api('POST', '/api/team', { token: user.token, body: { name: 'st-team-' + TS } })).json.value)
  const c = await api('POST', '/api/app', { token: user.token, body: { teamId: TID, name: '统计应用' + TS, appType: 'agent' } })
  const APP_ID = String(c.json?.value ?? '')
  check('ST-02a 创建应用 200', isGuid(APP_ID), `${c.status} ${c.text.slice(0, 120)}`)
  let sessions = 0
  for (let i = 0; i < 3; i++) {
    const s = await api('POST', `/api/app/${APP_ID}/session`, { token: user.token, body: { title: `st-${i}` } })
    if (s.status === 200) sessions++
  }
  check('ST-02b 创建 3 个会话 200', sessions === 3, `sessions=${sessions}`)

  // ST-03 等待批量落库：日表出现 app-usage / 该应用记录且 count>=3、日期为今天（UTC）
  // 注意轮询以 count>=3 为完成条件：flush 周期可能把 3 次会话计数拆到两个批次，行先以较小 count 出现
  const nid = nFormat(APP_ID)
  let dailyItem = null
  const deadline = Date.now() + 150_000
  for (;;) {
    const r = await api('GET', `/api/statistics/usage/daily?name=app-usage&pageNo=1&pageSize=100`, { token: AT })
    dailyItem = (r.json?.items ?? []).find((x) => x.resourceId === nid) ?? null
    if ((dailyItem && Number(dailyItem.count) >= 3) || Date.now() >= deadline) break
    await sleep(5000)
  }
  check('ST-03a 日表出现该应用记录', dailyItem != null, '120s 内未等到批量落库')
  check('ST-03b 日表当日 count>=3', Number(dailyItem?.count) >= 3, `count=${dailyItem?.count}`)
  check('ST-03c 日表统计日期为今天(UTC)', dailyItem?.usageDate?.slice(0, 10) === todayUtc(), `usageDate=${dailyItem?.usageDate}`)

  // ST-04 总量表同步累加（同样按 >=3 轮询等待批次聚合完成）
  let totalItem = null
  const dl5 = Date.now() + 150_000
  for (;;) {
    const t = await api('GET', `/api/statistics/usage/total?name=app-usage&pageNo=1&pageSize=100`, { token: AT })
    totalItem = (t.json?.items ?? []).find((x) => x.resourceId === nid) ?? null
    if ((totalItem && Number(totalItem.total) >= 3) || Date.now() >= dl5) break
    await sleep(5000)
  }
  check('ST-04 总量表 count>=3', Number(totalItem?.total) >= 3, `total=${totalItem?.total}`)

  // ST-05 names 清单包含 app-usage
  const names = await api('GET', '/api/statistics/usage/names', { token: AT })
  check('ST-05 names 含 app-usage', (names.json?.items ?? []).includes('app-usage'), `items=${JSON.stringify(names.json?.items)}`)

  // ST-06 过滤：今天区间命中 1 条，明天区间为 0；分页字段回显
  const hit = await api('GET', `/api/statistics/usage/daily?name=app-usage&startDate=${todayUtc()}&endDate=${todayUtc()}&pageNo=1&pageSize=100`, { token: AT })
  const miss = await api('GET', `/api/statistics/usage/daily?name=app-usage&startDate=${tomorrowUtc()}&endDate=${tomorrowUtc()}&pageNo=1&pageSize=10`, { token: AT })
  check('ST-06a 今天区间命中', (hit.json?.items ?? []).some((x) => x.resourceId === nid) && Number(hit.json?.total) >= 1, `total=${hit.json?.total}`)
  check('ST-06b 明天区间为空', (miss.json?.items ?? []).length === 0 && Number(miss.json?.total) === 0, `total=${miss.json?.total}`)
  check('ST-06c 分页字段回显', Number(hit.json?.pageNo) === 1 && Number(hit.json?.pageSize) === 100, `pageNo=${hit.json?.pageNo} pageSize=${hit.json?.pageSize}`)

  // ST-07 参数校验：startDate > endDate 400
  const bad = await api('GET', `/api/statistics/usage/daily?startDate=${tomorrowUtc()}&endDate=${todayUtc()}&pageNo=1&pageSize=10`, { token: AT })
  check('ST-07 startDate>endDate 400', bad.status === 400, `${bad.status} ${bad.text.slice(0, 120)}`)

  // ST-08 写入器联动：资源自身表仍被累加（应用列表 useCount>=3）
  let useCount = -1
  const dl2 = Date.now() + 90_000
  for (;;) {
    const r = await api('GET', `/api/app/list?teamId=${TID}&pageNo=1&pageSize=50`, { token: user.token })
    const item = (r.json?.items ?? []).find((x) => String(x.appId) === APP_ID)
    useCount = item ? Number(item.useCount ?? 0) : -1
    if (useCount >= 3 || Date.now() >= dl2) break
    await sleep(5000)
  }
  check('ST-08 应用列表 useCount>=3（写入器仍写资源表）', useCount >= 3, `useCount=${useCount}`)

  // ST-09 提示词专家统计：创建团队提示词 → 会话选用 → 日表 prompt-usage 出现该专家且 count>=1
  const cp = await api('POST', '/api/prompt', { token: user.token, body: { teamId: TID, name: '统计专家' + TS, description: 'st', content: '# 专家提示词', promptClassId: 0 } })
  const PROMPT_ID = String(cp.json?.value ?? '')
  check('ST-09a 创建团队提示词 200', /^\d+$/.test(PROMPT_ID), `${cp.status} ${cp.text.slice(0, 140)}`)
  const ps = await api('POST', `/api/app/${APP_ID}/session`, { token: user.token, body: { title: 'st-prompt', promptId: Number(PROMPT_ID) } })
  check('ST-09b 会话选用专家 200', ps.status === 200 && isGuid(ps.json?.value), `${ps.status} ${ps.text.slice(0, 140)}`)
  let promptCount = 0
  const dl3 = Date.now() + 120_000
  for (;;) {
    const r = await api('GET', `/api/statistics/usage/daily?name=prompt-usage&pageNo=1&pageSize=100`, { token: AT })
    promptCount = Number((r.json?.items ?? []).find((x) => x.resourceId === PROMPT_ID)?.count ?? 0)
    if (promptCount >= 1 || Date.now() >= dl3) break
    await sleep(5000)
  }
  check('ST-09c 日表 prompt-usage count>=1（写入器落 prompts.use_count）', promptCount >= 1, `count=${promptCount}`)

  // ST-10 统计粒度设置：设置清单回显 + 非法值 400 + 切 hour 后日表带小时桶 + 还原 day
  const gs = await api('GET', '/api/settings', { token: AT })
  const gran = (gs.json?.items ?? []).find((x) => x.key === 'STATISTICS_GRANULARITY')
  check('ST-10a 设置清单含统计粒度（默认 day）', gran != null && gran.value === 'day', `value=${gran?.value}`)
  check('ST-10b 非法粒度 400', (await api('PUT', '/api/settings', { token: AT, body: { key: 'STATISTICS_GRANULARITY', value: 'week' } })).status === 400)
  check('ST-10c 切换 hour 200', (await api('PUT', '/api/settings', { token: AT, body: { key: 'STATISTICS_GRANULARITY', value: 'hour' } })).status === 200)
  const hs = await api('POST', `/api/app/${APP_ID}/session`, { token: user.token, body: { title: 'st-hour' } })
  check('ST-10d hour 模式创建会话 200', hs.status === 200, `${hs.status}`)
  const utcHour = new Date().getUTCHours()
  let hourRow = null
  const dl4 = Date.now() + 120_000
  for (;;) {
    const r = await api('GET', `/api/statistics/usage/daily?name=app-usage&startDate=${todayUtc()}&endDate=${todayUtc()}&pageNo=1&pageSize=100`, { token: AT })
    hourRow = (r.json?.items ?? []).find((x) => x.resourceId === nid && Number(x.usageHour) === utcHour) ?? null
    if (hourRow || Date.now() >= dl4) break
    await sleep(5000)
  }
  check('ST-10e hour 模式日表出现当前 UTC 小时桶', hourRow != null, `usageHour=${hourRow?.usageHour}（期望 ${utcHour}）`)
  check('ST-10f 还原 day 200', (await api('PUT', '/api/settings', { token: AT, body: { key: 'STATISTICS_GRANULARITY', value: 'day' } })).status === 200)

  // ST-11 插件调用链路 + 趋势分析接口：管理员运行静态插件 → plugin-usage 落库 → trend 按日聚合出现数据
  const pl = await api('GET', '/api/ai/plugin/manage/list?kind=static', { token: AT })
  const pick = (pl.json?.items ?? []).find((i) => i.pluginName) ?? (pl.json?.items ?? [])[0]
  check('ST-11a 存在可运行插件', pick != null && !!pick.pluginName, `items=${(pl.json?.items ?? []).length}`)
  if (pick) {
    const run = await api('POST', '/api/ai/plugin/run', { token: AT, body: { key: pick.pluginName, requestJson: '{}' } })
    check('ST-11b 运行插件 200', run.status === 200, `${run.status} ${run.text.slice(0, 100)}`)
    const ridN = nFormat(String(pick.id))
    let trendHit = 0
    const dl6 = Date.now() + 120_000
    for (;;) {
      const t = await api('GET', `/api/statistics/usage/total?name=plugin-usage&pageNo=1&pageSize=100`, { token: AT })
      trendHit = Number((t.json?.items ?? []).find((x) => x.resourceId === ridN)?.total ?? 0)
      if (trendHit >= 1 || Date.now() >= dl6) break
      await sleep(5000)
    }
    check('ST-11c 插件计数落统一总表', trendHit >= 1, `total=${trendHit}`)
    const tr = await api('GET', `/api/statistics/usage/trend?name=plugin-usage`, { token: AT })
    const todayCount = Number((tr.json?.items ?? []).find((x) => x.date?.slice(0, 10) === todayUtc())?.count ?? 0)
    check('ST-11d 趋势接口按日聚合含今日数据', tr.status === 200 && (tr.json?.items ?? []).length >= 30 && todayCount >= 1, `days=${(tr.json?.items ?? []).length} today=${todayCount}`)
    const trBad = await api('GET', `/api/statistics/usage/trend?startDate=${tomorrowUtc()}&endDate=${todayUtc()}`, { token: AT })
    check('ST-11e 趋势参数校验 400', trBad.status === 400, `${trBad.status}`)
    const trAll = await api('GET', `/api/statistics/usage/trend`, { token: AT })
    check('ST-11f 不带 name 统计全部类型', trAll.status === 200 && Number(trAll.json?.total) >= 1, `total=${trAll.json?.total}`)
  }

  console.log(`\n结果：PASS=${PASS} FAIL=${FAIL}`)
  process.exit(FAIL > 0 ? 1 : 0)
}

main().catch((e) => { console.error('E2E 异常:', e); process.exit(1) })
