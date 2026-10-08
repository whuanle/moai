// 智能运维观测插件（Prometheus / Elasticsearch / ClickHouse / Grafana Tempo）桩服务端到端
// 做法：本地起一个多路桩服务（/prom/ /es/ /ch/ /tempo/ 四段路由）→ 拉起一个指向桩服务的独立后端 →
//       创建四个动态插件实例并运行 → 断言「实例 → 注册表模板 → Refit 客户端 → 桩服务 → 响应解析」整条链路，
//       覆盖鉴权头、参数下发、只读守卫、响应解析与错误归一，无需任何真实观测服务部署。
// 用法：node local-dev/observability-plugin-e2e.mjs
// 环境变量：OPE_BACKEND_PORT（默认 5195）｜OPE_MOCK_PORT（默认 5194）｜OPE_KEEP_BACKEND=1 保留后端供排查
import { createServer } from 'node:http'
import { spawn } from 'node:child_process'
import crypto from 'node:crypto'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const REPO_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const BACKEND_PORT = Number(process.env.OPE_BACKEND_PORT ?? 5195)
const MOCK_PORT = Number(process.env.OPE_MOCK_PORT ?? 5194)
const BASE = `http://127.0.0.1:${BACKEND_PORT}`
const MOCK = `http://127.0.0.1:${MOCK_PORT}`

const PROM_AUTH = `Basic ${Buffer.from('admin:opssecret').toString('base64')}`
const ES_AUTH = 'ApiKey VnNceEtleQ=='
const CH_AUTH = `Basic ${Buffer.from('chuser:chsecret').toString('base64')}`
const TEMPO_AUTH = `Basic ${Buffer.from('opsuser:opssecret').toString('base64')}`
const TEMPO_TENANT = 'tenant-ops'

let PASS = 0, FAIL = 0
const check = (name, cond, detail = '') => {
  if (cond) { PASS++; console.log(`PASS | ${name}`) }
  else { FAIL++; console.log(`FAIL | ${name} ${detail ? '— ' + detail : ''}`) }
}

// ---------------- 桩服务：按官方文档样例报文回放 ----------------
const PROM_VECTOR = { status: 'success', data: { resultType: 'vector', result: [{ metric: { __name__: 'node_cpu_seconds_total', job: 'node', instance: '10.0.0.1:9100' }, value: [1719000000.123, '0.85'] }] } }
const PROM_MATRIX = { status: 'success', data: { resultType: 'matrix', result: [{ metric: { __name__: 'up', job: 'node' }, values: [[1719000000, '1'], [1719000015, '1'], [1719000030, 'NaN']] }, { metric: { __name__: 'up', job: 'prom' }, values: [[1719000000, '0']] }] } }
const PROM_LABELS = { status: 'success', data: ['__name__', 'job', 'instance'] }
const PROM_LABEL_VALUES = { status: 'success', data: ['node', 'prometheus'] }
const PROM_SERIES = { status: 'success', data: [{ __name__: 'up', job: 'node' }, { __name__: 'up', job: 'prom' }] }
const PROM_ALERTS = { status: 'success', data: { alerts: [{ labels: { alertname: 'HighCPU', severity: 'warning' }, annotations: { summary: 'CPU 峰值过高' }, state: 'firing', activeAt: '2026-09-26T08:00:00Z', value: '0.93' }] } }
const PROM_RULES = { status: 'success', data: { groups: [{ name: 'ops.rules', file: '/etc/prom/rules.yml', rules: [{ name: 'HighCPU', alert: 'HighCPU', query: 'cpu > 0.9', duration: 300, state: 'firing', health: 'ok', labels: { severity: 'warning' }, annotations: { summary: 'CPU' } }, { name: 'jobs:up:sum', query: 'sum(up)', health: 'ok' }] }] } }

const ES_SEARCH = {
  took: 12,
  timed_out: false,
  hits: {
    total: { value: 2, relation: 'eq' },
    max_score: 1.3,
    hits: [
      { _index: 'logs-ops-2026.09.26', _id: 'docs-1', _score: 1.3, _source: { level: 'error', message: 'DB connection failed', service: 'api' } },
      { _index: 'logs-ops-2026.09.26', _id: 'docs-2', _score: 1.0, _source: { level: 'warn', message: 'GC pause', service: 'api' } },
    ],
  },
  aggregations: { error_count: { value: 42 } },
}
const ES_COUNT = { count: 7, _shards: { successful: 1, total: 1, skipped: 0, failed: 0 } }
const ES_MAPPING = { 'logs-ops-2026.09.26': { mappings: { properties: { level: { type: 'keyword' }, message: { type: 'text', fields: { keyword: { type: 'keyword' } } } } } } }
const ES_CAT = [{ health: 'green', status: 'open', index: 'logs-ops-2026.09.26', uuid: 'u-1', pri: 1, rep: 1, 'docs.count': 1200, 'store.size': '4.5mb' }]

