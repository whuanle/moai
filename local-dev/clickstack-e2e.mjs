// ClickStack 查询插件（clickstack_query）桩服务端到端（场景 @DYN-S64 / @DYN-S65 / @DYN-S66）
// 做法：本地起一个 ClickStack 对外 API 桩（/api/v2/sources｜search｜charts/series，Bearer 鉴权 + 401/404/400 错误形态）→
//       拉起一个独立后端 → 创建动态插件实例并运行 → 断言「实例 → 注册表模板 → 客户端 → 桩服务 → 响应解析」整条链路，
//       覆盖三模式成功路径、请求体透传（ISO/epoch ms 时间窗、select/orderBy/offset/series）、缺省窗口、参数校验与错误归一，
//       无需真实 ClickStack。
// 前置：宿主已按独立输出目录构建（默认 bin/Debug 可能被运行中的后端锁定）：
//       dotnet build src/MoAI/MoAI.csproj -o .builds/clickstack
// 用法：node local-dev/clickstack-e2e.mjs
// 环境变量：CKS_BACKEND_PORT（默认 5197）｜CKS_MOCK_PORT（默认 5196）｜CKS_KEEP_BACKEND=1 保留后端供排查
import { createServer } from 'node:http'
import { spawn } from 'node:child_process'
import fs from 'node:fs'
import crypto from 'node:crypto'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const REPO_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const BACKEND_PORT = Number(process.env.CKS_BACKEND_PORT ?? 5197)
const MOCK_PORT = Number(process.env.CKS_MOCK_PORT ?? 5196)
const BASE = `http://127.0.0.1:${BACKEND_PORT}`
const MOCK = `http://127.0.0.1:${MOCK_PORT}`
const MOCK_BASE_URL = `http://127.0.0.1:${MOCK_PORT}`
const CKS_KEY = 'cks-e2e-personal-api-key'

let PASS = 0, FAIL = 0
const check = (name, cond, detail = '') => {
  if (cond) { PASS++; console.log(`PASS | ${name}`) }
  else { FAIL++; console.log(`FAIL | ${name} ${detail ? '— ' + detail : ''}`) }
}

// ---------------- ClickStack 对外 API 桩数据 ----------------
const SOURCES = [
  { id: 'src-log-e2e', name: '应用日志', kind: 'log', from: { databaseName: 'hyperdx', tableName: 'otel_logs' }, defaultTableSelectExpression: 'Timestamp,ServiceName,Body' },
  { id: 'src-metric-e2e', name: '主机指标', kind: 'metric', from: { databaseName: 'hyperdx', tableName: null }, metricTables: { sum: 'otel_metrics_sum', gauge: 'otel_metrics_gauge', exp_histogram: 'otel_metrics_exp_histogram' } },
  { id: 'src-session-e2e', name: '会话回放', kind: 'session', from: { databaseName: 'hyperdx', tableName: 'hyperdx_sessions' }, disabled: true },
]
const SEARCH_ROWS = [
  { Timestamp: '2026-09-29T01:00:00Z', ServiceName: 'moai', SeverityText: 'ERROR', Body: 'boom happened', Duration: 12 },
  { Timestamp: '2026-09-29T01:01:00Z', ServiceName: 'moai', SeverityText: 'INFO', Body: 'ok now', Duration: 3.5, Ok: true },
]
const CHART_POINTS = [
  { ts_bucket: 1735689600000, 'series_0.data': 12.5, group: ['moai', 'ERROR'] },
  { ts_bucket: 1735689900000, 'series_0.data': 3, group: ['moai', 'INFO'] },
]

const hits = { sources: 0, search: 0, chart: 0, unauthorized: 0 }
let lastAuth = null
let lastSearch = null
let lastChart = null

const sleep = (ms) => new Promise((r) => setTimeout(r, ms))

