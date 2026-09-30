// P2 智能运维插件（ssh_executor / grafana_query / sqlserver_query）端到端
// 做法：本地起一个 Grafana HTTP 桩 → 拉起一个独立后端 → 创建实例并运行 →
//       ssh_executor 的守卫用例（校验先于连接）与 sqlserver_query 的守卫用例无条件运行；
//       SSH 真机执行（SSH_E2E_CONNECTION="host,port,user,password"）与 SQL Server 真库
//       （SQLSERVER_E2E_CONNECTION="host,port,db,user,password"）按环境变量门控，未设置时 SKIP。
// 前置：宿主已按独立输出目录构建（默认 bin/Debug 可能被运行中的后端锁定）：
//       dotnet build src/MoAI/MoAI.csproj -o .builds/ops-p2
// 用法：node local-dev/ops-p2-plugins-e2e.mjs
// 环境变量：OPP2_BACKEND_PORT（默认 5190）｜OPP2_MOCK_PORT（默认 5189）｜OPP2_KEEP_BACKEND=1 保留后端供排查
//           ｜SSH_E2E_CONNECTION｜SQLSERVER_E2E_CONNECTION（可选，真实链路验证）
import { createServer } from 'node:http'
import { spawn } from 'node:child_process'
import crypto from 'node:crypto'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const REPO_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const BACKEND_PORT = Number(process.env.OPP2_BACKEND_PORT ?? 5190)
const MOCK_PORT = Number(process.env.OPP2_MOCK_PORT ?? 5189)
const BASE = `http://127.0.0.1:${BACKEND_PORT}`
const MOCK = `http://127.0.0.1:${MOCK_PORT}`
const GRAFANA_TOKEN = 'glsa-e2e-token'

let PASS = 0, FAIL = 0, SKIP = 0
const check = (name, cond, detail = '') => {
  if (cond) { PASS++; console.log(`PASS | ${name}`) }
  else { FAIL++; console.log(`FAIL | ${name} ${detail ? '— ' + detail : ''}`) }
}
const skip = (name, why) => { SKIP++; console.log(`SKIP | ${name} — ${why}`) }

const sleep = (ms) => new Promise((r) => setTimeout(r, ms))

// ---------------- Grafana 桩（health / annotations / search，Bearer 令牌鉴权） ----------------
const GRAFANA_ANNOTATIONS = [
  { id: 7, text: 'deploy nginx v2.1', tags: ['deploy'], time: 1790640000000, timeEnd: 1790640060000, dashboardId: 12, panelId: 3 },
  { id: 8, text: 'alert: cpu high on web-01', tags: ['alerting', 'web-01'], time: 1790643600000, timeEnd: 0, dashboardId: 13, panelId: 0 },
]
const GRAFANA_DASHBOARDS = [
  { id: 12, uid: 'ngi-abc', title: 'Nginx 概览', url: '/d/ngi-abc/nginx', type: 'dash-db' },
  { id: 13, uid: 'sys-xyz', title: '主机巡检', url: '/d/sys-xyz/hosts', type: 'dash-db' },
]
let lastGrafana = null

function startMock() {
  const server = createServer((req, res) => {
    const url = new URL(req.url ?? '/', MOCK)
    const pathName = url.pathname
    const auth = req.headers['authorization'] ?? ''
    lastGrafana = { path: pathName, query: Object.fromEntries(url.searchParams.entries()), tagValues: url.searchParams.getAll('tags'), auth }
    const send = (code, obj) => {
      res.writeHead(code, { 'Content-Type': 'application/json' })
      res.end(JSON.stringify(obj))
    }

    if (pathName === '/grafana/api/health') return send(200, { commit: 'abc1234', database: 'ok', version: '11.2.0' })
    if (pathName === '/grafana/api/annotations' || pathName === '/grafana/api/search') {
      if (auth !== `Bearer ${GRAFANA_TOKEN}`) return send(401, { message: 'invalid API token' })
      if (pathName.endsWith('annotations')) return send(200, GRAFANA_ANNOTATIONS)
      return send(200, GRAFANA_DASHBOARDS)
    }
    return send(404, { message: `no mock for ${pathName}` })
  })
  return new Promise((resolve) => server.listen(MOCK_PORT, '127.0.0.1', () => resolve(server)))
}

