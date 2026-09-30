// P0 智能运维插件（alertmanager_query / loki_query / kubernetes_query / dingtalk_webhook_text / wecom_webhook_text）端到端
// 做法：本地起一个多路 HTTP 桩（/am=Alertmanager v2、/loki=Loki API、/k8s*=API Server、/robot/send 与 /cgi-bin/webhook/send=通知机器人）
//       → 拉起一个独立后端（MoAI__DingTalk__RobotEndpoint / MoAI__WeixinWork__RobotEndpoint 指向桩）→
//       创建实例并运行 → 断言「实例 → 注册表模板 → 客户端 → 桩服务 → 响应解析」整条链路，
//       覆盖告警/静默/状态解析、LogQL 时间归一、K8s 资源与日志读取、钉钉 HMAC 加签与错误归一，无需真实运维系统。
// 前置：宿主已按独立输出目录构建（默认 bin/Debug 可能被运行中的后端锁定）：
//       dotnet build src/MoAI/MoAI.csproj -o .builds/ops-p0
// 用法：node local-dev/ops-p0-plugins-e2e.mjs
// 环境变量：OPP0_BACKEND_PORT（默认 5188）｜OPP0_MOCK_PORT（默认 5187）｜OPP0_KEEP_BACKEND=1 保留后端供排查
import { createServer } from 'node:http'
import { spawn } from 'node:child_process'
import crypto from 'node:crypto'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const REPO_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const BACKEND_PORT = Number(process.env.OPP0_BACKEND_PORT ?? 5188)
const MOCK_PORT = Number(process.env.OPP0_MOCK_PORT ?? 5187)
const BASE = `http://127.0.0.1:${BACKEND_PORT}`
const MOCK = `http://127.0.0.1:${MOCK_PORT}`
const AM_TOKEN = 'am-token'
const K8S_TOKEN = 'k8s-token'
const DT_TOKEN = 'e2e-dt-token'
const DT_SECRET = 'SEC-e2e-secret'
const WC_KEY = 'e2e-wc-key-uuid'

let PASS = 0, FAIL = 0, SKIP = 0
const check = (name, cond, detail = '') => {
  if (cond) { PASS++; console.log(`PASS | ${name}`) }
  else { FAIL++; console.log(`FAIL | ${name} ${detail ? '— ' + detail : ''}`) }
}
const skip = (name, why) => { SKIP++; console.log(`SKIP | ${name} — ${why}`) }

const sleep = (ms) => new Promise((r) => setTimeout(r, ms))