const CH_COLUMNS = [
  { name: 'Timestamp', type: "DateTime64(9, 'UTC')" },
  { name: 'TraceId', type: 'String' },
  { name: 'SpanId', type: 'String' },
  { name: 'ServiceName', type: 'LowCardinality(String)' },
  { name: 'Name', type: 'LowCardinality(String)' },
  { name: 'StatusCode', type: 'Int32' },
  { name: 'Duration', type: 'Int64' },
  { name: 'Body', type: 'String' },
  { name: 'ResourceAttributes', type: 'Map(LowCardinality(String), String)' },
]
const CH_TRACES_ROWS = [
  { Timestamp: '2026-09-26 08:00:00.123456789', TraceId: 'abcdef1234567890', SpanId: 'span-1', ServiceName: 'api', Name: 'GET /api/orders', StatusCode: 2, Duration: 120, ResourceAttributes: { 'service.name': 'api', 'host.name': 'pod-1' } },
  { Timestamp: '2026-09-26 07:59:00.000000000', TraceId: 'abcdef1234567890', SpanId: 'span-2', ServiceName: 'api', Name: 'INTERNAL', StatusCode: 0, Duration: 80, ResourceAttributes: { 'service.name': 'api' } },
  { Timestamp: '2026-09-26 07:50:00.000000000', TraceId: 'other', SpanId: 'span-3', ServiceName: 'web', Name: 'GET /', StatusCode: 1, Duration: 15, ResourceAttributes: { 'service.name': 'web' } },
]
const TEMPO_SEARCH = { traces: [{ traceID: 'abcdef1234567890', rootServiceName: 'frontend', rootTraceName: 'GET /checkout', startTimeUnixNano: '1719000000000000000', durationMs: '1.2' }, { traceID: '1112223334', rootServiceName: 'payment', rootTraceName: 'GRPC Pay', startTimeUnixNano: '1719000005000000000', durationMs: '80' }] }
const TEMPO_TRACE = {
  batches: [{
    resource: { attributes: [{ key: 'service.name', value: { stringValue: 'frontend' } }, { key: 'host.name', value: { stringValue: 'pod-1' } }] },
    scopeSpans: [{
      scope: { name: 'http-router', version: '1.0' },
      spans: [
        { traceID: 'abcdef1234567890', spanID: 'span-1', name: 'GET /checkout', startTimeUnixNano: '1719000000000000000', endTimeUnixNano: '1719000001200000000', attributes: [{ key: 'http.method', value: { stringValue: 'GET' } }, { key: 'num', value: { intValue: '5' } }], status: { code: 1, message: '' } },
        { traceID: 'abcdef1234567890', spanID: 'span-2', parentSpanID: 'span-1', name: 'GRPC payment', startTimeUnixNano: '1719000000100000000', endTimeUnixNano: '1719000000115000000', attributes: [{ key: 'rpc.status', value: { stringValue: 'UNAVAILABLE' } }], status: { code: 2, message: 'unavailable' } },
      ],
    }],
  }],
}
const TEMPO_TAGS = { tagNames: ['.kind', 'http.method', 'service.name'] }
const TEMPO_TAG_VALUES = { tagValues: ['frontend', 'payment', 'api'] }

let promHits = 0, esHits = 0, chHits = 0, tempoHits = 0
let lastProm = null, lastES = null, lastCH = null, lastTempo = null

const sleep = (ms) => new Promise((r) => setTimeout(r, ms))

