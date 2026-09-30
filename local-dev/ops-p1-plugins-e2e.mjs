// P1 智能运维插件（http_probe / zabbix_query / redis_query）桩服务端到端（场景 @DYN-S50 / @DYN-S51 / @DYN-S52）
// 做法：本地起一个 HTTP 桩（拨测目标 + Zabbix JSON-RPC）和一个 Redis RESP2 桩 → 拉起一个独立后端 →
//       创建七个动态插件实例并运行 → 断言「实例 → 注册表模板 → 客户端 → 桩服务 → 响应解析」整条链路，
//       覆盖拨测内网防护、Zabbix 两种鉴权形态与子路径部署、Redis 只读诊断命令与错误归一，无需任何真实运维系统。
// 前置：宿主已按独立输出目录构建（默认 bin/Debug 可能被运行中的后端锁定）：
//       dotnet build src/MoAI/MoAI.csproj -o .builds/ops-p1
// 用法：node local-dev/ops-p1-plugins-e2e.mjs
// 环境变量：OPP_BACKEND_PORT（默认 5193）｜OPP_MOCK_PORT（默认 5192）｜OPP_REDIS_PORT（默认 5191）｜OPP_KEEP_BACKEND=1 保留后端供排查
import { createServer } from 'node:http'
import { spawn } from 'node:child_process'
import net from 'node:net'
import fs from 'node:fs'
import crypto from 'node:crypto'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const REPO_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const BACKEND_PORT = Number(process.env.OPP_BACKEND_PORT ?? 5193)
const MOCK_PORT = Number(process.env.OPP_MOCK_PORT ?? 5192)
const REDIS_PORT = Number(process.env.OPP_REDIS_PORT ?? 5191)
const BASE = `http://127.0.0.1:${BACKEND_PORT}`
const MOCK = `http://127.0.0.1:${MOCK_PORT}`
const MOCK_BASE_URL = `http://127.0.0.1:${MOCK_PORT}`
const ZBX_SESSION = 'stub-session-001'
const ZBX_TOKEN = 'stub-api-token'
const REDIS_PASSWORD = 'opspass'
const CLOSED_PORT = REDIS_PORT + 50

let PASS = 0, FAIL = 0
const check = (name, cond, detail = '') => {
  if (cond) { PASS++; console.log(`PASS | ${name}`) }
  else { FAIL++; console.log(`FAIL | ${name} ${detail ? '— ' + detail : ''}`) }
}

const NOW = Math.floor(Date.now() / 1000)

// ---------------- Zabbix JSON-RPC 桩数据（故意混用字符串/数字，覆盖宽松解析） ----------------
const ZBX_PROBLEMS = [
  { eventid: '502', objectid: '902', source: 0, object: 0, clock: NOW - 2 * 3600, ns: 0, severity: 2, name: 'CPU utilization is too high', acknowledgement: 0, tags: [] },
  { eventid: '501', objectid: '901', source: 0, object: 0, clock: NOW - 3 * 86400 - 2 * 3600, ns: 0, severity: '5', name: 'Disk space is critically low on /data', acknowledgement: '0', tags: [{ tag: 'scope', value: 'ops' }] },
]
const ZBX_TRIGGER_HOSTS = [
  { triggerid: '901', hosts: [{ hostid: '1001', host: 'web-01', name: 'Web Server 01' }] },
  { triggerid: '902', hosts: [{ hostid: '1002', host: 'db-01', name: 'DB Server 01' }] },
]
const ZBX_TRIGGERS = [
  { triggerid: '901', description: 'Disk space is critically low on /data', priority: '4', value: '1', state: '0', status: '0', lastchange: NOW - 86400, hosts: ZBX_TRIGGER_HOSTS[0].hosts },
  { triggerid: '902', description: 'Too many connections on db-01', priority: '5', value: '1', state: '0', status: '0', lastchange: NOW - 7200, hosts: ZBX_TRIGGER_HOSTS[1].hosts },
]
const ZBX_HOSTS = [
  { hostid: '1001', host: 'web-01', name: 'Web Server 01', status: '0', interfaces: [{ interfaceid: '1', ip: '10.0.0.1', dns: '', port: '10050', type: '1', available: '1' }] },
  { hostid: '1002', host: 'db-01', name: 'DB Server 01', status: '0', interfaces: [{ interfaceid: '2', ip: '10.0.0.2', dns: 'db-01.corp', port: '10050', type: '1', available: '2' }] },
]