// ---------------- 桩数据 ----------------
const AM_ALERTS = [
  { labels: { alertname: 'HighDiskUsage', instance: 'web-01', severity: 'critical' }, annotations: { summary: '磁盘使用率 95%' }, startsAt: '2026-09-29T00:00:00Z', endsAt: '0001-01-01T00:00:00Z', state: 'active', generatorURL: 'http://prom/graph', silencedBy: [], inhibitedBy: [] },
  { labels: { alertname: 'HighCPU', instance: 'web-02' }, annotations: { summary: 'CPU 过高' }, startsAt: '2026-09-28T22:00:00Z', endsAt: '2026-09-28T23:00:00Z', state: 'suppressed', generatorURL: '', silencedBy: ['silence-1'], inhibitedBy: [] },
]
const AM_SILENCES = [
  { id: 'silence-1', status: { state: 'active' }, matchers: [{ name: 'alertname', value: 'HighCPU', isRegex: false }, { name: 'instance', value: 'web-0.*', isRegex: true }], startsAt: '2026-09-28T20:00:00Z', endsAt: '2026-09-30T20:00:00Z', createdBy: 'ops-admin', comment: '例行维护窗口' },
]
const AM_STATUS = { cluster: { peers: [{ name: 'am-01' }, { name: 'am-02' }], status: 'ready' }, versionInfo: { version: '0.27.0' } }
const LOKI_STREAMS = {
  status: 'success',
  data: {
    resultType: 'streams',
    result: [
      { stream: { job: 'nginx', instance: 'web-01' }, values: [['1790640000000000000', '10.0.0.1 - - GET /healthz 200'], ['1790640060000000000', '10.0.0.1 - - GET /boom 500']] },
    ],
  },
}
const LOKI_ERROR = { status: 'error', error: 'parse error at char 1' }
const K8S_PODS = {
  kind: 'PodList',
  items: [
    { metadata: { name: 'nginx-7d9f-x1', namespace: 'prod', creationTimestamp: '2026-09-01T00:00:00Z', labels: { app: 'nginx' } }, status: { phase: 'Running', podIP: '10.244.0.5', nodeName: 'node-1', containerStatuses: [{ ready: true, restartCount: 0 }, { ready: false, restartCount: 3 }] } },
    { metadata: { name: 'cache-0', namespace: 'prod', creationTimestamp: '2026-09-02T00:00:00Z' }, status: { phase: 'Running', podIP: '10.244.0.6', nodeName: 'node-2', containerStatuses: [{ ready: true, restartCount: 0 }] } },
  ],
}
const K8S_EVENTS = {
  kind: 'EventList',
  items: [
    { type: 'Warning', reason: 'BackOff', message: 'Back-off restarting failed container', involvedObject: { kind: 'Pod', name: 'nginx-7d9f-x1', namespace: 'prod' }, lastTimestamp: '2026-09-29T01:00:00Z', count: 7 },
  ],
}
const K8S_DEPLOYMENTS = {
  kind: 'DeploymentList',
  items: [
    { metadata: { name: 'nginx', namespace: 'prod', creationTimestamp: '2026-08-01T00:00:00Z' }, spec: { replicas: 3, template: { spec: { containers: [{ image: 'nginx:1.27' }] } } }, status: { readyReplicas: 2, availableReplicas: 2 } },
  ],
}
const K8S_NODES = {
  kind: 'NodeList',
  items: [
    { metadata: { name: 'node-1' }, status: { conditions: [{ type: 'Ready', status: 'True' }], nodeInfo: { kubeletVersion: 'v1.31.2' }, allocatable: { cpu: '8', memory: '32Gi' }, addresses: [{ type: 'InternalIP', address: '192.168.1.11' }] } },
  ],
}

let lastReq = { am: null, loki: null, k8s: null, dingtalk: null, wecom: null }
let dingTalkSignValid = false

function verifyDingTalkSign(query) {
  const ts = query.get('timestamp')
  const sign = query.get('sign')
  if (!ts || !sign) return false
  const expected = encodeURIComponent(crypto.createHmac('sha256', DT_SECRET).update(`${ts}\n${DT_SECRET}`).digest('base64'))
  return sign === expected
}