function startMock() {
  const server = createServer((req, res) => {
    let body = ''
    req.on('data', (c) => { body += c })
    req.on('end', () => {
      let parsed = null
      try { parsed = JSON.parse(body) } catch { /* 非 JSON（ClickHouse SQL 文本等） */ }
      const pathName = decodeURIComponent(new URL(req.url ?? '/', 'http://127.0.0.1').pathname)
      const query = new URL(req.url ?? '/', 'http://127.0.0.1')
      const send = (code, payload, contentType = 'application/json') => {
        res.writeHead(code, { 'Content-Type': contentType })
        res.end(typeof payload === 'string' ? payload : JSON.stringify(payload))
      }

      const auth = req.headers['authorization'] ?? ''
      const ok = (expected) => auth === expected

      // ---- Prometheus：/prom/api/v1/* ----
      if (pathName.startsWith('/prom/')) {
        promHits++; lastProm = { path: pathName, query: query.searchParams.get('query'), time: query.searchParams.get('time'), start: query.searchParams.get('start'), end: query.searchParams.get('end'), step: query.searchParams.get('step'), match: query.searchParams.get('match[]'), label: pathName, auth, body: parsed }
        if (!ok(PROM_AUTH)) return send(401, 'Unauthorized')
        if (pathName === '/prom/api/v1/query') {
          if (query.searchParams.get('query')?.includes('scalarm')) {
            return send(200, { status: 'success', data: { resultType: 'scalar', result: [1719000000.5, '42'] } })
          }

          return send(200, PROM_VECTOR)
        }
        if (pathName === '/prom/api/v1/query_range') return send(200, PROM_MATRIX)
        if (pathName === '/prom/api/v1/labels') return send(200, PROM_LABELS)
        if (pathName.includes('/api/v1/label/') && pathName.endsWith('/values')) return send(200, PROM_LABEL_VALUES)
        if (pathName === '/prom/api/v1/series') return send(200, PROM_SERIES)
        if (pathName === '/prom/api/v1/alerts') return send(200, PROM_ALERTS)
        if (pathName === '/prom/api/v1/rules') return send(200, PROM_RULES)
        return send(404, `no prom mock for ${pathName}`)
      }

      // ---- Elasticsearch：/es/**  ----
      if (pathName.startsWith('/es/')) {
        esHits++; lastES = { path: pathName, auth, accept: req.headers['content-type'], body: parsed }
        if (!ok(ES_AUTH)) return send(401, JSON.stringify({ error: { type: 'security_exception', reason: 'missing authentication credentials' }, status: 401 }))
        if (pathName.endsWith('/_search') && req.method === 'POST') {
          if (parsed === null || parsed?.query === undefined) return send(400, JSON.stringify({ error: { type: 'parsing_exception', reason: '[x] parsing failed' }, status: 400 }))
          return send(200, ES_SEARCH)
        }
        if (pathName.endsWith('/_count') && req.method === 'POST') return send(200, ES_COUNT)
        if (pathName.endsWith('/_mapping') && req.method === 'GET') return send(200, ES_MAPPING)
        if (pathName === '/es/_cat/indices' && req.method === 'GET') return send(200, ES_CAT)
        return send(404, `no es mock for ${pathName}`)
      }

      // ---- ClickHouse 挂在桩服务根路径：POST /?query=...&readonly=1...，FORMAT JSON（meta/data 信封）输出 ----
      if (pathName === '/') {
        chHits++
        const sql = query.searchParams.get('query')?.trim() ?? ''
        lastCH = { path: pathName, sql, readonly: query.searchParams.get('readonly'), maxExec: query.searchParams.get('max_execution_time'), maxRows: query.searchParams.get('max_result_rows'), overflow: query.searchParams.get('result_overflow_mode'), format: query.searchParams.get('default_format'), auth }
        if (!ok(CH_AUTH)) return send(401, 'Unauthorized.\n', 'text/plain')
        const chJson = (meta, data) => send(200, JSON.stringify({ meta, data, rows: data.length, statistics: { elapsed: 0.005, rows_read: 100, bytes_read: 4096 } }))
        if (sql === 'SHOW DATABASES') return chJson([{ name: 'database', type: 'String' }], [{ database: 'otel' }, { database: 'default' }])
        if (sql === 'SHOW TABLES FROM otel') return chJson([{ name: 'name', type: 'String' }], [{ name: 'otel_traces' }, { name: 'otel_logs' }, { name: 'otel_metrics_gauge' }])
        if (sql.startsWith('DESCRIBE TABLE otel.otel_traces')) return chJson([{ name: 'name', type: 'String' }, { name: 'type', type: 'String' }], [{ name: 'Timestamp', type: "DateTime64(9, 'UTC')" }, { name: 'TraceId', type: 'String' }, { name: 'ServiceName', type: 'LowCardinality(String)' }])
        if (sql.startsWith('SHOW CREATE TABLE otel.otel_traces')) return chJson([{ name: 'statement', type: 'String' }], [{ statement: 'CREATE TABLE otel.otel_traces (...) ENGINE = MergeTree ORDER BY (ServiceName, Timestamp)' }])
        if (sql.startsWith('SELECT Timestamp')) return chJson(CH_COLUMNS, CH_TRACES_ROWS)
        if (sql === 'SELECT 1') return chJson([{ name: '1', type: 'UInt8' }], [{ 1: 1 }])
        if (sql.includes('WHERE 0')) return chJson([{ name: 'TraceId', type: 'String' }], [])
        return send(500, 'Code: 60. DB::Exception: Unknown expression identifier (version 24.3.1.2671)', 'text/plain')
      }

      // ---- Tempo：/tempo/api/* ----
      if (pathName.startsWith('/tempo/')) {
        tempoHits++; lastTempo = { path: pathName, rawUrl: req.url ?? '', auth, tenant: req.headers['x-scope-orgid'], query: query.searchParams.get('query'), limit: query.searchParams.get('limit'), start: query.searchParams.get('start'), end: query.searchParams.get('end'), accept: req.headers['accept'] }
        if (!ok(TEMPO_AUTH)) return send(401, 'Unauthorized')
        if (req.headers['x-scope-orgid'] !== TEMPO_TENANT) return send(403, 'missing tenant')
        if (pathName === '/tempo/api/search') {
          if (!query.searchParams.get('query')) return send(400, 'missing query')
          return send(200, TEMPO_SEARCH)
        }
        if (pathName.startsWith('/tempo/api/traces/') && req.method === 'GET') {
          if (pathName.endsWith('/missing')) return send(404, 'trace not found', 'text/plain')
          if (lastTempo.accept !== 'application/json') return send(400, 'need Accept: application/json')
          return send(200, TEMPO_TRACE)
        }
        if (pathName === '/tempo/api/search/tags') return send(200, TEMPO_TAGS)
        if (pathName.startsWith('/tempo/api/search/tag/') && pathName.endsWith('/values')) {
          return send(200, TEMPO_TAG_VALUES)
        }
        return send(404, `no tempo mock for ${pathName}`)
      }

      return send(404, `no mock for ${pathName}`)
    })
  })
  return new Promise((resolve) => server.listen(MOCK_PORT, '127.0.0.1', () => resolve(server)))
}