const zbxHits = { version: 0, login: 0, problem: 0, host: 0, trigger: 0 }
let lastRpc = null

// ---------------- Redis RESP2 桩数据 ----------------
const REDIS_INFO_ALL = '# Server\r\nredis_version:7.2.4\r\nredis_mode:standalone\r\nos:Linux\r\nrun_id:ops-e2e-run-id\r\ntcp_port:6379\r\nuptime_in_seconds:86400\r\n\r\n# Memory\r\nused_memory:104857600\r\nmaxmemory:268435456\r\nmem_fragmentation_ratio:1.20\r\n\r\n# Clients\r\nconnected_clients:12\r\nblocked_clients:0\r\n\r\n# Replication\r\nrole:master\r\nconnected_slaves:0\r\nmaster_replid:ops-e2e-replid\r\nmaster_repl_offset:0\r\n'
const REDIS_INFO_MEMORY = '# Memory\r\nused_memory:104857600\r\nmaxmemory:268435456\r\n'
const REDIS_CLIENT_LIST = 'id=11 addr=127.0.0.1:50001 fd=8 name=ops-e2e db=0 sub=0 psub=0 multi=-1\r\nid=12 addr=127.0.0.1:50002 fd=9 name= db=0 sub=0 psub=0 multi=-1\r\n'
const REDIS_SLOWLOG = [
  [1, NOW - 60, 15000, ['GET', 'bigkey'], '127.0.0.1:50001', 'ops-e2e'],
  [2, NOW - 120, 250000, ['KEYS', '*'], '127.0.0.1:50002', ''],
]
// SE.Redis 握手会读 CONFIG GET replica-read-only / databases，必须给到
const REDIS_CONFIG_DB = { 'replica-read-only': 'no', databases: '16', maxmemory: '268435456', 'maxmemory-policy': 'noeviction' }
const redisHits = { auth: 0, info: 0, dbsize: 0, slowlog: 0, clientList: 0, config: 0, type: 0, ttl: 0, len: 0, memory: 0, ping: 0, echo: 0, setinfo: 0, select: 0 }

const sleep = (ms) => new Promise((r) => setTimeout(r, ms))

// ---------------- HTTP 桩服务：拨测目标 + Zabbix JSON-RPC ----------------
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

      if (pathName === '/ok') return send(200, { status: 'up' })
      if (pathName === '/boom') return send(500, { error: 'boom' })

      if (pathName === '/zabbix/api_jsonrpc.php' || pathName === '/api_jsonrpc.php') {
        let rpc = null
        try { rpc = JSON.parse(body) } catch { /* 非 JSON */ }
        const headerAuth = req.headers['authorization'] ?? ''
        lastRpc = { path: pathName, method: rpc?.method ?? '', params: rpc?.params ?? {}, bodyAuth: rpc?.auth ?? null, headerAuth }
        const id = rpc?.id ?? 1
        const reply = (result) => send(200, { jsonrpc: '2.0', result, id })
        const replyErr = (code, message, data) => send(200, { jsonrpc: '2.0', error: { code, message, data }, id })

        if (rpc?.method === 'apiinfo.version') { zbxHits.version++; return reply('6.0.33') }
        if (rpc?.method === 'user.login') {
          zbxHits.login++
          const user = rpc.params?.username ?? rpc.params?.user
          if (user === 'opsadmin' && rpc.params?.password === 'opspass') return reply(ZBX_SESSION)
          return replyErr(-32602, 'Invalid params.', 'Login name or password is incorrect.')
        }

        const authorized = rpc?.auth === ZBX_SESSION || headerAuth === `Bearer ${ZBX_TOKEN}`
        if (!authorized) return replyErr(-32602, 'Not authorized', '')

        if (rpc.method === 'problem.get') {
          zbxHits.problem++
          const min = Array.isArray(rpc.params?.severities) ? Math.min(...rpc.params.severities) : 0
          return reply(ZBX_PROBLEMS.filter((p) => Number(p.severity) >= min))
        }
        if (rpc.method === 'trigger.get') {
          zbxHits.trigger++
          if (Array.isArray(rpc.params?.triggerids)) {
            return reply(ZBX_TRIGGER_HOSTS.filter((t) => rpc.params.triggerids.includes(t.triggerid)))
          }
          return reply(ZBX_TRIGGERS)
        }
        if (rpc.method === 'host.get') {
          zbxHits.host++
          const search = rpc.params?.search?.host
          if (search !== undefined && Array.isArray(rpc.params?.output) && rpc.params.output.length === 1 && rpc.params.output[0] === 'hostid') {
            return reply(search === 'web' ? [{ hostid: '1001' }] : [])
          }
          return reply(ZBX_HOSTS)
        }
        return replyErr(-32602, 'Unknown method', rpc.method)
      }

      return send(404, { code: 404, message: `no mock for ${pathName}` })
    })
  })
  return new Promise((resolve) => server.listen(MOCK_PORT, '127.0.0.1', () => resolve(server)))
}