// ---------------- 多路桩 ----------------
function startMock() {
  const server = createServer((req, res) => {
    const url = new URL(req.url ?? '/', MOCK)
    const pathName = url.pathname
    const auth = req.headers['authorization'] ?? ''
    let body = ''
    req.on('data', (c) => { body += c })
    req.on('end', () => {
      let parsed = null
      try { parsed = JSON.parse(body) } catch { /* 非 JSON */ }
      const send = (code, obj, raw = null) => {
        res.writeHead(code, { 'Content-Type': 'application/json' })
        res.end(raw ?? JSON.stringify(obj))
      }

      if (pathName.startsWith('/am/api/v2/')) {
        if (auth !== `Bearer ${AM_TOKEN}`) return send(401, { message: 'unauthorized' })
        if (pathName === '/am/api/v2/alerts') {
          lastReq.am = { path: pathName, query: Object.fromEntries(url.searchParams.entries()), auth }
          const state = url.searchParams.get('state')
          return send(200, state ? AM_ALERTS.filter((a) => a.state === state) : AM_ALERTS)
        }
        if (pathName === '/am/api/v2/silences') { lastReq.am = { path: pathName, query: Object.fromEntries(url.searchParams.entries()), auth }; return send(200, AM_SILENCES) }
        if (pathName === '/am/api/v2/status') { lastReq.am = { path: pathName, auth }; return send(200, AM_STATUS) }
        return send(404, { message: `no mock for ${pathName}` })
      }

      if (pathName.startsWith('/loki/api/v1/')) {
        lastReq.loki = { path: pathName, query: Object.fromEntries(url.searchParams.entries()), auth }
        if (pathName === '/loki/api/v1/query_range') return send(200, LOKI_STREAMS)
        if (pathName === '/loki/api/v1/query') return url.searchParams.get('query')?.includes('BAD') ? send(200, LOKI_ERROR) : send(200, LOKI_STREAMS)
        if (pathName === '/loki/api/v1/labels') return send(200, { status: 'success', data: ['job', 'instance'] })
        if (pathName === '/loki/api/v1/series') return send(200, { status: 'success', data: [{ job: 'nginx', instance: 'web-01' }] })
        if (pathName.startsWith('/loki/api/v1/label/')) return send(200, { status: 'success', data: ['nginx', 'gateway'] })
        return send(404, { message: `no mock for ${pathName}` })
      }

      if (pathName === '/api/v1/pods' || pathName === '/api/v1/namespaces/prod/pods') {
        if (auth !== `Bearer ${K8S_TOKEN}`) return send(401, { kind: 'Status', code: 401, message: 'Unauthorized' })
        lastReq.k8s = { path: pathName, query: Object.fromEntries(url.searchParams.entries()), auth }
        return send(200, K8S_PODS)
      }
      if (pathName.startsWith('/api/v1/namespaces/prod/pods/nginx-1/log')) {
        lastReq.k8s = { path: pathName, query: Object.fromEntries(url.searchParams.entries()), auth }
        return send(200, {}, 'nginx-1 log line 1\nnginx-1 log line 2\n')
      }
      if (pathName === '/api/v1/events' || pathName === '/api/v1/namespaces/prod/events') { lastReq.k8s = { path: pathName, query: Object.fromEntries(url.searchParams.entries()), auth }; return send(200, K8S_EVENTS) }
      if (pathName === '/apis/apps/v1/deployments' || pathName === '/apis/apps/v1/namespaces/prod/deployments') { lastReq.k8s = { path: pathName, query: Object.fromEntries(url.searchParams.entries()), auth }; return send(200, K8S_DEPLOYMENTS) }
      if (pathName === '/api/v1/nodes') { lastReq.k8s = { path: pathName, auth }; return send(200, K8S_NODES) }
      if (pathName === '/api/v1/namespaces/forbidden/pods') return send(403, { kind: 'Status', code: 403, message: 'pods is forbidden' })

      if (pathName === '/robot/send') {
        const token = url.searchParams.get('access_token')
        const okSign = verifyDingTalkSign(url.searchParams)
        lastReq.dingtalk = { path: pathName, query: Object.fromEntries(url.searchParams.entries()), body: parsed, signValid: okSign }
        if (token !== DT_TOKEN) return send(200, { errcode: 310000, errmsg: 'sign match fail or not exist' })
        if (!okSign) return send(200, { errcode: 310000, errmsg: 'sign match fail' })
        return send(200, { errcode: 0, errmsg: 'ok' })
      }

      if (pathName === '/cgi-bin/webhook/send') {
        const key = url.searchParams.get('key')
        lastReq.wecom = { query: Object.fromEntries(url.searchParams.entries()), body: parsed }
        if (key !== WC_KEY) return send(200, { errcode: 93000, errmsg: 'invalid webhook url' })
        return send(200, { errcode: 0, errmsg: 'ok' })
      }

      return send(404, { message: `no mock for ${pathName}` })
    })
  })
  return new Promise((resolve) => server.listen(MOCK_PORT, '127.0.0.1', () => resolve(server)))
}