// ---------------- 后端：优先独立输出目录 DLL，否则回退 dotnet run ----------------
function startBackend() {
  const dll = path.join(REPO_ROOT, '.builds', 'ops-p2', 'MoAI.dll')
  if (!fs.existsSync(dll)) {
    console.log('WARN | 未找到 .builds/ops-p2/MoAI.dll，回退 dotnet run（默认 bin/Debug；若后端正被运行会因 DLL 锁失败）')
    return startBackendDotnetRun()
  }
  const child = spawn('dotnet', [dll], {
    cwd: path.join(REPO_ROOT, 'src', 'MoAI'),
    env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development', MoAI__Port: String(BACKEND_PORT) },
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
    env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development', MoAI__Port: String(BACKEND_PORT) },
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
  console.log(`Grafana 桩已就绪：${MOCK}`)
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
    const SSH_OK = `ops2_ssh_ok_${TS}`
    const SSH_REBOOT = `ops2_ssh_rbt_${TS}`
    const SSH_EMPTY = `ops2_ssh_emp_${TS}`
    const GRAFANA = `ops2_grafana_${TS}`
    const GRAFANA_BAD = `ops2_grafana_bad_${TS}`
    const MSSQL = `ops2_mssql_${TS}`
    const MSSQL_BADPORT = `ops2_mssql_p0_${TS}`

    // ---------- 注册表 ----------
    const list = await api('GET', '/api/ai/plugin', { token: admin })
    const items = list.json?.items ?? list.json ?? []
    for (const [tplKey, configTypeKey] of [['ssh_executor', 'SshExecutorConfig'], ['grafana_query', 'GrafanaQueryConfig'], ['sqlserver_query', 'SqlServerQueryConfig']]) {
      const tpl = items.find((x) => x.key === tplKey)
      check(`注册表含 ${tplKey} 且为动态模板`, Boolean(tpl) && tpl.isDynamic === true, list.text.slice(0, 200))
      check(`${tplKey} 配置类型已解析`, (tpl?.configType ?? '').includes(configTypeKey), tpl?.configType ?? '')
    }

    // ---------- S54 ssh_executor：白名单守卫（校验先于连接） ----------
    check('S54a 创建 SSH 实例（正常白名单）', await saveInstance(admin, SSH_OK, 'ssh_executor', 'SSH-守卫', { Host: '127.0.0.1', Port: 22, Username: 'ops', Password: 'x', CommandWhitelist: 'systemctl status,journalctl,df,free,uptime,tail,ps,grep,rm -rf /tmp', CommandTimeoutSeconds: 5, MaxOutputChars: 8192 }))
    check('S54b 创建 SSH 实例（白名单含 reboot/mkfs/rm）', await saveInstance(admin, SSH_REBOOT, 'ssh_executor', 'SSH-黑名单', { Host: '127.0.0.1', Port: 22, Username: 'ops', Password: 'x', CommandWhitelist: 'reboot,mkfs.ext4,rm,uptime', CommandTimeoutSeconds: 5, MaxOutputChars: 8192 }))
    check('S54c 创建 SSH 实例（空白名单）', await saveInstance(admin, SSH_EMPTY, 'ssh_executor', 'SSH-空白名单', { Host: '127.0.0.1', Port: 22, Username: 'ops', Password: 'x', CommandWhitelist: '', CommandTimeoutSeconds: 5, MaxOutputChars: 8192 }))

    const empty = await run(admin, SSH_EMPTY, JSON.stringify({ Command: 'uptime' }))
    check('S54d 空白名单 fail-closed（拒绝一切命令）', empty.json?.success === false && /白名单/.test(empty.json?.error ?? ''), `${empty.json?.error?.slice(0, 120)}`)
    const chained = await run(admin, SSH_OK, JSON.stringify({ Command: 'df -h; rm -rf /' }))
    check('S54e 拼接/替换符号拒绝（; rm -rf /）', chained.json?.success === false && /拼接\/替换符号/.test(chained.json?.error ?? ''), `${chained.json?.error?.slice(0, 120)}`)
    const chainedBacktick = await run(admin, SSH_OK, JSON.stringify({ Command: 'echo `whoami`' }))
    check('S54f 命令替换拒绝（反引号）', chainedBacktick.json?.success === false && /拼接\/替换符号/.test(chainedBacktick.json?.error ?? ''), `${chainedBacktick.json?.error?.slice(0, 120)}`)
    const unknown = await run(admin, SSH_OK, JSON.stringify({ Command: 'docker ps' }))
    check('S54g 未命中白名单拒绝', unknown.json?.success === false && /未命中/.test(unknown.json?.error ?? '') && /docker ps/.test(unknown.json?.error ?? ''), `${unknown.json?.error?.slice(0, 120)}`)
    const reboot = await run(admin, SSH_REBOOT, JSON.stringify({ Command: 'reboot' }))
    check('S54h 灾难级黑名单压过白名单（reboot）', reboot.json?.success === false && /灾难级黑名单/.test(reboot.json?.error ?? ''), `${reboot.json?.error?.slice(0, 120)}`)
    const mkfs = await run(admin, SSH_REBOOT, JSON.stringify({ Command: 'mkfs.ext4 /dev/sda1' }))
    check('S54i 灾难级黑名单（mkfs.* 前缀）', mkfs.json?.success === false && /灾难级黑名单/.test(mkfs.json?.error ?? ''), `${mkfs.json?.error?.slice(0, 120)}`)
    const rmRoot = await run(admin, SSH_REBOOT, JSON.stringify({ Command: 'rm -rf /' }))
    check('S54j 灾难级递归删除拒绝（rm -rf /，白名单含 rm）', rmRoot.json?.success === false && /灾难级递归删除/.test(rmRoot.json?.error ?? ''), `${rmRoot.json?.error?.slice(0, 120)}`)
    const rmTmp = await run(admin, SSH_REBOOT, JSON.stringify({ Command: 'rm -rf /tmp/ops2-cache' }))
    check('S54k 守卫放行后进入连接阶段（普通 rm → 连接失败而非 400）', rmTmp.json?.success === false && /连接失败|超时/.test(rmTmp.json?.error ?? '') && !/白名单|灾难级/.test(rmTmp.json?.error ?? ''), `${rmTmp.json?.error?.slice(0, 160)}`)
    const uptimeConn = await run(admin, SSH_OK, JSON.stringify({ Command: 'uptime' }))
    check('S54l 正常命令连接失败归一（无 SSH 服务可达）', uptimeConn.json?.success === false && /SSH 连接失败|连接超时/.test(uptimeConn.json?.error ?? ''), `${uptimeConn.json?.error?.slice(0, 160)}`)

    if (process.env.SSH_E2E_CONNECTION) {
      const [host, port, user, pass] = process.env.SSH_E2E_CONNECTION.split(',')
      const SSH_REAL = `ops2_ssh_real_${TS}`
      await saveInstance(admin, SSH_REAL, 'ssh_executor', 'SSH-真机', { Host: host.trim(), Port: Number(port), Username: user.trim(), Password: pass.trim(), CommandWhitelist: 'uptime,df,free', CommandTimeoutSeconds: 15, MaxOutputChars: 8192 })
      const real = await run(admin, SSH_REAL, JSON.stringify({ Command: 'uptime' }))
      check('S54m 真机执行 uptime（退出码 0 + 输出非空）', real.json?.success === true && real.data?.ExitStatus === 0 && (real.data?.Output ?? '').length > 0 && real.data?.Ok === true, JSON.stringify(real.data ?? real.json?.error ?? {}))
      await del(admin, SSH_REAL)
    } else {
      skip('S54m 真机执行 uptime', '未设置 SSH_E2E_CONNECTION（host,port,user,password）')
    }

    // ---------- S55 grafana_query：health / annotations / search ----------
    check('S55a 创建 Grafana 实例（子路径 + 服务账号令牌）', await saveInstance(admin, GRAFANA, 'grafana_query', 'Grafana-ok', { BaseUrl: `${MOCK}/grafana`, Token: GRAFANA_TOKEN, TimeoutSeconds: 10, MaxListItems: 100 }))
    const health = await run(admin, GRAFANA, JSON.stringify({ Mode: 'health' }))
    check('S55b health 版本解析 + Bearer 头 + 子路径命中', health.json?.success === true && health.data?.Health?.version === '11.2.0' && health.data?.Health?.database === 'ok' && lastGrafana?.path === '/grafana/api/health' && lastGrafana?.auth === `Bearer ${GRAFANA_TOKEN}`, JSON.stringify({ data: health.data, lastGrafana }))

    const annotations = await run(admin, GRAFANA, JSON.stringify({ Mode: 'annotations', Tags: 'alerting,deploy', From: '2026-09-29T00:00:00Z' }))
    const deployed = (annotations.data?.Annotations ?? []).find((a) => a.Id === 7)
    check('S55c annotations 时间归一（RFC3339→毫秒）+ 标签逐个下发 + 解析', annotations.json?.success === true
      && lastGrafana?.query?.from === '1790640000000'
      && (lastGrafana?.tagValues ?? []).join(',') === 'alerting,deploy'
      && deployed?.Text === 'deploy nginx v2.1'
      && /^\d{4}-\d{2}-\d{2}T/.test(deployed?.TimeFrom ?? '')
      && deployed?.TimeTo === '2026-09-29T00:01:00Z', JSON.stringify({ q: lastGrafana?.query, tags: lastGrafana?.tagValues, data: annotations.data }))

    const search = await run(admin, GRAFANA, JSON.stringify({ Mode: 'search', Query: 'nginx' }))
    check('S55d search 仪表板解析（type=dash-db + query 透传）', search.json?.success === true && (search.data?.Dashboards ?? []).length === 2 && search.data.Dashboards[0].Title === 'Nginx 概览' && search.data.Dashboards[0].Uid === 'ngi-abc' && lastGrafana?.query?.type === 'dash-db' && lastGrafana?.query?.query === 'nginx' && lastGrafana?.query?.limit === '100', JSON.stringify({ q: lastGrafana?.query, data: search.data }))

    check('S55e 创建 Grafana 实例（错误令牌）', await saveInstance(admin, GRAFANA_BAD, 'grafana_query', 'Grafana-bad', { BaseUrl: `${MOCK}/grafana`, Token: 'glsa-wrong', TimeoutSeconds: 10, MaxListItems: 100 }))
    const grafanaBad = await run(admin, GRAFANA_BAD, JSON.stringify({ Mode: 'search' }))
    check('S55f 上游 401 归一为可读失败', grafanaBad.json?.success === false && /HTTP 401/.test(grafanaBad.json?.error ?? '') && /invalid API token/.test(grafanaBad.json?.error ?? ''), `${grafanaBad.json?.error?.slice(0, 160)}`)

    // ---------- S56 sqlserver_query：守卫先于连接 + 连接失败归一 ----------
    check('S56a 创建 SQL Server 实例（正常配置）', await saveInstance(admin, MSSQL, 'sqlserver_query', 'MSSQL-ok', { Host: '127.0.0.1', Port: 1433, Username: 'reader', Password: 'x', MaxRows: 100, CommandTimeoutSeconds: 5, TrustServerCertificate: true }))
    check('S56b 创建 SQL Server 实例（Port 0）', await saveInstance(admin, MSSQL_BADPORT, 'sqlserver_query', 'MSSQL-badport', { Host: '127.0.0.1', Port: 0, Username: 'reader', Password: 'x', MaxRows: 100, CommandTimeoutSeconds: 5 }))
    const badPort = await run(admin, MSSQL_BADPORT, JSON.stringify({ Sql: 'SELECT 1' }))
    check('S56c InitAsync 端口校验（1-65535）', badPort.json?.success === false && /1-65535/.test(badPort.json?.error ?? ''), `${badPort.json?.error?.slice(0, 160)}`)
    const write = await run(admin, MSSQL, JSON.stringify({ Sql: 'DELETE FROM dbo.demo' }))
    check('S56d 写 SQL 先于连接被拒', write.json?.success === false && /只读/.test(write.json?.error ?? ''), `${write.json?.error?.slice(0, 160)}`)
    const multi = await run(admin, MSSQL, JSON.stringify({ Sql: 'SELECT 1; SELECT 2' }))
    check('S56e 多条语句拒绝', multi.json?.success === false && /只读/.test(multi.json?.error ?? ''), `${multi.json?.error?.slice(0, 160)}`)
    const connFail = await run(admin, MSSQL, JSON.stringify({ Sql: 'SELECT 1' }))
    check('S56f 连接失败归一（无 SQL Server 可达）', connFail.json?.success === false && /数据库连接失败/.test(connFail.json?.error ?? ''), `${connFail.json?.error?.slice(0, 160)}`)

    if (process.env.SQLSERVER_E2E_CONNECTION) {
      const [host, port, db, user, pass] = process.env.SQLSERVER_E2E_CONNECTION.split(',')
      const MSSQL_REAL = `ops2_mssql_real_${TS}`
      await saveInstance(admin, MSSQL_REAL, 'sqlserver_query', 'MSSQL-真库', { Host: host.trim(), Port: Number(port), Database: db.trim(), Username: user.trim(), Password: pass.trim(), MaxRows: 10, CommandTimeoutSeconds: 15, TrustServerCertificate: true })
      const real = await run(admin, MSSQL_REAL, JSON.stringify({ Sql: 'SELECT @@VERSION AS v' }))
      check('S56g 真库查询 @@VERSION（列名+行数据）', real.json?.success === true && real.data?.RowCount === 1 && real.data?.Columns?.[0] === 'v' && (real.data?.Rows?.[0]?.v ?? '').length > 0, JSON.stringify(real.data ?? real.json?.error ?? {}))
      await del(admin, MSSQL_REAL)
    } else {
      skip('S56g 真库查询 @@VERSION', '未设置 SQLSERVER_E2E_CONNECTION（host,port,db,user,password）')
    }

    // ---------- 清理 ----------
    for (const key of [SSH_OK, SSH_REBOOT, SSH_EMPTY, GRAFANA, GRAFANA_BAD, MSSQL, MSSQL_BADPORT]) {
      await del(admin, key)
    }

    console.log(`\n=== P2 运维插件 E2E: PASS ${PASS} / FAIL ${FAIL} / SKIP ${SKIP} ===`)
    if (FAIL > 0) process.exitCode = 1
  } finally {
    if (process.env.OPP2_KEEP_BACKEND === '1') {
      console.log(`OPP2_KEEP_BACKEND=1，保留后端 pid=${backend.pid}`)
      // 保留后端时必须断开 stdio 管道并 unref，否则本脚本进程因子进程管道挂住不退出
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