// ---------------- Redis RESP2 桩（最小实现：仅覆盖插件白名单内的诊断命令） ----------------
const REDIS_DEBUG = process.env.OPP_REDIS_DEBUG === '1'

function parseRespCommand(buf) {
  if (buf.length === 0) return null
  if (buf[0] === 0x2a) { // '*N' 数组形态
    const headerEnd = buf.indexOf('\r\n')
    if (headerEnd < 0) return null
    const count = parseInt(buf.toString('latin1', 1, headerEnd), 10)
    if (!Number.isInteger(count)) return null
    let pos = headerEnd + 2
    const argv = []
    for (let i = 0; i < count; i++) {
      if (pos >= buf.length || buf[pos] !== 0x24) return null // '$len' bulk
      const lenEnd = buf.indexOf('\r\n', pos)
      if (lenEnd < 0) return null
      const len = parseInt(buf.toString('latin1', pos + 1, lenEnd), 10)
      if (!Number.isInteger(len)) return null
      const start = lenEnd + 2
      if (buf.length < start + len + 2) return null
      // latin1 保证字节 1:1 保留（ECHO 需要原样回显二进制 token）
      argv.push(buf.toString('latin1', start, start + len))
      pos = start + len + 2
    }
    return { argv, consumed: pos }
  }
  // inline 命令兜底
  const lineEnd = buf.indexOf('\r\n')
  if (lineEnd < 0) return null
  const line = buf.toString('utf8', 0, lineEnd).trim()
  return { argv: line.split(/\s+/).filter(Boolean), consumed: lineEnd + 2 }
}