// ---------------- 后端 ----------------
function startBackend() {
  const dll = path.join(REPO_ROOT, '.builds', 'ops-p0', 'MoAI.dll')
  if (!fs.existsSync(dll)) {
    console.log('WARN | 未找到 .builds/ops-p0/MoAI.dll，回退 dotnet run（默认 bin/Debug；若后端正被运行会因 DLL 锁失败）')
    return startBackendDotnetRun()
  }
  const child = spawn('dotnet', [dll], {
    cwd: path.join(REPO_ROOT, 'src', 'MoAI'),
    env: {
      ...process.env,
      ASPNETCORE_ENVIRONMENT: 'Development',
      MoAI__Port: String(BACKEND_PORT),
      MoAI__DingTalk__RobotEndpoint: MOCK,
      MoAI__WeixinWork__RobotEndpoint: MOCK,
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
      MoAI__DingTalk__RobotEndpoint: MOCK,
      MoAI__WeixinWork__RobotEndpoint: MOCK,
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
  console.log(`多路桩已就绪：${MOCK}`)
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
    const AM = `ops0_am_${TS}`
    const AM_BAD = `ops0_am_bad_${TS}`
    const LOKI = `ops0_loki_${TS}`
    const K8S = `ops0_k8s_${TS}`
    const K8S_FORBIDDEN = `ops0_k8s_forb_${TS}`
    const DT = `ops0_dt_${TS}`
    const DT_BAD = `ops0_dt_bad_${TS}`
    const WC = `ops0_wc_${TS}`
    const WC_BAD = `ops0_wc_bad_${TS}`

    // ---------- S58 alertmanager_query ----------
    check('S58a 创建 Alertmanager 实例（子路径 + Bearer）', await saveInstance(admin, AM, 'alertmanager_query', 'AM-ok', { BaseUrl: `${MOCK}/am`, BearerToken: AM_TOKEN, TimeoutSeconds: 10, MaxListItems: 100 }))
    const alerts = await run(admin, AM, JSON.stringify({ Mode: 'alerts', State: 'active' }))
    const diskAlert = (alerts.data?.Alerts ?? []).find((a) => a.Labels?.alertname === 'HighDiskUsage')
    check('S58b alerts 解析（标签/注解/状态/时间）+ 状态过滤下发', alerts.json?.success === true && (alerts.data?.Alerts ?? []).length === 1 && diskAlert?.Annotations?.summary === '磁盘使用率 95%' && diskAlert?.State === 'active' && /^\d{4}-\d{2}-\d{2}T/.test(diskAlert?.StartsAt ?? '') && lastReq.am?.query?.state === 'active' && lastReq.am?.path === '/am/api/v2/alerts' && lastReq.am?.auth === `Bearer ${AM_TOKEN}`, JSON.stringify({ q: lastReq.am?.query, data: alerts.data, error: alerts.json?.error }))
    const silences = await run(admin, AM, JSON.stringify({ Mode: 'silences' }))
    check('S58c silences 解析（正则匹配器加 ~ 前缀）', silences.json?.success === true && silences.data?.Silences?.[0]?.Id === 'silence-1' && silences.data.Silences[0].State === 'active' && silences.data.Silences[0].Matchers?.instance === '~web-0.*' && silences.data.Silences[0].CreatedBy === 'ops-admin', JSON.stringify(silences.data ?? silences.json?.error ?? {}))
    const status = await run(admin, AM, JSON.stringify({ Mode: 'status' }))
    check('S58d status 解析（版本/集群状态/成员）', status.json?.success === true && status.data?.Status?.Version === '0.27.0' && status.data?.Status?.ClusterStatus === 'ready' && (status.data?.Status?.PeerNames ?? []).length === 2, JSON.stringify(status.data ?? status.json?.error ?? {}))
    check('S58e 创建 Alertmanager 实例（错误令牌）', await saveInstance(admin, AM_BAD, 'alertmanager_query', 'AM-bad', { BaseUrl: `${MOCK}/am`, BearerToken: 'am-wrong', TimeoutSeconds: 10, MaxListItems: 100 }))
    const amBad = await run(admin, AM_BAD, JSON.stringify({ Mode: 'alerts' }))
    check('S58f 上游 401 归一为可读失败', amBad.json?.success === false && /HTTP 401/.test(amBad.json?.error ?? ''), `${amBad.json?.error?.slice(0, 160)}`)

    // ---------- S59 loki_query ----------
    check('S59a 创建 Loki 实例（子路径 + Basic）', await saveInstance(admin, LOKI, 'loki_query', 'Loki-ok', { BaseUrl: `${MOCK}/loki`, Username: 'loki-user', Password: 'loki-pass', TimeoutSeconds: 10, MaxLines: 200, MaxLineChars: 1024, MaxListItems: 200 }))
    const lokiRange = await run(admin, LOKI, JSON.stringify({ Mode: 'query_range', Query: '{job="nginx"} |= "500"', Start: '2026-09-29T00:00:00Z', Direction: 'backward' }))
    const firstStream = lokiRange.data?.Streams?.[0]
    check('S59b query_range 时间归一（RFC3339→纳秒）+ Basic 头 + 日志流解析', lokiRange.json?.success === true && lastReq.loki?.path === '/loki/api/v1/query_range' && lastReq.loki?.query?.start === '1790640000000000000' && lastReq.loki?.query?.limit === '200' && lastReq.loki?.auth?.startsWith('Basic ') && firstStream?.Labels?.job === 'nginx' && firstStream?.Lines?.[1]?.Text?.includes('/boom') && firstStream?.Lines?.[0]?.Timestamp === '2026-09-29T00:00:00Z', JSON.stringify({ q: lastReq.loki?.query, data: lokiRange.data, error: lokiRange.json?.error }))
    const lokiLabels = await run(admin, LOKI, JSON.stringify({ Mode: 'labels' }))
    check('S59c labels 标签名列表', lokiLabels.json?.success === true && (lokiLabels.data?.Labels ?? []).join(',') === 'job,instance', JSON.stringify(lokiLabels.data ?? lokiLabels.json?.error ?? {}))
    const lokiValues = await run(admin, LOKI, JSON.stringify({ Mode: 'label_values', Label: 'job' }))
    check('S59d label_values 路径与取值', lokiValues.json?.success === true && lastReq.loki?.path === '/loki/api/v1/label/job/values' && (lokiValues.data?.LabelValues ?? []).join(',') === 'nginx,gateway', JSON.stringify({ path: lastReq.loki?.path, data: lokiValues.data }))
    const lokiSeries = await run(admin, LOKI, JSON.stringify({ Mode: 'series', Selector: '{job="nginx"}' }))
    check('S59e series 标签集（match[] 下发）', lokiSeries.json?.success === true && lastReq.loki?.query?.['match[]'] === '{job="nginx"}' && (lokiSeries.data?.Series ?? []).length === 1 && lokiSeries.data.Series[0].job === 'nginx', JSON.stringify({ q: lastReq.loki?.query, data: lokiSeries.data }))
    const lokiBad = await run(admin, LOKI, JSON.stringify({ Mode: 'query', Query: 'BAD' }))
    check('S59f status=error 信封归一', lokiBad.json?.success === false && /Loki 查询失败/.test(lokiBad.json?.error ?? '') && /parse error/.test(lokiBad.json?.error ?? ''), `${lokiBad.json?.error?.slice(0, 160)}`)

    // ---------- S60 kubernetes_query ----------
    check('S60a 创建 K8s 实例', await saveInstance(admin, K8S, 'kubernetes_query', 'K8s-ok', { BaseUrl: MOCK, Token: K8S_TOKEN, SkipTlsVerify: true, TimeoutSeconds: 10, MaxListItems: 100, MaxLogChars: 16384 }))
    const pods = await run(admin, K8S, JSON.stringify({ Mode: 'pods', Namespace: 'prod', LabelSelector: 'app=nginx' }))
    const nginxPod = (pods.data?.Pods ?? []).find((p) => p.Name === 'nginx-7d9f-x1')
    check('S60b pods 解析（阶段/IP/节点/就绪/重启）+ 命名空间路径与选择器', pods.json?.success === true && (pods.data?.Pods ?? []).length === 2 && nginxPod?.Phase === 'Running' && nginxPod?.ReadyContainers === '1/2' && nginxPod?.RestartCount === 3 && nginxPod?.PodIp === '10.244.0.5' && lastReq.k8s?.path === '/api/v1/namespaces/prod/pods' && lastReq.k8s?.query?.labelSelector === 'app=nginx' && lastReq.k8s?.auth === `Bearer ${K8S_TOKEN}`, JSON.stringify({ path: lastReq.k8s?.path, q: lastReq.k8s?.query, data: pods.data, error: pods.json?.error }))
    const logs = await run(admin, K8S, JSON.stringify({ Mode: 'pod_logs', Namespace: 'prod', Pod: 'nginx-1', Container: 'nginx', TailLines: 50 }))
    check('S60c pod_logs 纯文本透传 + 尾行/容器参数', logs.json?.success === true && (logs.data?.LogText ?? '').includes('log line 1') && logs.data?.Container === 'nginx' && lastReq.k8s?.query?.tailLines === '50' && lastReq.k8s?.query?.container === 'nginx', JSON.stringify({ data: logs.data, q: lastReq.k8s?.query }))
    const events = await run(admin, K8S, JSON.stringify({ Mode: 'events', Namespace: 'prod' }))
    check('S60d events 解析（Warning/原因/关联对象/次数）', events.json?.success === true && events.data?.Events?.[0]?.Type === 'Warning' && events.data.Events[0].Reason === 'BackOff' && events.data.Events[0].ObjectName === 'nginx-7d9f-x1' && events.data.Events[0].Count === 7, JSON.stringify(events.data ?? events.json?.error ?? {}))
    const deployments = await run(admin, K8S, JSON.stringify({ Mode: 'deployments', Namespace: 'prod' }))
    check('S60e deployments 解析（副本/镜像）', deployments.json?.success === true && deployments.data?.Deployments?.[0]?.Replicas === 3 && deployments.data.Deployments[0].ReadyReplicas === 2 && deployments.data.Deployments[0].Images?.[0] === 'nginx:1.27', JSON.stringify(deployments.data ?? deployments.json?.error ?? {}))
    const nodes = await run(admin, K8S, JSON.stringify({ Mode: 'nodes' }))
    check('S60f nodes 解析（Ready/版本/IP/可分配）', nodes.json?.success === true && nodes.data?.Nodes?.[0]?.Ready === true && nodes.data.Nodes[0].KubeletVersion === 'v1.31.2' && nodes.data.Nodes[0].InternalIp === '192.168.1.11', JSON.stringify(nodes.data ?? nodes.json?.error ?? {}))
    check('S60g 创建 K8s 实例（无权限令牌）', await saveInstance(admin, K8S_FORBIDDEN, 'kubernetes_query', 'K8s-forbidden', { BaseUrl: MOCK, Token: 'k8s-wrong', SkipTlsVerify: true, TimeoutSeconds: 10, MaxListItems: 100, MaxLogChars: 16384 }))
    const k8sForbidden = await run(admin, K8S_FORBIDDEN, JSON.stringify({ Mode: 'pods', Namespace: 'forbidden' }))
    check('S60h 403 归一为权限提示', k8sForbidden.json?.success === false && /HTTP 403/.test(k8sForbidden.json?.error ?? '') && /权限不足/.test(k8sForbidden.json?.error ?? ''), `${k8sForbidden.json?.error?.slice(0, 160)}`)

    // ---------- S61 dingtalk_webhook_text ----------
    check('S61a 创建钉钉实例（加签）', await saveInstance(admin, DT, 'dingtalk_webhook_text', 'DT-ok', { WebhookKey: `https://oapi.dingtalk.com/robot/send?access_token=${DT_TOKEN}`, Secret: DT_SECRET }))
    const dt = await run(admin, DT, JSON.stringify({ Text: 'MoAI 提醒：磁盘使用率 95%', AtMobiles: '13800000000,13900000000' }))
    check('S61b 推送成功（HMAC 加签被桩验证 + @ 手机号下发）', dt.json?.success === true && dt.data?.Errcode === 0 && dt.data?.Text === 'MoAI 提醒：磁盘使用率 95%' && lastReq.dingtalk?.signValid === true && lastReq.dingtalk?.query?.access_token === DT_TOKEN && lastReq.dingtalk?.body?.msgtype === 'text' && lastReq.dingtalk?.body?.text?.content === 'MoAI 提醒：磁盘使用率 95%' && (lastReq.dingtalk?.body?.at?.atMobiles ?? []).length === 2, JSON.stringify({ q: lastReq.dingtalk?.query, signValid: lastReq.dingtalk?.signValid, error: dt.json?.error }))
    const dtAtAll = await run(admin, DT, JSON.stringify({ Text: '@所有人巡检', AtAll: true }))
    check('S61c @所有人下发', dtAtAll.json?.success === true && lastReq.dingtalk?.body?.at?.isAtAll === true, JSON.stringify(lastReq.dingtalk?.body ?? {}))
    check('S61d 创建钉钉实例（错误 token）', await saveInstance(admin, DT_BAD, 'dingtalk_webhook_text', 'DT-bad', { WebhookKey: 'dt-wrong-token', Secret: DT_SECRET }))
    const dtBad = await run(admin, DT_BAD, JSON.stringify({ Text: '测试' }))
    check('S61e errcode!=0 归一为安全设置提示', dtBad.json?.success === false && /310000/.test(dtBad.json?.error ?? '') && /关键词|加签|白名单/.test(dtBad.json?.error ?? ''), `${dtBad.json?.error?.slice(0, 160)}`)

    // ---------- S62 wecom_webhook_text ----------
    check('S62a 创建企微实例', await saveInstance(admin, WC, 'wecom_webhook_text', 'WC-ok', { WebhookKey: `https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key=${WC_KEY}` }))
    const wc = await run(admin, WC, JSON.stringify({ Text: 'MoAI 提醒：生产 Warning 告警', AtMobiles: '13800000000' }))
    check('S62b 推送成功（key 归一 + mentioned_mobile_list）', wc.json?.success === true && wc.data?.Errcode === 0 && lastReq.wecom?.query?.key === WC_KEY && lastReq.wecom?.body?.text?.content === 'MoAI 提醒：生产 Warning 告警' && (lastReq.wecom?.body?.text?.mentioned_mobile_list ?? []).join(',') === '13800000000', JSON.stringify({ body: lastReq.wecom?.body, error: wc.json?.error }))
    const wcAtAll = await run(admin, WC, JSON.stringify({ Text: '@所有人', AtAll: true }))
    check('S62c @所有人下发（mentioned_list）', wcAtAll.json?.success === true && (lastReq.wecom?.body?.text?.mentioned_list ?? []).join(',') === '@all', JSON.stringify(lastReq.wecom?.body ?? {}))
    check('S62d 创建企微实例（错误 key）', await saveInstance(admin, WC_BAD, 'wecom_webhook_text', 'WC-bad', { WebhookKey: 'wc-wrong-key' }))
    const wcBad = await run(admin, WC_BAD, JSON.stringify({ Text: '测试' }))
    check('S62e errcode!=0 归一', wcBad.json?.success === false && /93000/.test(wcBad.json?.error ?? ''), `${wcBad.json?.error?.slice(0, 160)}`)

    // ---------- 清理 ----------
    for (const key of [AM, AM_BAD, LOKI, K8S, K8S_FORBIDDEN, DT, DT_BAD, WC, WC_BAD]) {
      await del(admin, key)
    }

    console.log(`\n=== P0 运维插件 E2E: PASS ${PASS} / FAIL ${FAIL} / SKIP ${SKIP} ===`)
    if (FAIL > 0) process.exitCode = 1
  } finally {
    if (process.env.OPP0_KEEP_BACKEND === '1') {
      console.log(`OPP0_KEEP_BACKEND=1，保留后端 pid=${backend.pid}`)
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