// ---------------- 后端：优先用独立输出目录 DLL（绕开运行中后端的 DLL 锁），否则回退 dotnet run ----------------
function startBackend() {
  const dll = path.join(REPO_ROOT, '.builds', 'observability', 'MoAI.dll')
  if (!fs.existsSync(dll)) {
    console.log('WARN | 未找到 .builds/observability/MoAI.dll，回退 dotnet run（默认 bin/Debug；若后端正被运行会因 DLL 锁失败）')
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
  for (let i = 0; i < 60; i++) {
    if (child.exitCode !== null) throw new Error(`后端进程提前退出（code=${child.exitCode}）：\n${child.getLog().slice(-2000)}`)
    try {
      const r = await fetch(`${BASE}/api/common/serverinfo`, { signal: AbortSignal.timeout(3000) })
      if (r.ok) return true
    } catch { /* 未就绪 */ }
    await sleep(2000)
  }
  throw new Error(`后端 ${BASE} 未在 120s 内就绪：\n${child.getLog().slice(-2000)}`)
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

async function main() {
  const mock = await startMock()
  console.log(`桩服务已就绪：${MOCK}（/prom/ /es/ /ch/ /tempo/ 四段路由）`)
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
    const keys = {
      prom: `ops_prom_${TS}`, promBad: `ops_prombad_${TS}`, promBadBase: `ops_prombase_${TS}`,
      es: `ops_es_${TS}`, esAgg: `ops_esagg_${TS}`, esBadBase: `ops_esbase_${TS}`,
      ch: `ops_ch_${TS}`, chBadAuth: `ops_chbadauth_${TS}`, chBadBase: `ops_chbase_${TS}`,
      tempo: `ops_tempo_${TS}`, tempoBadBase: `ops_tempobase_${TS}`,
    }

    // ================= Prometheus =================
    check('创建 Prometheus 实例', (await save(admin, { pluginKey: keys.prom, templeteKey: 'prometheus_query', title: '桩服务 Prometheus', description: 'mock', classifyId: 0, config: JSON.stringify({ BaseUrl: `${MOCK}/prom/`, Username: 'admin', Password: 'opssecret', TimeoutSeconds: 5, MaxSeries: 1, MaxPointsPerSeries: 2, MaxListItems: 200 }) })).status === 200)

    const pv = await run(admin, keys.prom, JSON.stringify({ Mode: 'instant', Query: '  up  ', Time: '1719000000' }))
    check('@DYN-S50a Prometheus instant 运行成功且解析向量', pv.json?.success === true && pv.data?.ResultType === 'vector' && pv.data?.Vector?.length === 1, `${pv.status} ${pv.text.slice(0, 200)}`)
    check('@DYN-S50b 向量样本解析（标签集/时间/值）', pv.data?.Vector?.[0]?.Labels?.__name__ === 'node_cpu_seconds_total' && pv.data?.Vector?.[0]?.Labels?.job === 'node' && pv.data?.Vector?.[0]?.Value === '0.85', JSON.stringify(pv.data?.Vector?.[0] ?? {}))
    check('@DYN-S50c PromQL 去空格下发且带 Time', lastProm?.query === 'up' && lastProm?.time === '1719000000', JSON.stringify(lastProm ?? {}))
    check('@DYN-S50d 鉴权头为 Basic', lastProm?.auth === PROM_AUTH, lastProm?.auth ?? '')

    const psc = await run(admin, keys.prom, JSON.stringify({ Mode: 'instant', Query: 'scalarm_value' }))
    check('@DYN-S50e scalar 结果解析', psc.json?.success === true && psc.data?.ResultType === 'scalar' && psc.data?.Scalar?.Value === '42' && psc.data?.Vector?.length === 0, JSON.stringify(psc.data?.Scalar))

    const pr = await run(admin, keys.prom, JSON.stringify({ Mode: 'range', Query: 'up', Start: '1719000000', End: '1719000060', Step: '1m' }))
    check('@DYN-S51a Prometheus range 解析矩阵', pr.json?.success === true && pr.data?.ResultType === 'matrix' && pr.data?.Matrix?.length === 1, `${pr.status} ${pr.text.slice(0, 200)}`)
    check('@DYN-S51b MaxPointsPerSeries 保留最近 2 点', pr.data?.Matrix?.[0]?.Samples?.length === 2 && pr.data?.Matrix?.[0]?.Samples?.[1]?.Value === 'NaN', JSON.stringify(pr.data?.Matrix?.[0]))
    check('@DYN-S51c MaxSeries 截断并标记 Truncated', pr.data?.Matrix?.length === 1 && pr.data?.Truncated === true, JSON.stringify({ m: pr.data?.Matrix?.length, t: pr.data?.Truncated }))
    check('@DYN-S51d range 参数下发（start/end/step）', lastProm?.start === '1719000000' && lastProm?.end === '1719000060' && lastProm?.step === '1m', JSON.stringify(lastProm ?? {}))

    await run(admin, keys.prom, JSON.stringify({ Mode: 'labels' }))
    const pl = await run(admin, keys.prom, JSON.stringify({ Mode: 'labels' }))
    check('@DYN-S52a labels 列表解析', pl.json?.success === true && pl.data?.ResultType === 'labels' && pl.data?.Labels?.length === 3 && pl.data?.Labels?.[0] === '__name__', JSON.stringify(pl.data?.Labels))
    const plv = await run(admin, keys.prom, JSON.stringify({ Mode: 'label_values', Label: 'job' }))
    check('@DYN-S52b label_values 解析（node/prometheus）', plv.json?.success === true && plv.data?.ResultType === 'label_values' && plv.data?.Labels?.[0] === 'node' && plv.data?.Labels?.[1] === 'prometheus', JSON.stringify(plv.data?.Labels))
    check('@DYN-S52d label_values 目标标签出现在 URL 路径', lastProm?.label.includes('/api/v1/label/job/values') === true, lastProm?.label ?? '')
    const ps = await run(admin, keys.prom, JSON.stringify({ Mode: 'series', Selector: 'job=node' }))
    check('@DYN-S52c series 标签集解析', ps.json?.success === true && ps.data?.ResultType === 'series' && ps.data?.Series?.length === 2 && ps.data?.Series?.[1]?.job === 'prom', JSON.stringify(ps.data?.Series))

    const pa = await run(admin, keys.prom, JSON.stringify({ Mode: 'alerts' }))
    check('@DYN-S53a alerts 解析（标签/注解/状态/触发时间/值）', pa.json?.success === true && pa.data?.Alerts?.length === 1 && pa.data?.Alerts?.[0]?.Labels?.alertname === 'HighCPU' && pa.data?.Alerts?.[0]?.Annotations?.summary === 'CPU 峰值过高' && pa.data?.Alerts?.[0]?.State === 'firing' && pa.data?.Alerts?.[0]?.Value === '0.93', JSON.stringify(pa.data?.Alerts))
    const prules = await run(admin, keys.prom, JSON.stringify({ Mode: 'rules' }))
    check('@DYN-S53b rules 解析（告警规则/记录规则区分）', prules.json?.success === true && prules.data?.Rules?.length === 1 && prules.data?.Rules?.[0]?.Name === 'ops.rules' && prules.data?.Rules?.[0]?.Rules?.[0]?.Type === 'alerting' && prules.data?.Rules?.[0]?.Rules?.[1]?.Type === 'recording', JSON.stringify(prules.data?.Rules))

    await save(admin, { pluginKey: keys.promBad, templeteKey: 'prometheus_query', title: '桩服务 Prometheus 无效鉴权', description: 'mock', classifyId: 0, config: JSON.stringify({ BaseUrl: `${MOCK}/prom/`, BearerToken: 'bad-token' }) })
    const pbad = await run(admin, keys.promBad, JSON.stringify({ Mode: 'instant', Query: 'up' }))
    check('@DYN-S54a 上游 401 归一为可读失败且带响应体', pbad.json?.success === false && /HTTP 401/.test(pbad.json?.error ?? '') && /Unauthorized/.test(pbad.json?.error ?? ''), `${pbad.status} ${pbad.json?.error}`)

    await save(admin, { pluginKey: keys.promBadBase, templeteKey: 'prometheus_query', title: '桩服务 Prometheus 空地址', description: 'mock', classifyId: 0, config: JSON.stringify({ BaseUrl: 'not-a-url' }) })
    const pbadbase = await run(admin, keys.promBadBase, JSON.stringify({ Mode: 'instant', Query: 'up' }))
    check('@DYN-S55a 非法 BaseUrl 返回可读失败', pbadbase.json?.success === false && /BaseUrl/.test(pbadbase.json?.error ?? ''), `${pbadbase.json?.error}`)
    const pbadmode = await run(admin, keys.prom, JSON.stringify({ Mode: 'nosuchmode', Query: 'up' }))
    check('@DYN-S55b 未知 Mode 返回可读失败', pbadmode.json?.success === false && /Mode/.test(pbadmode.json?.error ?? ''), `${pbadmode.json?.error}`)
    const pbadq = await run(admin, keys.prom, JSON.stringify({ Mode: 'instant', Query: '   ' }))
    check('@DYN-S55c 空 PromQL 返回可读失败', pbadq.json?.success === false && /Query/.test(pbadq.json?.error ?? ''), `${pbadq.json?.error}`)

    // ================= Elasticsearch =================
    check('创建 Elasticsearch 实例', (await save(admin, { pluginKey: keys.es, templeteKey: 'elasticsearch_query', title: '桩服务 ES', description: 'mock', classifyId: 0, config: JSON.stringify({ BaseUrl: `${MOCK}/es`, ApiKey: 'VnNceEtleQ==', TimeoutSeconds: 5, MaxHits: 100, MaxSourceCharsPerHit: 4000 }) })).status === 200)

    const esr = await run(admin, keys.es, JSON.stringify({ Mode: 'search', Index: 'logs-*', Dsl: { query: { match: { message: 'error' } }, size: 20, _source: ['level', 'message'] } }))
    check('@DYN-S56a ES search 运行成功', esr.json?.success === true && esr.data?.Mode === 'search', `${esr.status} ${esr.text.slice(0, 200)}`)
    check('@DYN-S56b 命中解析（索引/id/得分）', esr.data?.TotalHits === 2 && esr.data?.Hits?.length === 2 && esr.data?.Hits?.[0]?.Index === 'logs-ops-2026.09.26' && esr.data?.Hits?.[0]?.Id === 'docs-1' && esr.data?.Hits?.[0]?.Score === '1.3', JSON.stringify(esr.data?.Hits))
    check('@DYN-S56c _source 提取', esr.data?.Hits?.[0]?.SourceJson?.includes('DB connection failed') === true && esr.data?.Hits?.[1]?.SourceJson?.includes('GC pause') === true, JSON.stringify(esr.data?.Hits?.[0]))
    check('@DYN-S56d 聚合原样回写', esr.data?.AggregationsJson === '{"error_count":{"value":42}}', esr.data?.AggregationsJson ?? '')
    check('@DYN-S56e DSL 以 JSON 下发（query 透传）', lastES?.body?.query?.match?.message === 'error' && (lastES?.path.includes('_search')) === true, JSON.stringify(lastES ?? {}))
    check('@DYN-S56f 鉴权头为 ApiKey', lastES?.auth === ES_AUTH, lastES?.auth ?? '')

    await save(admin, { pluginKey: keys.esAgg, templeteKey: 'elasticsearch_query', title: '桩服务 ES 截断', description: 'mock', classifyId: 0, config: JSON.stringify({ BaseUrl: `${MOCK}/es`, ApiKey: 'VnNceEtleQ==', MaxHits: 1, MaxResponseChars: 1 }) })
    const esTrunc = await run(admin, keys.esAgg, JSON.stringify({ Mode: 'search', Index: 'logs-*', Dsl: { query: { match: { message: 'error' } } } }))
    check('@DYN-S57a MaxHits 截断 + 聚合截断 + Truncated', esTrunc.json?.success === true && esTrunc.data?.Hits?.length === 1 && (esTrunc.data?.AggregationsJson ?? '').length <= 1 && esTrunc.data?.Truncated === true, JSON.stringify({ h: esTrunc.data?.Hits?.length, tr: esTrunc.data?.Truncated }))

    const esCount = await run(admin, keys.es, JSON.stringify({ Mode: 'count', Index: 'logs-*', Dsl: { query: { match_all: {} } } }))
    check('@DYN-S58a count 计数', esCount.json?.success === true && esCount.data?.Count === 7 && esCount.data?.TotalHits === 7, JSON.stringify(esCount.data))
    const esMap = await run(admin, keys.es, JSON.stringify({ Mode: 'mappings', Index: 'logs-*' }))
    check('@DYN-S58b mappings 原文回写', esMap.json?.success === true && (esMap.data?.MappingJson ?? '').includes('"type":"keyword"') && esMap.data?.MappingJson?.includes('logs-ops-2026.09.26'), (esMap.data?.MappingJson ?? '').slice(0, 120))
    const esIdx = await run(admin, keys.es, JSON.stringify({ Mode: 'indices' }))
    check('@DYN-S58c indices 清单解析（CAT 列裁剪）', esIdx.json?.success === true && esIdx.data?.Indices?.length === 1 && esIdx.data?.Indices?.[0]?.Health === 'green' && esIdx.data?.Indices?.[0]?.Index === 'logs-ops-2026.09.26' && String(esIdx.data?.Indices?.[0]?.DocsCount) === '1200' && esIdx.data?.Indices?.[0]?.StoreSize === '4.5mb', JSON.stringify(esIdx.data?.Indices))

    const esErr = await run(admin, keys.es, JSON.stringify({ Mode: 'search', Index: 'logs-*', Dsl: {} }))
    check('@DYN-S59a ES 400 归一为 HTTP 状态 + error.type/reason', esErr.json?.success === false && /HTTP 400/.test(esErr.json?.error ?? '') && /parsing_exception/.test(esErr.json?.error ?? '') && /parsing failed/.test(esErr.json?.error ?? ''), esErr.json?.error)

    await save(admin, { pluginKey: keys.esBadBase, templeteKey: 'elasticsearch_query', title: '桩服务 ES 空地址', description: 'mock', classifyId: 0, config: JSON.stringify({ BaseUrl: ' ' }) })
    const esBadBase = await run(admin, keys.esBadBase, JSON.stringify({ Mode: 'search' }))
    check('@DYN-S60a 空 BaseUrl 返回可读失败', esBadBase.json?.success === false && /BaseUrl/.test(esBadBase.json?.error ?? ''), esBadBase.json?.error)

    // ================= ClickHouse（自由只读 SQL） =================
    check('创建 ClickHouse 实例', (await save(admin, { pluginKey: keys.ch, templeteKey: 'clickhouse_query', title: '桩服务 CH', description: 'mock', classifyId: 0, config: JSON.stringify({ BaseUrl: `${MOCK}/`, Username: 'chuser', Password: 'chsecret', TimeoutSeconds: 5, MaxRows: 2 }) })).status === 200)

    const chq = await run(admin, keys.ch, JSON.stringify({ Sql: 'SELECT Timestamp, TraceId, ServiceName, Duration FROM otel.otel_traces ORDER BY Timestamp DESC' }))
    check('@DYN-S71a 自由 SELECT 成功并按 MaxRows 截断', chq.json?.success === true && chq.data?.RowCount === 2 && chq.data?.Rows?.length === 2 && chq.data?.Truncated === true, `${chq.status} ${chq.text.slice(0, 240)}`)
    check('@DYN-S71b 列与列类型来自 FORMAT JSON meta', chq.data?.Columns?.[0] === 'Timestamp' && chq.data?.Columns?.includes('Duration') && chq.data?.ColumnTypes?.[0]?.includes('DateTime64') && chq.data?.ColumnTypes?.includes('Int64'), JSON.stringify({ c: chq.data?.Columns, t: chq.data?.ColumnTypes }))
    check('@DYN-S71c 行值归一（字符串/整数/Map 对象）', chq.data?.Rows?.[0]?.TraceId === 'abcdef1234567890' && chq.data?.Rows?.[0]?.Duration === 120 && typeof chq.data?.Rows?.[0]?.ResourceAttributes === 'object' && chq.data?.Rows?.[0]?.ResourceAttributes?.['service.name'] === 'api', JSON.stringify(chq.data?.Rows?.[0] ?? {}))
    check('@DYN-S71d 连接层强制只读与资源上限（readonly=1 + max_result_rows=MaxRows+1 + FORMAT JSON + Basic）', lastCH?.readonly === '1' && lastCH?.maxRows === '3' && lastCH?.format === 'JSON' && lastCH?.overflow === 'break' && lastCH?.auth === CH_AUTH, JSON.stringify(lastCH ?? {}))

    const chDbs = await run(admin, keys.ch, JSON.stringify({ Sql: 'SHOW DATABASES' }))
    check('@DYN-S71e SHOW DATABASES 摸库', chDbs.json?.success === true && chDbs.data?.Rows?.length === 2 && chDbs.data?.Rows?.[0]?.database === 'otel', JSON.stringify(chDbs.data?.Rows))
    const chTables = await run(admin, keys.ch, JSON.stringify({ Sql: 'SHOW TABLES FROM otel' }))
    check('@DYN-S71f SHOW TABLES 摸表（同样受 MaxRows 截断）', chTables.json?.success === true && chTables.data?.Rows?.length === 2 && chTables.data?.Rows?.[0]?.name === 'otel_traces' && chTables.data?.Rows?.[1]?.name === 'otel_logs' && chTables.data?.Truncated === true, JSON.stringify(chTables.data))
    const chDesc = await run(admin, keys.ch, JSON.stringify({ Sql: 'DESCRIBE TABLE otel.otel_traces' }))
    check('@DYN-S71g DESCRIBE TABLE 看列结构', chDesc.json?.success === true && chDesc.data?.Rows?.[0]?.name === 'Timestamp' && (chDesc.data?.Rows?.[0]?.type ?? '').includes('DateTime64'), JSON.stringify(chDesc.data?.Rows?.[0] ?? {}))
    const chCreate = await run(admin, keys.ch, JSON.stringify({ Sql: 'SHOW CREATE TABLE otel.otel_traces' }))
    check('@DYN-S71h SHOW CREATE TABLE 放行（对象名含 CREATE 不再被误杀）', chCreate.json?.success === true && (chCreate.data?.Rows?.[0]?.statement ?? '').includes('MergeTree'), chCreate.json?.error ?? JSON.stringify(chCreate.data?.Rows?.[0]))
    const chEmpty = await run(admin, keys.ch, JSON.stringify({ Sql: 'SELECT TraceId FROM otel.otel_traces WHERE 0' }))
    check('@DYN-S71i 空结果集仍返回列信息', chEmpty.json?.success === true && chEmpty.data?.Rows?.length === 0 && chEmpty.data?.Columns?.includes('TraceId') && chEmpty.data?.ColumnTypes?.[0] === 'String', JSON.stringify(chEmpty.data))
    const chForm = await run(admin, keys.ch, JSON.stringify({ Sql: 'SELECT 1 FORMAT JSON ;' }))
    check('@DYN-S71j 末尾 FORMAT 子句被剥离（出参固定 JSON）', chForm.json?.success === true && lastCH?.sql === 'SELECT 1' && lastCH?.format === 'JSON', `${lastCH?.sql ?? ''} | ${lastCH?.format ?? ''}`)

    const chForbidden = [
      ['INSERT', 'INSERT INTO otel.otel_traces VALUES (1)'],
      ['UPDATE', 'UPDATE otel.otel_traces SET Duration = 1'],
      ['DELETE', 'DELETE FROM otel.otel_traces'],
      ['DDL-DROP', 'DROP TABLE otel.otel_traces'],
      ['DDL-CREATE', 'CREATE TABLE demo (x UInt8) ENGINE = Memory'],
      ['会话变更', 'SET max_threads = 1'],
      ['SYSTEM', 'SYSTEM RELOAD CONFIG'],
      ['url 表函数', "SELECT * FROM url('http://x')"],
      ['多语句', 'SELECT 1; SELECT 2'],
    ]
    const chHitsBeforeGuard = chHits
    let chForbiddenBlocked = true
    for (const [label, forbiddenSql] of chForbidden) {
      const r = await run(admin, keys.ch, JSON.stringify({ Sql: forbiddenSql }))
      chForbiddenBlocked = chForbiddenBlocked && r.json?.success === false && (r.json?.error ?? '').includes('只允许')
      if (r.json?.success !== false) console.log(`INFO | ${label} 未被拒：${r.json?.error}`)
    }
    check('@DYN-S72a 写操作/DDL/会话/外部源函数/多语句全部被拒且未触达桩', chForbiddenBlocked && chHits === chHitsBeforeGuard, JSON.stringify({ blocked: chForbiddenBlocked, hits: [chHitsBeforeGuard, chHits] }))

    const chErr = await run(admin, keys.ch, JSON.stringify({ Sql: 'SELECT no_such_column FROM otel.otel_traces' }))
    check('@DYN-S72b 上游 500 归一为可读失败', chErr.json?.success === false && /HTTP 500/.test(chErr.json?.error ?? '') && /Exception/.test(chErr.json?.error ?? ''), chErr.json?.error)

    await save(admin, { pluginKey: keys.chBadAuth, templeteKey: 'clickhouse_query', title: '桩服务 CH 错误凭据', description: 'mock', classifyId: 0, config: JSON.stringify({ BaseUrl: `${MOCK}/`, Username: 'chuser', Password: 'wrong', TimeoutSeconds: 5, MaxRows: 2 }) })
    const ch401 = await run(admin, keys.chBadAuth, JSON.stringify({ Sql: 'SELECT 1' }))
    check('@DYN-S72c 上游 401 归一为可读失败', ch401.json?.success === false && /HTTP 401/.test(ch401.json?.error ?? ''), ch401.json?.error)

    await save(admin, { pluginKey: keys.chBadBase, templeteKey: 'clickhouse_query', title: '桩服务 CH 空地址', description: 'mock', classifyId: 0, config: JSON.stringify({ BaseUrl: '' }) })
    const chBadBase = await run(admin, keys.chBadBase, JSON.stringify({ Sql: 'SELECT 1' }))
    check('@DYN-S72d 空 BaseUrl 返回可读失败', chBadBase.json?.success === false && /BaseUrl/.test(chBadBase.json?.error ?? ''), chBadBase.json?.error)

    // ================= Tempo =================
    check('创建 Tempo 实例', (await save(admin, { pluginKey: keys.tempo, templeteKey: 'tempo_query', title: '桩服务 Tempo', description: 'mock', classifyId: 0, config: JSON.stringify({ BaseUrl: `${MOCK}/tempo`, Username: 'opsuser', Password: 'opssecret', TenantId: TEMPO_TENANT, TimeoutSeconds: 5, MaxTraces: 1, MaxSpans: 2 }) })).status === 200)

    await run(admin, keys.tempo, JSON.stringify({ Mode: 'traceql', Query: '{service.name="api" && duration>1s}', Limit: 9 })); console.log(`INFO | tempo rawUrl: ${lastTempo?.rawUrl ?? ''}`)
    const tq = await run(admin, keys.tempo, JSON.stringify({ Mode: 'traceql', Query: '{service.name="api" && duration>1s}', Limit: 9 }))
    check('@DYN-S66a TraceQL 检索成功并解析链路摘要', tq.json?.success === true && tq.data?.Traces?.length === 1 && tq.data?.Traces?.[0]?.TraceId === 'abcdef1234567890' && tq.data?.Traces?.[0]?.RootServiceName === 'frontend' && tq.data?.Traces?.[0]?.DurationMs === '1.2', `${tq.status} ${tq.text.slice(0, 240)}`)
    check('@DYN-S66b MaxTraces 截断标记（桩返回 2 条）', tq.data?.Truncated === true, JSON.stringify({ t: tq.data?.Truncated, c: tq.data?.Traces?.length }))
    check('@DYN-S66c limit 参数与租户头下发', lastTempo?.limit === '1' && lastTempo?.tenant === TEMPO_TENANT, JSON.stringify(lastTempo ?? {}))

    const tt = await run(admin, keys.tempo, JSON.stringify({ Mode: 'trace', TraceId: 'abcdef1234567890' }))
    check('@DYN-S67a 链路详情解析（batch/资源属性/spans）', tt.json?.success === true && tt.data?.Batches?.length === 1 && tt.data?.Batches?.[0]?.Resource?.['service.name'] === 'frontend' && tt.data?.Batches?.[0]?.Spans?.length === 2, `${tt.status} ${tt.text.slice(0, 240)}`)
    check('@DYN-S67b span 属性/状态/时长解析', tt.data?.Batches?.[0]?.Spans?.[0]?.Attributes?.['http.method'] === 'GET' && tt.data?.Batches?.[0]?.Spans?.[0]?.StartTimeUnixNano === '1719000000000000000' && tt.data?.Batches?.[0]?.Spans?.[0]?.DurationNanos === '1200000000' && tt.data?.Batches?.[0]?.Spans?.[1]?.StatusCode === '2' && tt.data?.Batches?.[0]?.Spans?.[1]?.StatusMessage === 'unavailable', JSON.stringify(tt.data?.Batches?.[0]?.Spans))
    check('@DYN-S67c 取链路带 Accept: application/json', lastTempo?.accept === 'application/json', lastTempo?.accept ?? '')

    const tmiss = await run(admin, keys.tempo, JSON.stringify({ Mode: 'trace', TraceId: 'missing' }))
    check('@DYN-S67d TraceID 不存在（404）归一为可读失败', tmiss.json?.success === false && /HTTP 404/.test(tmiss.json?.error ?? ''), tmiss.json?.error)

    const ttags = await run(admin, keys.tempo, JSON.stringify({ Mode: 'tags' }))
    check('@DYN-S68a tags 列表解析', ttags.json?.success === true && ttags.data?.Tags?.length === 3 && ttags.data?.Tags?.includes('http.method'), JSON.stringify(ttags.data?.Tags))
    const ttv = await run(admin, keys.tempo, JSON.stringify({ Mode: 'tag_values' }))
    check('@DYN-S68b tag_values 默认 service.name 列出服务', ttv.json?.success === true && ttv.data?.TagValues?.length === 3 && ttv.data?.TagValues?.includes('payment'), JSON.stringify(ttv.data?.TagValues))

    await save(admin, { pluginKey: keys.tempoBadBase, templeteKey: 'tempo_query', title: '桩服务 Tempo 空地址', description: 'mock', classifyId: 0, config: JSON.stringify({ BaseUrl: '' }) })
    const tbad = await run(admin, keys.tempoBadBase, JSON.stringify({ Mode: 'traceql', Query: '{service.name="api"}' }))
    check('@DYN-S69a 空 BaseUrl 返回可读失败', tbad.json?.success === false && /BaseUrl/.test(tbad.json?.error ?? ''), tbad.json?.error)
    const tbadmode = await run(admin, keys.tempo, JSON.stringify({ Mode: 'nope' }))
    check('@DYN-S69b 未知 Mode 返回可读失败', tbadmode.json?.success === false && /Mode/.test(tbadmode.json?.error ?? ''), tbadmode.json?.error)

    check('桩服务确被四类服务命中', promHits >= 10 && esHits >= 5 && chHits >= 6 && tempoHits >= 3, JSON.stringify({ promHits, esHits, chHits, tempoHits }))

    // ---------- 清理 ----------
    for (const key of Object.values(keys)) await del(admin, key)

    console.log(`\n=== 智能运维观测插件桩服务 E2E: PASS ${PASS} / FAIL ${FAIL} ===`)
    if (FAIL > 0) process.exitCode = 1
  } finally {
    if (process.env.OPE_KEEP_BACKEND === '1') console.log(`OPE_KEEP_BACKEND=1，保留后端 pid=${backend.pid}`)
    else await killBackend(backend)
    mock.close()
  }
}

main().catch((e) => { console.error('E2E 异常:', e.message); process.exitCode = 1 })