function startRedisStub() {
  const server = net.createServer((socket) => {
    let buffer = Buffer.alloc(0)
    const write = (value) => {
      if (typeof value === 'number') socket.write(`:${value}\r\n`)
      else if (typeof value === 'string' && value.startsWith('-ERR')) socket.write(`${value}\r\n`)
      else if (typeof value === 'string' && value.startsWith('+')) socket.write(`${value}\r\n`)
      else if (value === null) socket.write('$-1\r\n')
      else if (Array.isArray(value)) {
        socket.write(`*${value.length}\r\n`)
        for (const item of value) write(item)
      } else {
        const b = Buffer.from(String(value), 'utf8')
        socket.write(`$${b.length}\r\n`)
        socket.write(b)
        socket.write('\r\n')
      }
    }
    const handle = (argv) => {
      const cmd = (argv[0] ?? '').toUpperCase()
      const sub = (argv[1] ?? '').toUpperCase()
      if (REDIS_DEBUG) console.log(`REDIS> ${JSON.stringify(argv.slice(0, 4))}`)
      if (cmd === 'AUTH') { redisHits.auth++; return argv[1] === REDIS_PASSWORD ? write('+OK') : write('-ERR WRONGPASS invalid password') }
      if (cmd === 'HELLO') { return write('-ERR unknown command \'HELLO\'') } // SE.Redis 收到错误后回退 RESP2
      if (cmd === 'SELECT') { redisHits.select++; return write('+OK') }
      if (cmd === 'CLIENT' && sub === 'SETINFO') { redisHits.setinfo++; return write('+OK') }
      if (cmd === 'CLIENT' && sub === 'SETNAME') { return write('+OK') }
      if (cmd === 'CLIENT' && sub === 'ID') { return write(1) }
      if (cmd === 'CLIENT' && sub === 'LIST') { redisHits.clientList++; return write(REDIS_CLIENT_LIST) }
      if (cmd === 'GET') { return write(null) } // __Booksleeve_TieBreak 等内部键：一律 nil
      if (cmd === 'ECHO') { redisHits.echo++; const echo = Buffer.from(argv[1] ?? '', 'latin1'); socket.write(`$${echo.length}\r\n`); socket.write(echo); socket.write('\r\n'); return }
      if (cmd === 'PING') { redisHits.ping++; return write('+PONG') }
      if (cmd === 'INFO') {
        redisHits.info++
        const section = (argv[1] ?? '').toUpperCase()
        if (section === 'MEMORY') return write(REDIS_INFO_MEMORY)
        return write(REDIS_INFO_ALL)
      }
      if (cmd === 'DBSIZE') { redisHits.dbsize++; return write(42) }
      if (cmd === 'SLOWLOG' && sub === 'GET') { redisHits.slowlog++; return write(REDIS_SLOWLOG) }
      if (cmd === 'CONFIG' && sub === 'GET') {
        redisHits.config++
        const pattern = argv[2] ?? '*'
        const prefix = pattern.endsWith('*') ? pattern.slice(0, -1) : pattern
        return write(Object.entries(REDIS_CONFIG_DB).filter(([k]) => k.startsWith(prefix)).flat())
      }
      if (cmd === 'SENTINEL') { return write('-ERR unknown command \'SENTINEL\'') }
      if (cmd === 'CLUSTER') { return write('-ERR unknown command \'CLUSTER\'') }
      if (cmd === 'TYPE') { redisHits.type++; return argv[1] === 'hashkey' ? write('+hash') : argv[1] === 'session:1001' ? write('+string') : write('+none') }
      if (cmd === 'TTL') { redisHits.ttl++; return argv[1] === 'hashkey' ? write(3600) : write(-2) }
      if (cmd === 'HLEN') { redisHits.len++; return write(128) }
      if (cmd === 'STRLEN') { redisHits.len++; return write(16) }
      if (cmd === 'MEMORY' && sub === 'USAGE') { redisHits.memory++; return write(4096) }
      return write(`-ERR unknown command '${cmd}'`)
    }
    socket.on('data', (chunk) => {
      buffer = Buffer.concat([buffer, chunk])
      while (true) {
        const parsed = parseRespCommand(buffer)
        if (!parsed) break
        buffer = buffer.subarray(parsed.consumed)
        handle(parsed.argv)
      }
    })
    socket.on('error', (err) => { if (REDIS_DEBUG) console.log(`REDIS socket error: ${err.message}`) })
    socket.on('close', (hadError) => { if (REDIS_DEBUG) console.log(`REDIS socket close (hadError=${hadError})`) })
  })
  return new Promise((resolve) => server.listen(REDIS_PORT, '127.0.0.1', () => resolve(server)))
}