// ---------------- HTTP 桩服务：ClickStack /api/v2 三端点 ----------------
function startMock() {
  const server = createServer((req, res) => {
    let body = ''
    req.on('data', (c) => { body += c })
    req.on('end', () => {
      const pathName = new URL(req.url ?? '/', MOCK).pathname
      const send = (code, obj) => {
        res.writeHead(code, { 'Content-Type': 'application/json' })
        res.end(JSON.stringify(obj))
      }
      lastAuth = req.headers['authorization'] ?? null

      if (pathName === '/api/v2/sources' && req.method === 'GET') {
        if (lastAuth !== `Bearer ${CKS_KEY}`) { hits.unauthorized++; return send(401, { message: 'invalid api key' }) }
        hits.sources++
        return send(200, { data: SOURCES })
      }

      if (pathName === '/api/v2/search' && req.method === 'POST') {
        if (lastAuth !== `Bearer ${CKS_KEY}`) { hits.unauthorized++; return send(401, { message: 'invalid api key' }) }
        let rpc = null
        try { rpc = JSON.parse(body) } catch { /* 非 JSON */ }
        lastSearch = rpc ?? {}
        hits.search++
        if (lastSearch.sourceId === 'src-404') return send(404, { message: `source not found: ${lastSearch.sourceId}` })
        if (String(lastSearch.where ?? '').includes('BOOM_WHERE')) return send(400, { message: 'invalid where expression' })
        return send(200, { data: SEARCH_ROWS, rows: SEARCH_ROWS.length })
      }

      if (pathName === '/api/v2/charts/series' && req.method === 'POST') {
        if (lastAuth !== `Bearer ${CKS_KEY}`) { hits.unauthorized++; return send(401, { message: 'invalid api key' }) }
        let rpc = null
        try { rpc = JSON.parse(body) } catch { /* 非 JSON */ }
        lastChart = rpc ?? {}
        hits.chart++
        if (lastChart.granularity === '99x') return send(400, { error: 'invalid granularity' })
        return send(200, { data: CHART_POINTS })
      }

      return send(404, { message: `no mock for ${pathName}` })
    })
  })
  return new Promise((resolve) => server.listen(MOCK_PORT, '127.0.0.1', () => resolve(server)))
}

// ---------------- 后端：优先用独立输出目录 DLL（绕开运行中后端的 DLL 锁），否则回退 dotnet run ----------------
function startBackend() {
  const dll = path.join(REPO_ROOT, '.builds', 'clickstack', 'MoAI.dll')
  if (!fs.existsSync(dll)) {
    console.log('WARN | 未找到 .builds/clickstack/MoAI.dll，回退 dotnet run（默认 bin/Debug；若后端正被运行会因 DLL 锁失败）')
    return startBackendDotnetRun()
  }
  const child = spawn('dotnet', [dll], {
    cwd: path.join(REPO_ROOT, 'src', 'MoAI'),
    env: {
      ...process.env,
      ASPNETCORE_ENVIRONMENT: 'Development',
      MoAI__Port: String(BACKEND_PORT),
    },
    stdio: ['ignore', 'pipe', 'pipe'],
  })
  let log = ''
  child.stdout.on('data', (d) => { log += d })
  child.stderr.on('data', (d) => { log += d })
  child.getLog = () => log
  return child
}

function startBackendDotnetRun() {
  const child = spawn('dotnet', ['run', '--project', 'src/MoAI/MoAI.csproj', '--no-build', '--no-restore'], {
    cwd: REPO_ROOT,
    env: {
      ...process.env,
      ASPNETCORE_ENVIRONMENT: 'Development',
      MoAI__Port: String(BACKEND_PORT),
    },
    stdio: ['ignore', 'pipe', 'pipe'],
  })
  let log = ''
  child.stdout.on('data', (d) => { log += d })
  child.stderr.on('data', (d) => { log += d })
  child.getLog = () => log
  return child
}

function killBackend(child) {
  return new Promise((resolve) => {
    if (!child || child.exitCode !== null) return resolve()
    spawn('taskkill', ['/pid', String(child.pid), '/T', '/F'], { stdio: 'ignore' })
      .on('close', () => resolve())
  })
}

async function waitReady(child) {
  for (let i = 0; i < 90; i++) {
    if (child.exitCode !== null) throw new Error(`后端进程提前退出（code=${child.exitCode}）：\n${child.getLog().slice(-2000)}`)
    try {
      const r = await fetch(`${BASE}/api/common/serverinfo`, { signal: AbortSignal.timeout(3000) })
      if (r.ok) return true
    } catch { /* 未就绪 */ }
    await sleep(2000)
  }
  throw new Error(`后端 ${BASE} 未在 180s 内就绪：\n${child.getLog().slice(-2000)}`)
}

async function api(method, p, { token, body } = {}) {
  const headers = {}
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  if (token) headers['Authorization'] = `Bearer ${token}`
  const res = await fetch(BASE + p, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) })
  const text = await res.text()
  let json = null
  try { json = JSON.parse(text) } catch { /* 非 JSON */ }
  return { status: res.status, json, text }
}

