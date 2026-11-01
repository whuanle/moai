// 站内信 + SignalR 事件推送 · E2E（场景 @MSG-S1..）
// 覆盖：Hub 鉴权（未认证 401 / access_token 连接）/ 消息接口门禁与空态 / 参数校验 /
//       MQ 全链路（RabbitMQ 管理口发布 message.push → MessagePushConsumer → SignalR onEvent 送达）/
//       事件目标隔离（仅目标用户收到）/ 同用户多连接均收到 / REST 空库幂等。
// 前置：后端运行中（需 Postgres/Redis/RabbitMQ 可达）；种子账号 admin / abcd123456。
//       MQ 场景经 RabbitMQ 管理 HTTP API（默认 http://192.168.50.199:15672，guest/guest）发布测试事件，
//       管理口不可达时该组场景自动 SKIP（不影响其余用例）。
// 用法：node local-dev/message-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5000，可用 MSG_BASE 覆盖）
import crypto from 'node:crypto'

const BASE = process.env.MSG_BASE ?? process.argv[2] ?? 'http://127.0.0.1:5000'
const MQ_API = process.env.MSG_MQ_API ?? 'http://192.168.50.199:15672'
const MQ_USER = process.env.MSG_MQ_USER ?? 'guest'
const MQ_PASS = process.env.MSG_MQ_PASS ?? 'guest'