// ---------------- 后端：优先用独立输出目录 DLL（绕开运行中后端的 DLL 锁），否则回退 dotnet run ----------------
function startBackend() {
  const dll = path.join(REPO_ROOT, '.builds', 'ops-p1', 'MoAI.dll')
  if (!fs.existsSync(dll)) {
    console.log('WARN | 未找到 .builds/ops-p1/MoAI.dll，回退 dotnet run（默认 bin/Debug；若后端正被运行会因 DLL 锁失败）')
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
  console.log(`HTTP 桩已就绪：${MOCK}`)
  const redisStub = await startRedisStub()
  console.log(`Redis RESP2 桩已就绪：127.0.0.1:${REDIS_PORT}`)
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
    const PROBE_DENIED = `ops_probe_d_${TS}`
    const PROBE_OK = `ops_probe_ok_${TS}`
    const ZBX_BODY = `ops_zbx_body_${TS}`
    const ZBX_HEADER = `ops_zbx_head_${TS}`
    const ZBX_BAD = `ops_zbx_bad_${TS}`
    const REDIS_OK = `ops_redis_ok_${TS}`
    const REDIS_BAD = `ops_redis_bad_${TS}`

    // ---------- 注册表 ----------
    const list = await api('GET', '/api/ai/plugin', { token: admin })
    const items = list.json?.items ?? list.json ?? []
    for (const [tplKey, configTypeKey] of [['http_probe', 'HttpProbeConfig'], ['zabbix_query', 'ZabbixQueryConfig'], ['redis_query', 'RedisQueryConfig']]) {
      const tpl = items.find((x) => x.key === tplKey)
      check(`注册表含 ${tplKey} 且为动态模板`, Boolean(tpl) && tpl.isDynamic === true, list.text.slice(0, 200))
      check(`${tplKey} 配置类型已解析`, (tpl?.configType ?? '').includes(configTypeKey), tpl?.configType ?? '')
    }

    // ---------- S50 http_probe：内网防护 + 三模式拨测 ----------
    check('@DYN-S50a 创建拨测实例（默认拒绝内网）', await saveInstance(admin, PROBE_DENIED, 'http_probe', '拨测-守卫', { TimeoutSeconds: 10, BodyPreviewChars: 512, AllowPrivateNetwork: false }))
    const denied = await run(admin, PROBE_DENIED, JSON.stringify({ Mode: 'http', Url: `${MOCK_BASE_URL}/ok` }))
    check('@DYN-S50b 内网防护默认拒绝 127.0.0.1', denied.json?.success === true && denied.data?.Ok === false && /内网/.test(denied.data?.Error ?? ''), JSON.stringify(denied.data ?? {}))
    check('@DYN-S50c 拨测实例（放行内网）创建成功', await saveInstance(admin, PROBE_OK, 'http_probe', '拨测-放行', { TimeoutSeconds: 10, BodyPreviewChars: 512, AllowPrivateNetwork: true }))

    const httpOk = await run(admin, PROBE_OK, JSON.stringify({ Mode: 'http', Url: `${MOCK_BASE_URL}/ok` }))
    check('@DYN-S50d http 拨测 200（状态码/正文预览/耗时）', httpOk.json?.success === true && httpOk.data?.Ok === true && httpOk.data?.StatusCode === 200 && /up/.test(httpOk.data?.BodyPreview ?? '') && Number.isFinite(httpOk.data?.ElapsedMs), JSON.stringify(httpOk.data ?? {}))
    const httpBoom = await run(admin, PROBE_OK, JSON.stringify({ Mode: 'http', Url: `${MOCK_BASE_URL}/boom` }))
    check('@DYN-S50e http 拨测 500 → Ok=false 且无传输层 Error', httpBoom.json?.success === true && httpBoom.data?.Ok === false && httpBoom.data?.StatusCode === 500 && httpBoom.data?.Error == null, JSON.stringify(httpBoom.data ?? {}))
    const tcpOk = await run(admin, PROBE_OK, JSON.stringify({ Mode: 'tcp', Host: '127.0.0.1', Port: MOCK_PORT }))
    check('@DYN-S50f tcp 拨测连通', tcpOk.json?.success === true && tcpOk.data?.Ok === true && String(tcpOk.data?.RemoteAddress ?? '').startsWith('127.0.0.1'), JSON.stringify(tcpOk.data ?? {}))
    const tcpClosed = await run(admin, PROBE_OK, JSON.stringify({ Mode: 'tcp', Host: '127.0.0.1', Port: CLOSED_PORT }))
    check('@DYN-S50g tcp 拨测拒绝端口 → Ok=false 且带原因', tcpClosed.json?.success === true && tcpClosed.data?.Ok === false && (tcpClosed.data?.Error ?? '').length > 0, JSON.stringify(tcpClosed.data ?? {}))
    const dnsOk = await run(admin, PROBE_OK, JSON.stringify({ Mode: 'dns', Host: 'localhost' }))
    check('@DYN-S50h dns 拨测解析出地址', dnsOk.json?.success === true && dnsOk.data?.Ok === true && (dnsOk.data?.Addresses ?? []).length >= 1, JSON.stringify(dnsOk.data ?? {}))
    const badMethod = await run(admin, PROBE_OK, JSON.stringify({ Mode: 'http', Url: `${MOCK_BASE_URL}/ok`, Method: 'DELETE' }))
    const badScheme = await run(admin, PROBE_OK, JSON.stringify({ Mode: 'http', Url: 'ftp://example.com' }))
    check('@DYN-S50i 参数校验：非法方法与非法协议均拒绝', badMethod.json?.success === false && badScheme.json?.success === false, `${badMethod.json?.error} / ${badScheme.json?.error}`)

    // ---------- S51 zabbix_query：body auth（子路径）+ header auth（根路径）+ 登录失败 ----------
    check('@DYN-S51a 创建 Zabbix 实例（子路径 + 用户名密码）', await saveInstance(admin, ZBX_BODY, 'zabbix_query', 'Zabbix-body', { BaseUrl: `${MOCK_BASE_URL}/zabbix`, Username: 'opsadmin', Password: 'opspass', TimeoutSeconds: 10, MaxListItems: 100 }))
    const version = await run(admin, ZBX_BODY, JSON.stringify({ Mode: 'version' }))
    check('@DYN-S51b version 免鉴权返回服务端版本', version.json?.success === true && version.data?.Version === '6.0.33', JSON.stringify(version.data ?? version.json?.error ?? {}))

    const problems = await run(admin, ZBX_BODY, JSON.stringify({ Mode: 'problems' }))
    const disaster = (problems.data?.Problems ?? []).find((p) => p.SeverityName === '灾难')
    check('@DYN-S51c problems 解析（灾难级/主机富化/ISO 时间/时长/标签）', problems.json?.success === true && (problems.data?.Problems ?? []).length === 2 && disaster?.HostName === 'Web Server 01' && /^\d{4}-\d{2}-\d{2}T/.test(disaster?.Clock ?? '') && (disaster?.Age ?? '').length > 0 && disaster?.Tags?.scope === 'ops', JSON.stringify(problems.data ?? problems.json?.error ?? {}))
    const filtered = await run(admin, ZBX_BODY, JSON.stringify({ Mode: 'problems', SeverityMin: 3 }))
    check('@DYN-S51d SeverityMin 过滤（仅剩严重以上）', filtered.json?.success === true && (filtered.data?.Problems ?? []).length === 1 && filtered.data.Problems[0].SeverityName === '灾难', JSON.stringify(filtered.data ?? {}))
    check('@DYN-S51e body auth 形态命中（session 放 auth 属性）', lastRpc?.path === '/zabbix/api_jsonrpc.php' && lastRpc?.bodyAuth === ZBX_SESSION && (lastRpc?.headerAuth ?? '') === '', JSON.stringify(lastRpc ?? {}))

    const hosts = await run(admin, ZBX_BODY, JSON.stringify({ Mode: 'hosts' }))
    check('@DYN-S51f hosts 列表与接口解析', hosts.json?.success === true && (hosts.data?.Hosts ?? []).length === 2 && hosts.data.Hosts[0].Name === 'Web Server 01' && hosts.data.Hosts[0].Interfaces?.[0]?.ip === '10.0.0.1', JSON.stringify(hosts.data ?? hosts.json?.error ?? {}))
    const triggers = await run(admin, ZBX_BODY, JSON.stringify({ Mode: 'triggers' }))
    check('@DYN-S51g triggers 问题态列表（严重级名/主机）', triggers.json?.success === true && (triggers.data?.Triggers ?? []).length === 2 && triggers.data.Triggers.every((t) => t.Value === 1 && t.PriorityName.length > 0) && triggers.data.Triggers[0].Hosts?.[0]?.Name === 'Web Server 01', JSON.stringify(triggers.data ?? triggers.json?.error ?? {}))

    check('@DYN-S51h 创建 Zabbix 实例（API 令牌 + Authorization 头）', await saveInstance(admin, ZBX_HEADER, 'zabbix_query', 'Zabbix-header', { BaseUrl: MOCK_BASE_URL, Token: ZBX_TOKEN, UseHeaderAuth: true, TimeoutSeconds: 10, MaxListItems: 100 }))
    const headerProblems = await run(admin, ZBX_HEADER, JSON.stringify({ Mode: 'problems' }))
    check('@DYN-S51i header auth 形态命中（Bearer 放头，根路径部署）', headerProblems.json?.success === true && lastRpc?.path === '/api_jsonrpc.php' && lastRpc?.headerAuth === `Bearer ${ZBX_TOKEN}` && lastRpc?.bodyAuth === null, JSON.stringify(lastRpc ?? {}))

    check('@DYN-S51j 创建 Zabbix 实例（错误密码）', await saveInstance(admin, ZBX_BAD, 'zabbix_query', 'Zabbix-bad', { BaseUrl: MOCK_BASE_URL, Username: 'opsadmin', Password: 'wrong', TimeoutSeconds: 10, MaxListItems: 100 }))
    const zbxBad = await run(admin, ZBX_BAD, JSON.stringify({ Mode: 'problems' }))
    check('@DYN-S51k 登录失败归一为可读错误（含重试 user 参数名）', zbxBad.json?.success === false && /登录失败/.test(zbxBad.json?.error ?? '') && /Login name or password/.test(zbxBad.json?.error ?? ''), `${zbxBad.status} ${zbxBad.json?.error?.slice(0, 200)}`)

    // ---------- S52 redis_query：六模式 + 错误归一 ----------
    check('@DYN-S52a 创建 Redis 实例', await saveInstance(admin, REDIS_OK, 'redis_query', 'Redis-ok', { Host: '127.0.0.1', Port: REDIS_PORT, Password: REDIS_PASSWORD, TimeoutSeconds: 5, MaxListItems: 100 }))
    const info = await run(admin, REDIS_OK, JSON.stringify({ Mode: 'info' }))
    check('@DYN-S52b info 分节解析（memory/clients）', info.json?.success === true && info.data?.Sections?.memory?.used_memory === '104857600' && info.data?.Sections?.clients?.connected_clients === '12', JSON.stringify(info.data ?? info.json?.error ?? {}))
    const infoMem = await run(admin, REDIS_OK, JSON.stringify({ Mode: 'info', Section: 'memory' }))
    check('@DYN-S52c info 指定节段透传', infoMem.json?.success === true && infoMem.data?.Sections?.memory?.used_memory === '104857600', JSON.stringify(infoMem.data ?? {}))

    const dbsize = await run(admin, REDIS_OK, JSON.stringify({ Mode: 'dbsize' }))
    check('@DYN-S52d dbsize 返回键数量', dbsize.json?.success === true && dbsize.data?.DbSize === 42, JSON.stringify(dbsize.data ?? dbsize.json?.error ?? {}))

    const slowlog = await run(admin, REDIS_OK, JSON.stringify({ Mode: 'slowlog' }))
    const slowFirst = slowlog.data?.SlowLog?.[0]
    check('@DYN-S52e slowlog 条目完整解析（命令/参数/耗时/客户端/时间）', slowlog.json?.success === true && (slowlog.data?.SlowLog ?? []).length === 2 && slowFirst?.Command === 'GET' && slowFirst?.Args === 'bigkey' && slowFirst?.DurationMicros === 15000 && slowFirst?.ClientAddress === '127.0.0.1:50001' && slowFirst?.ClientName === 'ops-e2e' && /\d{4}-\d{2}-\d{2}T/.test(slowFirst?.Timestamp ?? ''), JSON.stringify(slowlog.data ?? slowlog.json?.error ?? {}))

    const clients = await run(admin, REDIS_OK, JSON.stringify({ Mode: 'client_list' }))
    check('@DYN-S52f client_list 行解析', clients.json?.success === true && (clients.data?.Clients ?? []).length === 2 && clients.data.Clients[0].addr === '127.0.0.1:50001' && clients.data.Clients[0].name === 'ops-e2e' && clients.data.Clients[1].name === '', JSON.stringify(clients.data ?? clients.json?.error ?? {}))

    const config = await run(admin, REDIS_OK, JSON.stringify({ Mode: 'config_get', Pattern: 'maxmemory*' }))
    check('@DYN-S52g config_get 键值对解析', config.json?.success === true && config.data?.Config?.maxmemory === '268435456' && config.data?.Config?.['maxmemory-policy'] === 'noeviction', JSON.stringify(config.data ?? config.json?.error ?? {}))

    const keyInfo = await run(admin, REDIS_OK, JSON.stringify({ Mode: 'key_info', Key: 'hashkey' }))
    check('@DYN-S52h key_info 单键体检（类型/TTL/长度/内存）', keyInfo.json?.success === true && keyInfo.data?.KeyInfo?.Exists === true && keyInfo.data?.KeyInfo?.Type === 'hash' && keyInfo.data?.KeyInfo?.TtlSeconds === 3600 && keyInfo.data?.KeyInfo?.Length === 128 && keyInfo.data?.KeyInfo?.MemoryBytes === 4096, JSON.stringify(keyInfo.data ?? keyInfo.json?.error ?? {}))
    const keyMissing = await run(admin, REDIS_OK, JSON.stringify({ Mode: 'key_info', Key: 'nokey' }))
    check('@DYN-S52i key_info 不存在的键', keyMissing.json?.success === true && keyMissing.data?.KeyInfo?.Exists === false && keyMissing.data?.KeyInfo?.Type === 'none', JSON.stringify(keyMissing.data ?? {}))
    const keyNoArg = await run(admin, REDIS_OK, JSON.stringify({ Mode: 'key_info' }))
    check('@DYN-S52j key_info 缺 Key 参数被拒绝', keyNoArg.json?.success === false, `${keyNoArg.status} ${keyNoArg.json?.error?.slice(0, 120)}`)

    check('@DYN-S52k 创建 Redis 实例（错误密码）', await saveInstance(admin, REDIS_BAD, 'redis_query', 'Redis-bad', { Host: '127.0.0.1', Port: REDIS_PORT, Password: 'wrongpw', TimeoutSeconds: 5, MaxListItems: 100 }))
    const redisBad = await run(admin, REDIS_BAD, JSON.stringify({ Mode: 'info' }))
    check('@DYN-S52l 错误密码归一为可读失败', redisBad.json?.success === false && /Redis|WRONGPASS|password/i.test(redisBad.json?.error ?? ''), `${redisBad.status} ${redisBad.json?.error?.slice(0, 200)}`)

    check('桩确被命中（zabbix 各方法 + redis 各命令）', zbxHits.problem >= 3 && zbxHits.host >= 1 && zbxHits.trigger >= 2 && zbxHits.login >= 2 && redisHits.info >= 2 && redisHits.slowlog >= 1 && redisHits.type >= 2 && redisHits.auth >= 2, JSON.stringify({ zbxHits, redisHits }))

    // ---------- 清理 ----------
    for (const key of [PROBE_DENIED, PROBE_OK, ZBX_BODY, ZBX_HEADER, ZBX_BAD, REDIS_OK, REDIS_BAD]) {
      await del(admin, key)
    }

    console.log(`\n=== P1 运维插件桩服务 E2E: PASS ${PASS} / FAIL ${FAIL} ===`)
    if (FAIL > 0) process.exitCode = 1
  } finally {
    if (process.env.OPP_KEEP_BACKEND === '1') {
      console.log(`OPP_KEEP_BACKEND=1，保留后端 pid=${backend.pid}`)
      // 保留后端时必须断开 stdio 管道并 unref，否则本脚本进程因子进程管道挂住不退出
      backend.stdout?.destroy()
      backend.stderr?.destroy()
      backend.unref()
    } else {
      await killBackend(backend)
    }
    mock.close()
    mock.closeAllConnections()
    redisStub.close()
    redisStub.closeAllConnections()
  }
}

main().catch((e) => { console.error('E2E 异常:', e.message); process.exitCode = 1 })