const save = (token, body) => api('POST', '/api/ai/plugin/dynamic/save', { token, body })
const del = (token, pluginKey) => api('DELETE', '/api/ai/plugin/dynamic', { token, body: { pluginKey } })
const run = async (token, key, requestJson) => {
  const r = await api('POST', '/api/ai/plugin/run', { token, body: { key, requestJson } })
  let data = null
  try { data = JSON.parse(r.json?.dataJson ?? 'null') } catch { /* 非 JSON */ }
  return { ...r, data }
}
const saveInstance = async (token, key, templeteKey, title, config) =>
  (await save(token, { pluginKey: key, templeteKey, title, description: 'e2e', classifyId: 0, config: JSON.stringify(config) })).status === 200

async function main() {
  const mock = await startMock()
  console.log(`ClickStack API 桩已就绪：${MOCK}`)
  const backend = startBackend()
  console.log(`后端启动中：${BASE}`)

  try {
    await waitReady(backend)
    console.log('后端已就绪')

    const si = await api('GET', '/api/common/serverinfo')
    const rsaPub = si.json.rsaPublic
    const rsa = (plain) => {
      const key = crypto.createPublicKey({ key: Buffer.from(rsaPub, 'base64'), format: 'der', type: 'spki' })
      return crypto.publicEncrypt({ key, padding: crypto.constants.RSA_PKCS1_PADDING }, Buffer.from(plain, 'utf8')).toString('base64')
    }
    const login = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
    if (login.status !== 200 || !login.json?.accessToken) throw new Error(`admin 登录失败: ${login.status} ${login.text.slice(0, 200)}`)
    const admin = login.json.accessToken

    const TS = Date.now().toString().slice(-8)
    const CKS_OK = `cks_ok_${TS}`
    const CKS_BAD = `cks_bad_${TS}`

    // ---------- 注册表 ----------
    const list = await api('GET', '/api/ai/plugin', { token: admin })
    const items = list.json?.items ?? list.json ?? []
    const tpl = items.find((x) => x.key === 'clickstack_query')
    check('注册表含 clickstack_query 且为动态模板', Boolean(tpl) && tpl.isDynamic === true, list.text.slice(0, 200))
    check('clickstack_query 配置类型已解析', (tpl?.configType ?? '').includes('ClickStackQueryConfig'), tpl?.configType ?? '')

    // ---------- S58 sources：数据源解析 + Bearer 鉴权 + 配置校验 ----------
    check('@DYN-S64a 创建 ClickStack 实例', await saveInstance(admin, CKS_OK, 'clickstack_query', 'ClickStack-ok', { BaseUrl: MOCK_BASE_URL, ApiKey: CKS_KEY, TimeoutSeconds: 10, MaxRows: 2 }))
    const sources = await run(admin, CKS_OK, JSON.stringify({ Mode: 'sources' }))
    const logSource = sources.data?.Sources?.[0]
    check('@DYN-S64b sources 解析（log 源字段完整 + metric 源表名回退 + session 源停用态）',
      sources.json?.success === true && sources.data?.ResultType === 'sources' && (sources.data?.Sources ?? []).length === 3
      && logSource?.Id === 'src-log-e2e' && logSource?.Kind === 'log' && logSource?.Database === 'hyperdx' && logSource?.Table === 'otel_logs' && logSource?.DefaultSelect === 'Timestamp,ServiceName,Body'
      && sources.data.Sources[1].Table === 'otel_metrics_sum' && sources.data.Sources[2].Disabled === true,
      JSON.stringify(sources.data ?? sources.json?.error ?? {}))
    check('@DYN-S64c Bearer 头命中 Personal API Access Key', lastAuth === `Bearer ${CKS_KEY}` && hits.sources >= 1, JSON.stringify({ lastAuth, hits }))

    check('@DYN-S64d 创建 ClickStack 实例（错误 Key）', await saveInstance(admin, CKS_BAD, 'clickstack_query', 'ClickStack-bad', { BaseUrl: MOCK_BASE_URL, ApiKey: 'wrong-key', TimeoutSeconds: 10, MaxRows: 2 }))
    const badKey = await run(admin, CKS_BAD, JSON.stringify({ Mode: 'sources' }))
    check('@DYN-S64e 401 归一为可读鉴权错误（提示 Personal API Access Key）',
      badKey.json?.success === false && /鉴权失败/.test(badKey.json?.error ?? '') && /401/.test(badKey.json?.error ?? '') && /Personal API Access Key/.test(badKey.json?.error ?? ''),
      `${badKey.status} ${badKey.json?.error?.slice(0, 220)}`)
    const badSchemeKey = `cks_scheme_${TS}`
    check('@DYN-S64f 创建 ClickStack 实例（非法协议 BaseUrl）', await saveInstance(admin, badSchemeKey, 'clickstack_query', 'ClickStack-scheme', { BaseUrl: 'ftp://example.com', ApiKey: CKS_KEY }))
    const badSchemeRun = await run(admin, badSchemeKey, JSON.stringify({ Mode: 'sources' }))
    check('@DYN-S64g BaseUrl 非法协议在运行时被拒', badSchemeRun.json?.success === false && /BaseUrl/.test(badSchemeRun.json?.error ?? ''), `${badSchemeRun.status} ${badSchemeRun.json?.error?.slice(0, 160)}`)
    await del(admin, badSchemeKey)

    // ---------- S59 search：行集解析 + 请求体透传 + 缺省窗口 + 校验与错误归一 ----------
    const search = await run(admin, CKS_OK, JSON.stringify({ Mode: 'search', SourceId: 'src-log-e2e', Where: 'SeverityText:ERROR', WhereLanguage: 'lucene', Select: 'Timestamp,ServiceName,SeverityText,Body', StartTime: '2026-09-29T00:00:00Z', EndTime: '2026-09-29T01:00:00Z', Offset: 0 }))
    const firstRow = search.data?.Rows?.[0]
    check('@DYN-S65a search 行集解析（混合类型值 + RowCount + 满额 Truncated）',
      search.json?.success === true && search.data?.ResultType === 'search' && (search.data?.Rows ?? []).length === 2
      && firstRow?.SeverityText === 'ERROR' && firstRow?.Body === 'boom happened' && firstRow?.Duration === 12
      && search.data?.RowCount === 2 && search.data?.Truncated === true,
      JSON.stringify(search.data ?? search.json?.error ?? {}))
    check('@DYN-S65b 请求体透传（sourceId/where/whereLanguage/select/maxResults=MaxRows/offset/ISO 时间窗）',
      lastSearch?.sourceId === 'src-log-e2e' && lastSearch?.where === 'SeverityText:ERROR' && lastSearch?.whereLanguage === 'lucene'
      && lastSearch?.select === 'Timestamp,ServiceName,SeverityText,Body' && lastSearch?.maxResults === 2 && lastSearch?.offset === 0
      && lastSearch?.startTime === '2026-09-29T00:00:00Z' && lastSearch?.endTime === '2026-09-29T01:00:00Z',
      JSON.stringify(lastSearch ?? {}))

    const defaultWindow = await run(admin, CKS_OK, JSON.stringify({ Mode: 'search', SourceId: 'src-log-e2e' }))
    const windowMs = defaultWindow.json?.success === true ? Date.parse(lastSearch?.endTime ?? '') - Date.parse(lastSearch?.startTime ?? '') : -1
    check('@DYN-S65c 缺省时间窗=End-15分钟', windowMs === 15 * 60 * 1000, JSON.stringify(lastSearch ?? {}))

    const paged = await run(admin, CKS_OK, JSON.stringify({ Mode: 'search', SourceId: 'src-log-e2e', MaxResults: 50, Offset: 100 }))
    check('@DYN-S65d MaxResults 覆盖与 offset 翻页（未满额不置 Truncated）',
      paged.json?.success === true && lastSearch?.maxResults === 50 && lastSearch?.offset === 100 && paged.data?.Truncated === false,
      JSON.stringify({ lastSearch, data: paged.data }))

    const noSource = await run(admin, CKS_OK, JSON.stringify({ Mode: 'search' }))
    const badLang = await run(admin, CKS_OK, JSON.stringify({ Mode: 'search', SourceId: 'src-log-e2e', WhereLanguage: 'regex' }))
    check('@DYN-S65e 缺 SourceId 与非法 WhereLanguage 均被 400 拒绝',
      noSource.json?.success === false && badLang.json?.success === false && /WhereLanguage/.test(badLang.json?.error ?? ''),
      `${noSource.json?.error} / ${badLang.json?.error}`)

    const notFound = await run(admin, CKS_OK, JSON.stringify({ Mode: 'search', SourceId: 'src-404' }))
    const boomWhere = await run(admin, CKS_OK, JSON.stringify({ Mode: 'search', SourceId: 'src-log-e2e', Where: 'BOOM_WHERE' }))
    check('@DYN-S65f 服务端 404/400 错误归一（sourceId 不存在与 where 非法均透出可读原因）',
      notFound.json?.success === false && /404/.test(notFound.json?.error ?? '') && /source not found/.test(notFound.json?.error ?? '')
      && boomWhere.json?.success === false && /invalid where expression/.test(boomWhere.json?.error ?? ''),
      `${notFound.json?.error} / ${boomWhere.json?.error}`)

    // ---------- S60 chart：时间线解析 + epoch 毫秒透传 + 缺省窗口/聚合 + 校验 ----------
    const chart = await run(admin, CKS_OK, JSON.stringify({ Mode: 'chart', SourceId: 'src-log-e2e', Where: 'ServiceName:moai', Granularity: '5m', AggFn: 'count', GroupBy: 'ServiceName,SeverityText', StartTime: '2026-09-29T00:00:00Z', EndTime: '2026-09-29T01:00:00Z' }))
    check('@DYN-S66a chart 时间线解析（时间桶 ISO/聚合值/分组值）',
      chart.json?.success === true && chart.data?.ResultType === 'chart' && (chart.data?.Points ?? []).length === 2
      && chart.data.Points[0].TsBucket === '2025-01-01T00:00:00.000Z' && chart.data.Points[0].Value === 12.5
      && chart.data.Points[0].Group?.[0] === 'moai' && chart.data.Points[0].Group?.[1] === 'ERROR',
      JSON.stringify(chart.data ?? chart.json?.error ?? {}))
    check('@DYN-S66b 请求体透传（epoch 毫秒时间窗 + granularity + series 结构 + groupBy 数组）',
      lastChart?.startTime === 1790640000000 && lastChart?.endTime === 1790643600000 && lastChart?.granularity === '5m'
      && lastChart?.series?.[0]?.sourceId === 'src-log-e2e' && lastChart?.series?.[0]?.aggFn === 'count'
      && lastChart?.series?.[0]?.where === 'ServiceName:moai' && JSON.stringify(lastChart?.series?.[0]?.groupBy) === JSON.stringify(['ServiceName', 'SeverityText']),
      JSON.stringify(lastChart ?? {}))

    const chartDefault = await run(admin, CKS_OK, JSON.stringify({ Mode: 'chart', SourceId: 'src-log-e2e' }))
    const chartWindowMs = chartDefault.json?.success === true ? lastChart?.endTime - lastChart?.startTime : -1
    check('@DYN-S66c 缺省窗口=1小时且缺省 aggFn/granularity',
      chartWindowMs === 3600000 && lastChart?.granularity === '1h' && lastChart?.series?.[0]?.aggFn === 'count',
      JSON.stringify(lastChart ?? {}))

    const sumNoField = await run(admin, CKS_OK, JSON.stringify({ Mode: 'chart', SourceId: 'src-log-e2e', AggFn: 'sum' }))
    const badGran = await run(admin, CKS_OK, JSON.stringify({ Mode: 'chart', SourceId: 'src-log-e2e', Granularity: '99x' }))
    const badAgg = await run(admin, CKS_OK, JSON.stringify({ Mode: 'chart', SourceId: 'src-log-e2e', AggFn: 'p99' }))
    const chartNoSource = await run(admin, CKS_OK, JSON.stringify({ Mode: 'chart' }))
    check('@DYN-S66d 参数校验：sum 缺 Field/非法 Granularity/非法 AggFn/缺 SourceId 均 400',
      sumNoField.json?.success === false && /Field/.test(sumNoField.json?.error ?? '')
      && badGran.json?.success === false && badAgg.json?.success === false && /AggFn/.test(badAgg.json?.error ?? '')
      && chartNoSource.json?.success === false && /SourceId/.test(chartNoSource.json?.error ?? ''),
      `${sumNoField.json?.error} / ${badGran.json?.error} / ${badAgg.json?.error} / ${chartNoSource.json?.error}`)

    check('桩确被命中（sources/search/chart；未授权恰为错误 Key 探测的 1 次）', hits.sources >= 1 && hits.search >= 5 && hits.chart >= 2 && hits.unauthorized === 1, JSON.stringify(hits))

    // ---------- 清理 ----------
    for (const key of [CKS_OK, CKS_BAD]) {
      await del(admin, key)
    }

    console.log(`\n=== ClickStack 插件桩服务 E2E: PASS ${PASS} / FAIL ${FAIL} ===`)
    if (FAIL > 0) process.exitCode = 1
  } finally {
    if (process.env.CKS_KEEP_BACKEND === '1') {
      console.log(`CKS_KEEP_BACKEND=1，保留后端 pid=${backend.pid}`)
      backend.stdout?.destroy()
      backend.stderr?.destroy()
      backend.unref()
    } else {
      await killBackend(backend)
    }
    mock.close()
    mock.closeAllConnections()
  }
}

main().catch((e) => { console.error('E2E 异常:', e.message); process.exitCode = 1 })