let PASS = 0
let FAIL = 0
let SKIP = 0
const check = (name, cond, detail = '') => {
  if (cond === 'SKIP') {
    SKIP++
    console.log(`SKIP | ${name}`)
  } else if (cond) {
    PASS++
    console.log(`PASS | ${name}`)
  } else {
    FAIL++
    console.log(`FAIL | ${name}${detail ? ' — ' + detail : ''}`)
  }
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

// ---------------------------------------------------------------------------
// SignalR 长轮询裸客户端（仅收事件，零 npm 依赖）
// 流程：POST /hub/moai/negotiate（Bearer）→ 挂起 GET /hub/moai?id=..&access_token=.. →
//       POST 同路径发握手帧 `{"protocol":"json","version":1}\x1e` → 首个 poll 返回握手响应 → 循环 poll 收帧。
async function connectHub(token) {
  const neg = await fetch(`${BASE}/hub/moai/negotiate?negotiateVersion=1`, {
    method: 'POST',
    headers: { Authorization: `Bearer ${token}` },
  })
  if (neg.status !== 200) {
    return { ok: false, status: neg.status, detail: `negotiate=${neg.status} body=${(await neg.text()).slice(0, 120)}` }
  }
  const negBody = await neg.json()
  const connToken = encodeURIComponent(negBody.connectionToken ?? negBody.connectionId)
  const q = `id=${connToken}&access_token=${encodeURIComponent(token)}`

  let releasePoll
  const firstPoll = new Promise((resolve) => { releasePoll = resolve })
  ;(async () => {
    const ctrl = new AbortController()
    const timer = setTimeout(() => ctrl.abort(), 30000)
    try {
      const res = await fetch(`${BASE}/hub/moai?${q}`, { signal: ctrl.signal })
      releasePoll({ status: res.status, text: await res.text() })
    } catch {
      releasePoll({ status: 0, text: '' })
    } finally {
      clearTimeout(timer)
    }
  })()

  const hs = await fetch(`${BASE}/hub/moai?${q}`, {
    method: 'POST',
    headers: { 'Content-Type': 'text/plain;charset=UTF-8' },
    body: '{"protocol":"json","version":1}\x1e',
  })
  const first = await firstPoll
  // 握手成功响应为 `{}\x1e`（无 error 字段即成功）
  const handshakeOk = hs.status === 200 && first.status === 200 && !first.text.includes('"error"')
  if (!handshakeOk) {
    return { ok: false, status: hs.status, first, detail: `hs=${hs.status} poll=${first.status} body=${first.text.slice(0, 120)}` }
  }

  async function pollOnce(timeoutMs) {
    const ctrl = new AbortController()
    const timer = setTimeout(() => ctrl.abort(), timeoutMs)
    try {
      const res = await fetch(`${BASE}/hub/moai?${q}`, { signal: ctrl.signal })
      return await res.text()
    } catch {
      return ''
    } finally {
      clearTimeout(timer)
    }
  }

  // 循环 poll 直到断言函数命中或超时；返回累计帧文本
  async function waitEvent(matchFn, timeoutMs) {
    const deadline = Date.now() + timeoutMs
    let frames = ''
    while (Date.now() < deadline) {
      const text = await pollOnce(Math.min(5000, deadline - Date.now()))
      if (text) {
        frames += text
        if (matchFn(frames)) return frames
      }
    }
    return frames
  }

  return { ok: true, connectionId: negBody.connectionId, pollOnce, waitEvent }
}

// 从 SignalR 帧文本中提取 onEvent 事件负载（type:1 invocation，帧以 \x1e 分隔）
function extractEvents(frames) {
  const events = []
  for (const raw of frames.split('\x1e')) {
    if (!raw.trim()) continue
    try {
      const msg = JSON.parse(raw)
      if (msg.type === 1 && msg.target === 'onEvent') {
        events.push(msg.arguments?.[0] ?? {})
      }
    } catch { /* ping/半帧忽略 */ }
  }
  return events
}

async function mqPublish(userIds, event, data) {
  const payload = { userIds, event, data, time: new Date().toISOString() }
  const basic = Buffer.from(`${MQ_USER}:${MQ_PASS}`).toString('base64')
  // Maomi.MQ 直连默认交换机，routing key = 队列名 message.push（见 MessagePushMessage.QueueName）
  const res = await fetch(`${MQ_API}/api/exchanges/%2F/amq.default/publish`, {
    method: 'POST',
    headers: { Authorization: `Basic ${basic}`, 'Content-Type': 'application/json' },
    body: JSON.stringify({ properties: { content_type: 'application/json' }, routing_key: 'message.push', payload: JSON.stringify(payload), payload_encoding: 'string' }),
  })
  return res.status === 200
}

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  // @MSG-S01 Hub 未认证拒绝：negotiate 不带 token 必须 401
  const negNoAuth = await fetch(`${BASE}/hub/moai/negotiate?negotiateVersion=1`, { method: 'POST' })
  check('MSG-01 Hub negotiate 未认证 401', negNoAuth.status === 401, `实际 ${negNoAuth.status}`)

  // @MSG-S03 消息接口未认证 401
  const apiNoAuth = await api('GET', '/api/message/unread-count')
  check('MSG-03 未认证调用消息接口 401', apiNoAuth.status === 401, `实际 ${apiNoAuth.status}`)

  // 用户与连接准备
  const userA = await mkuser('ma') // 目标用户 A
  const userB = await mkuser('mb') // 隔离对照用户 B

  const hubA1 = await connectHub(userA.token)
  check('MSG-02 携带 access_token 连接 Hub 成功（握手无 error）', hubA1.ok === true, hubA1.detail ?? '')
  const hubB = await connectHub(userB.token)
  const hubA2 = await connectHub(userA.token) // 同用户第二连接（多端）
  if (!hubA1.ok || !hubB.ok || !hubA2.ok) throw new Error('Hub 连接失败，无法继续')

  // @MSG-S04 空态：未读数 0 + 列表空
  const unread0 = await api('GET', '/api/message/unread-count', { token: userA.token })
  const list0 = await api('GET', '/api/message/list?pageNo=1&pageSize=10', { token: userA.token })
  check('MSG-04 新用户空态（unread-count=0 且 list total=0）',
    unread0.status === 200 && Number(unread0.json.value ?? unread0.json) === 0 && list0.status === 200 && list0.json.total === 0,
    `unread=${unread0.text.slice(0, 60)} list=${list0.text.slice(0, 60)}`)

  // @MSG-S05 参数校验：空 ids / 越界 pageSize
  const readEmpty = await api('POST', '/api/message/read', { token: userA.token, body: { messageIds: [] } })
  const delEmpty = await api('POST', '/api/message/delete', { token: userA.token, body: { messageIds: [] } })
  const badPage = await api('GET', '/api/message/list?pageNo=1&pageSize=500', { token: userA.token })
  check('MSG-05 参数校验（read/delete 空 ids 400，pageSize=500 400）',
    readEmpty.status === 400 && delEmpty.status === 400 && badPage.status === 400,
    `read=${readEmpty.status} del=${delEmpty.status} page=${badPage.status}`)

  // @MSG-S09 空库幂等：read-all 200、delete 他人/不存在 id 200（0 行生效）
  const readAllEmpty = await api('POST', '/api/message/read-all', { token: userA.token })
  const delForeign = await api('POST', '/api/message/delete', { token: userA.token, body: { messageIds: [999999999] } })
  check('MSG-09 空库幂等（read-all 200，delete 不存在 id 200）', readAllEmpty.status === 200 && delForeign.status === 200,
    `readAll=${readAllEmpty.status} del=${delForeign.status}`)

  // MQ 可达性门控：管理口不可达则推送链路场景 SKIP
  const mqUp = await fetch(`${MQ_API}/api/overview`, { headers: { Authorization: `Basic ${Buffer.from(`${MQ_USER}:${MQ_PASS}`).toString('base64')}` } })
    .then((r) => r.ok).catch(() => false)

  // @MSG-S06 MQ 全链路：发布 message.push → 消费者 → SignalR onEvent 送达在线连接
  {
    let frames = null
    if (!mqUp) {
      check('MSG-06 MQ→消费者→onEvent 事件送达', 'SKIP', 'RabbitMQ 管理口不可达')
    } else {
      const eventName = `e2e.msg${TS}`
      const published = await mqPublish([userA.userId], eventName, { source: 'e2e' })
      if (!published) {
        check('MSG-06 MQ→消费者→onEvent 事件送达', 'SKIP', '管理口发布失败')
      } else {
        frames = await hubA1.waitEvent((f) => f.includes(eventName), 20000)
        const events = extractEvents(frames)
        const hit = events.find((x) => x.event === eventName)
        check('MSG-06 MQ→消费者→onEvent 事件送达', hit != null && typeof hit.time === 'string', `frames=${JSON.stringify(frames).slice(0, 200)}`)
      }
    }
  }

  // @MSG-S07 目标隔离：只发给 A，B 收不到
  if (!mqUp) {
    check('MSG-07 事件只送达目标用户', 'SKIP', 'RabbitMQ 管理口不可达')
  } else {
    const eventName = `e2e.iso${TS}`
    await mqPublish([userA.userId], eventName)
    const framesB = await hubB.waitEvent((f) => f.includes(eventName), 6000)
    check('MSG-07 事件只送达目标用户（B 未收到发给 A 的事件）', !framesB.includes(eventName))
  }

  // @MSG-S08 同用户多端：A 的两条连接都收到
  if (!mqUp) {
    check('MSG-08 同用户多连接均收到事件', 'SKIP', 'RabbitMQ 管理口不可达')
  } else {
    const eventName = `e2e.multi${TS}`
    await mqPublish([userA.userId], eventName)
    const [f1, f2] = await Promise.all([
      hubA1.waitEvent((f) => f.includes(eventName), 15000),
      hubA2.waitEvent((f) => f.includes(eventName), 15000),
    ])
    check('MSG-08 同用户多连接均收到事件', f1.includes(eventName) && f2.includes(eventName))
  }

  console.log(`\n结果：PASS=${PASS} FAIL=${FAIL} SKIP=${SKIP}`)
  if (FAIL > 0) process.exit(1)
}

await main()
