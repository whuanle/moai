// 企业微信渠道 E2E（场景 @WX-Sn）：企业微信自建应用连接 CRUD/门禁/「创建即绑定」 + 回调验证与消息推送全链路
// 用法：node local-dev/weixinwork-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5000，可用 WW_BASE 覆盖）
// 前置：后端运行中，且已执行 asserts/weixinwork_app.sql 建表（新库由 EnsureCreated 直接建成）。
// 内置本地 qyapi 桩（gettoken/message/send/appchat/send）+ OpenAI 兼容桩模型：连接配置的 API 地址指向桩，
// 回调推送用连接的 Token/EncodingAESKey 加密后打到 /api/weixinwork_app/{id}/callback，全链路零真实依赖。
import crypto from 'node:crypto'
import http from 'node:http'

const BASE = process.env.WW_BASE ?? process.argv[2] ?? 'http://127.0.0.1:5000'
let PASS = 0, FAIL = 0, SKIP = 0
const check = (name, cond, detail = '') => {
  if (cond) { PASS++; console.log(`PASS | ${name}`) }
  else { FAIL++; console.log(`FAIL | ${name} ${detail ? '— ' + detail : ''}`) }
}
const skip = (name, reason = '') => {
  SKIP++; console.log(`SKIP | ${name} ${reason ? '— ' + reason : ''}`)
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

const isGuid = (v) => typeof v === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(v)

// ===== 企业微信回调加解密（与官方「加解密方案」一致） =====
const aesKeyOf = (key43) => Buffer.from(key43 + '=', 'base64')
const wwSign = (token, timestamp, nonce, encrypt) =>
  crypto.createHash('sha1').update([token, timestamp, nonce, encrypt].sort().join(''), 'utf8').digest('hex')

function wwEncrypt(key43, plainXml, receiveId) {
  const key = aesKeyOf(key43)
  const msg = Buffer.from(plainXml, 'utf8')
  const len = Buffer.alloc(4)
  len.writeUInt32BE(msg.length, 0)
  const data = Buffer.concat([crypto.randomBytes(16), len, msg, Buffer.from(receiveId, 'utf8')])
  const cipher = crypto.createCipheriv('aes-256-cbc', key, key.subarray(0, 16))
  return Buffer.concat([cipher.update(data), cipher.final()]).toString('base64')
}

/** 构造加密推送报文（信封 XML），返回 {body, msgSignature, timestamp, nonce} */
function buildPush(key43, token, corpId, messageXml) {
  const encrypt = wwEncrypt(key43, messageXml, corpId)
  const timestamp = String(Math.floor(Date.now() / 1000))
  const nonce = crypto.randomBytes(8).toString('hex')
  const body = `<xml><ToUserName><![CDATA[${corpId}]]></ToUserName><Encrypt><![CDATA[${encrypt}]]></Encrypt><AgentID><![CDATA[1000002]]></AgentID></xml>`
  return { body, msgSignature: wwSign(token, timestamp, nonce, encrypt), timestamp, nonce }
}

const textMsgXml = ({ toUser, fromUser, content, msgId, chatId = '', agentId = 1000002 }) =>
  `<xml><ToUserName><![CDATA[TO]]></ToUserName><FromUserName><![CDATA[${fromUser}]]></FromUserName><CreateTime>${Math.floor(Date.now() / 1000)}</CreateTime><MsgType><![CDATA[text]]></MsgType><Content><![CDATA[${content}]]></Content><MsgId>${msgId}</MsgId>${chatId ? `<ChatId><![CDATA[${chatId}]]></ChatId>` : ''}<AgentID>${agentId}</AgentID></xml>`

// ===== qyapi 桩：gettoken / message/send / appchat/send =====
function startQyapiStub() {
  const sent = []
  const tokens = []
  const server = http.createServer((req, res) => {
    let raw = ''
    req.on('data', (c) => { raw += c })
    req.on('end', () => {
      const url = new URL(req.url, 'http://stub')
      if (req.method === 'GET' && url.pathname === '/cgi-bin/gettoken') {
        tokens.push({ corpid: url.searchParams.get('corpid'), corpsecret: url.searchParams.get('corpsecret') })
        res.writeHead(200, { 'Content-Type': 'application/json' })
        res.end(JSON.stringify({ errcode: 0, errmsg: 'ok', access_token: `stub-token-${tokens.length}`, expires_in: 7200 }))
        return
      }
      if (req.method === 'POST' && (url.pathname === '/cgi-bin/message/send' || url.pathname === '/cgi-bin/appchat/send')) {
        try { sent.push({ kind: url.pathname === '/cgi-bin/appchat/send' ? 'appchat' : 'message', accessToken: url.searchParams.get('access_token'), body: JSON.parse(raw || '{}') }) } catch { /* 忽略 */ }
        res.writeHead(200, { 'Content-Type': 'application/json' })
        res.end(JSON.stringify({ errcode: 0, errmsg: 'ok', msgid: `stub-msg-${sent.length}` }))
        return
      }
      res.writeHead(404, { 'Content-Type': 'application/json' })
      res.end(JSON.stringify({ errcode: -1, errmsg: 'not found' }))
    })
  })
  return { server, sent, tokens, listen: () => new Promise((resolve) => server.listen(0, '127.0.0.1', () => resolve(server.address().port))) }
}

// ===== OpenAI 兼容桩模型：回复回显用户最后一条消息 =====
function startModelStub() {
  const server = http.createServer((req, res) => {
    let raw = ''
    req.on('data', (c) => { raw += c })
    req.on('end', () => {
      const url = new URL(req.url, 'http://stub')
      if (req.method === 'POST' && url.pathname === '/v1/chat/completions') {
        let payload = {}
        try { payload = JSON.parse(raw || '{}') } catch { /* 忽略 */ }
        const userMsgs = (payload.messages ?? []).filter((m) => m.role === 'user').map((m) => String(m.content ?? ''))
        const last = userMsgs[userMsgs.length - 1] ?? ''
        const reply = `WX-STUB-REPLY:${last.slice(0, 40)}`
        if (payload.stream) {
          res.writeHead(200, { 'Content-Type': 'text/event-stream' })
          const base = { id: 'chatcmpl-stub', object: 'chat.completion.chunk', created: Math.floor(Date.now() / 1000), model: payload.model ?? 'stub' }
          res.write('data: ' + JSON.stringify({ ...base, choices: [{ index: 0, delta: { role: 'assistant', content: reply }, finish_reason: null }] }) + '\n\n')
          res.write('data: ' + JSON.stringify({ ...base, choices: [{ index: 0, delta: {}, finish_reason: 'stop' }] }) + '\n\n')
          res.write('data: [DONE]\n\n')
          res.end()
          return
        }
        res.writeHead(200, { 'Content-Type': 'application/json' })
        res.end(JSON.stringify({ id: 'chatcmpl-stub', object: 'chat.completion', created: Math.floor(Date.now() / 1000), model: payload.model ?? 'stub', choices: [{ index: 0, message: { role: 'assistant', content: reply }, finish_reason: 'stop' }], usage: { prompt_tokens: 10, completion_tokens: 10, total_tokens: 20 } }))
        return
      }
      res.writeHead(404)
      res.end()
    })
  })
  return { server, listen: () => new Promise((resolve) => server.listen(0, '127.0.0.1', () => resolve(server.address().port))) }
}

async function adminLogin() {
  try {
    const l = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
    return l.status === 200 ? l.json.accessToken : null
  } catch {
    return null
  }
}

/** 用桩创建渠道 + 对话模型并授权给团队，返回模型 id 或 '' */
async function createStubChatModel(adminToken, TID, stubPort) {
  const chName = `ww-stub渠道-${TS}`
  const crc = await api('POST', '/api/ai/channel', {
    token: adminToken,
    body: { providerKey: 'openai', name: chName, protocolFamily: 'openaiChatCompletions', baseUrl: `http://127.0.0.1:${stubPort}/v1`, apiKey: 'ww-stub-key', enabled: true, description: 'weixinwork E2E 桩渠道' },
  })
  if (crc.status !== 200) { console.log(`INFO | 桩渠道创建失败：${crc.status} ${crc.text.slice(0, 160)}`); return '' }
  const channels = await api('GET', '/api/ai/channel', { token: adminToken })
  const CH_ID = String((channels.json?.items ?? []).find((c) => c.name === chName)?.id ?? '')
  if (!CH_ID) { console.log('INFO | 桩渠道未找到'); return '' }

  const modelName = `ww-stub-chat-${TS}`
  const mrc = await api('POST', '/api/ai/model', {
    token: adminToken,
    body: { channelId: CH_ID, meta: { modelId: modelName, name: modelName, modelKind: 'conversation', description: 'weixinwork E2E 桩模型' }, enabled: true, isPublic: false },
  })
  if (mrc.status !== 200) { console.log(`INFO | 桩模型创建失败：${mrc.status} ${mrc.text.slice(0, 160)}`); return '' }
  const models = await api('GET', `/api/ai/model?channelId=${CH_ID}`, { token: adminToken })
  const modelId = String((models.json?.items ?? []).find((m) => m.name === modelName)?.id ?? '')
  if (!modelId) return ''

  const cur = await api('GET', `/api/ai/model/${modelId}/authorization`, { token: adminToken })
  const teamIds = [...new Set([...(cur.json?.items ?? []).map((i) => Number(i.teamId)), TID])]
  const put = await api('PUT', `/api/ai/model/${modelId}/authorization`, { token: adminToken, body: { modelId, teamIds } })
  if (put.status !== 200) { console.log(`INFO | 桩模型授权失败：${put.status}`); return '' }
  return modelId
}

const wait = (ms) => new Promise((r) => setTimeout(r, ms))
async function waitFor(pred, timeoutMs = 20000, stepMs = 400) {
  const deadline = Date.now() + timeoutMs
  while (Date.now() < deadline) {
    const v = pred()
    if (v) return v
    await wait(stepMs)
  }
  return undefined
}

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const owner = await mkuser('wo')
  const member = await mkuser('wm')
  const outsider = await mkuser('wx')

  const TID = Number((await api('POST', '/api/team', { token: owner.token, body: { name: 'ww-team-' + TS } })).json.value)
  await api('POST', `/api/team/${TID}/users`, { token: owner.token, body: { userId: member.userId, role: 0 } })
  const OTHER_TID = Number((await api('POST', '/api/team', { token: outsider.token, body: { name: 'ww-other-' + TS } })).json.value)

  const appA = (await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: 'ww-a-' + TS, appType: 'agent' } })).json.value
  const appB = (await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: 'ww-b-' + TS, appType: 'agent' } })).json.value
  const appC = (await api('POST', '/api/app', { token: outsider.token, body: { teamId: OTHER_TID, name: 'ww-c-' + TS, appType: 'agent' } })).json.value

  // ===== 模型准备（对话全链路用；失败时依赖模型的场景 SKIP） =====
  const adminToken = await adminLogin()
  const modelStub = startModelStub()
  const modelStubPort = await modelStub.listen()
  const chatModelId = adminToken ? await createStubChatModel(adminToken, TID, modelStubPort) : ''
  let convReady = false
  if (chatModelId) {
    const cfg = await api('PUT', `/api/app/${appA}/agent-config`, { token: owner.token, body: { modelId: chatModelId, prompt: '', wikiIds: [], plugins: [] } })
    const pub = await api('POST', `/api/app/${appA}/publish`, { token: owner.token })
    convReady = cfg.status === 200 && pub.status === 200
    if (!convReady) console.log(`INFO | 应用配置/发布失败：cfg=${cfg.status}(${cfg.text.slice(0, 120)}) pub=${pub.status}(${pub.text.slice(0, 120)})，依赖对话的场景将跳过`)
  } else {
    console.log('INFO | 桩模型不可用，依赖对话的场景将跳过')
  }

  // ===== qyapi 桩 =====
  const qyapi = startQyapiStub()
  const qyapiPort = await qyapi.listen()
  const qyapiBase = `http://127.0.0.1:${qyapiPort}`

  // 连接工厂：API 地址指向 qyapi 桩
  const TOKEN = 'ww-token-' + TS
  const AES_KEY = 'a'.repeat(43)
  const mkConn = (name, { corpId = `wwcorp${TS}`, agentId = 1000002, channelType, channelId } = {}) => ({
    teamId: TID, name, description: 'ww-e2e',
    corpId, corpSecret: 'secret-' + TS, agentId, token: TOKEN, encodingAesKey: AES_KEY,
    apiEndpoint: qyapiBase, ...(channelType ? { channelType, channelId } : {}),
  })

  // ===== @WX-S1：门禁与校验 =====
  check('WX-01 未登录查列表 401', (await api('GET', `/api/weixinwork_app/list?teamId=${TID}`)).status === 401)

  check('WX-02a 空名称 400', (await api('POST', '/api/weixinwork_app', { token: owner.token, body: { ...mkConn('n'), name: '' } })).status === 400)
  check('WX-02b 空企业 ID 400', (await api('POST', '/api/weixinwork_app', { token: owner.token, body: { ...mkConn('n'), corpId: '' } })).status === 400)
  check('WX-02c 空 Secret 400', (await api('POST', '/api/weixinwork_app', { token: owner.token, body: { ...mkConn('n'), corpSecret: '' } })).status === 400)
  check('WX-02d AgentId 0 400', (await api('POST', '/api/weixinwork_app', { token: owner.token, body: { ...mkConn('n'), agentId: 0 } })).status === 400)
  check('WX-02e 空 Token 400', (await api('POST', '/api/weixinwork_app', { token: owner.token, body: { ...mkConn('n'), token: '' } })).status === 400)
  check('WX-02f AESKey 42 位 400', (await api('POST', '/api/weixinwork_app', { token: owner.token, body: { ...mkConn('n'), encodingAesKey: 'a'.repeat(42) } })).status === 400)
  check('WX-02g API 地址非 http 400', (await api('POST', '/api/weixinwork_app', { token: owner.token, body: { ...mkConn('n'), apiEndpoint: 'ftp://x' } })).status === 400)

  check('WX-03 Member 创建 403', (await api('POST', '/api/weixinwork_app', { token: member.token, body: mkConn('m') })).status === 403)

  // ===== @WX-S2：创建与唯一性 =====
  const cr = await api('POST', '/api/weixinwork_app', { token: owner.token, body: mkConn('ww-o-' + TS) })
  check('WX-04a Owner 创建 200 + guid', cr.status === 200 && isGuid(cr.json?.value), JSON.stringify(cr.json).slice(0, 160))
  const WID0 = cr.json.value
  const lst = await api('GET', `/api/weixinwork_app/list?teamId=${TID}`, { token: member.token })
  const item = (lst.json?.items ?? []).find((x) => x.weixinWorkAppId === WID0)
  check('WX-04b 列表可见且不回显密钥', lst.status === 200 && item && item.corpSecret === undefined && item.token === undefined && item.encodingAesKey === undefined, JSON.stringify(item).slice(0, 200))
  check('WX-04c 启用状态在线（回调模式）', item && item.isOnline === true && item.isDisable === false)

  check('WX-05a 重复 CorpId+AgentId 409', (await api('POST', '/api/weixinwork_app', { token: owner.token, body: mkConn('ww-x-' + TS) })).status === 409)
  check('WX-05b 重复名称 409', (await api('POST', '/api/weixinwork_app', { token: owner.token, body: mkConn('ww-o-' + TS, { agentId: 2000002 }) })).status === 409)

  // ===== @WX-S3：创建即绑定 =====
  check('WX-06a 绑定不存在的应用渠道 404', (await api('POST', '/api/weixinwork_app', { token: owner.token, body: mkConn('ww-e-' + TS, { agentId: 3000002, channelType: 'app', channelId: crypto.randomUUID() }) })).status === 404)
  check('WX-06b 绑定跨团队应用渠道 403', (await api('POST', '/api/weixinwork_app', { token: owner.token, body: mkConn('ww-e-' + TS, { agentId: 3000003, channelType: 'app', channelId: appC }) })).status === 403)
  check('WX-06c 渠道 id 格式错误 400', (await api('POST', '/api/weixinwork_app', { token: owner.token, body: mkConn('ww-e-' + TS, { agentId: 3000004, channelType: 'app', channelId: 'not-a-guid' }) })).status === 400)
  check('WX-06d 非法渠道类型枚举 400', (await api('POST', '/api/weixinwork_app', { token: owner.token, body: mkConn('ww-e-' + TS, { agentId: 3000005, channelType: 'wiki', channelId: '1' }) })).status === 400)
  check('WX-06e 只传渠道 id 不传渠道类型 400', (await api('POST', '/api/weixinwork_app', { token: owner.token, body: { ...mkConn('ww-e-' + TS, { agentId: 3000006 }), channelId: appA } })).status === 400)

  const crb = await api('POST', '/api/weixinwork_app', { token: owner.token, body: mkConn('ww-b-' + TS, { agentId: 3000007, channelType: 'app', channelId: appA }) })
  check('WX-07a 创建即绑定 200 + guid', crb.status === 200 && isGuid(crb.json?.value), JSON.stringify(crb.json).slice(0, 160))
  const WID = crb.json.value
  const lst2 = await api('GET', `/api/weixinwork_app/list?teamId=${TID}`, { token: owner.token })
  const item2 = (lst2.json?.items ?? []).find((x) => x.weixinWorkAppId === WID)
  check('WX-07b 列表回显绑定渠道', item2 && item2.bindChannelType === 'app' && item2.bindChannelId === appA && item2.bindTime != null, JSON.stringify(item2).slice(0, 200))

  check('WX-08 同 CorpId+AgentId 再创建（带渠道）409', (await api('POST', '/api/weixinwork_app', { token: owner.token, body: mkConn('ww-b2-' + TS, { agentId: 3000007, channelType: 'app', channelId: appB }) })).status === 409)

  const cr2 = await api('POST', '/api/weixinwork_app', { token: owner.token, body: mkConn('ww-p-' + TS, { agentId: 4000002, channelType: 'app', channelId: appB }) })
  check('WX-09a 第二连接接入另一应用 200', cr2.status === 200 && isGuid(cr2.json?.value), cr2.text.slice(0, 160))
  const WID2 = cr2.json?.value
  check('WX-10 Member 创建即绑定 403', (await api('POST', '/api/weixinwork_app', { token: member.token, body: mkConn('m2', { agentId: 5000002, channelType: 'app', channelId: appA }) })).status === 403)
  check('WX-11 其它团队更新本团队连接 404', (await api('PUT', `/api/weixinwork_app/${WID}`, { token: outsider.token, body: { name: 'hack', corpId: `wwcorp${TS}`, agentId: 1000002, isDisable: false } })).status === 404)

  // ===== @WX-S4：更新 =====
  const upd = await api('PUT', `/api/weixinwork_app/${WID}`, { token: owner.token, body: { name: 'ww-o2-' + TS, description: 'd', corpId: `wwcorp${TS}`, agentId: 3000007, isDisable: true } })
  check('WX-12a 更新并禁用 200', upd.status === 200, upd.text.slice(0, 160))
  const item3 = ((await api('GET', `/api/weixinwork_app/list?teamId=${TID}`, { token: owner.token })).json?.items ?? []).find((x) => x.weixinWorkAppId === WID)
  check('WX-12b 禁用生效且禁用时离线', item3 && item3.isDisable === true && item3.isOnline === false)
  check('WX-12c Secret/Token/AESKey 留空保持不变', (await api('PUT', `/api/weixinwork_app/${WID}`, { token: owner.token, body: { name: 'ww-o2-' + TS, corpId: `wwcorp${TS}`, agentId: 3000007, isDisable: false } })).status === 200)
  check('WX-12d Member 更新 403', (await api('PUT', `/api/weixinwork_app/${WID}`, { token: member.token, body: { name: 'ww-o2-' + TS, corpId: `wwcorp${TS}`, agentId: 3000007, isDisable: false } })).status === 403)
  check('WX-12e 换 CorpId+AgentId 200', (await api('PUT', `/api/weixinwork_app/${WID}`, { token: owner.token, body: { name: 'ww-o2-' + TS, corpId: `wwedit${TS}`, agentId: 3000009, isDisable: false } })).status === 200)
  const lstEdit = await api('GET', `/api/weixinwork_app/list?teamId=${TID}`, { token: owner.token })
  check('WX-12f 列表回显新 CorpId/AgentId', ((lstEdit.json?.items ?? []).find((x) => x.weixinWorkAppId === WID)?.corpId === `wwedit${TS}`) && (lstEdit.json?.items ?? []).find((x) => x.weixinWorkAppId === WID)?.agentId === 3000009)
  check('WX-12g 换成已存在的 CorpId+AgentId 409', (await api('PUT', `/api/weixinwork_app/${WID}`, { token: owner.token, body: { name: 'ww-o2-' + TS, corpId: `wwcorp${TS}`, agentId: 1000002, isDisable: false } })).status === 409)

  // ===== @WX-S5：删除与重建 =====
  check('WX-13a 删除 200', (await api('DELETE', `/api/weixinwork_app/${WID2}`, { token: owner.token })).status === 200)
  check('WX-13b 重复删除 404', (await api('DELETE', `/api/weixinwork_app/${WID2}`, { token: owner.token })).status === 404)
  const recre = await api('POST', '/api/weixinwork_app', { token: owner.token, body: mkConn('ww-p2-' + TS, { agentId: 4000002, channelType: 'app', channelId: appB }) })
  check('WX-13c 删除后同 CorpId+AgentId 可重建并接入同渠道', recre.status === 200, recre.text.slice(0, 160))

  check('WX-14a 删除带绑定的连接 200', (await api('DELETE', `/api/weixinwork_app/${WID}`, { token: owner.token })).status === 200)
  const lst14 = await api('GET', `/api/weixinwork_app/list?teamId=${TID}`, { token: owner.token })
  check('WX-14b 列表不再包含已删连接', (lst14.json?.items ?? []).every((x) => x.weixinWorkAppId !== WID))
  const bind3 = await api('POST', '/api/weixinwork_app', { token: owner.token, body: mkConn('ww-q-' + TS, { agentId: 3000009, channelType: 'app', channelId: appA }) })
  check('WX-14c 应用 A 渠道可被新连接接入', bind3.status === 200, bind3.text.slice(0, 160))
  const WID3 = bind3.json?.value

  // ===== @WX-S6：回调 URL 验证（GET echostr） =====
  const echoPlain = `echostr-${TS}`
  const echoEnc = wwEncrypt(AES_KEY, echoPlain, `wwcorp${TS}`)

  const verifyOk = await fetch(`${BASE}/api/weixinwork_app/callback/${WID3}?msg_signature=${wwSign(TOKEN, '1700000000', 'nonce1', echoEnc)}&timestamp=1700000000&nonce=nonce1&echostr=${encodeURIComponent(echoEnc)}`)
  check('WX-15a 验证 URL：正确签名回显明文', verifyOk.status === 200 && (await verifyOk.text()) === echoPlain)

  const verifyBad = await fetch(`${BASE}/api/weixinwork_app/callback/${WID3}?msg_signature=${'0'.repeat(40)}&timestamp=1700000000&nonce=nonce1&echostr=${encodeURIComponent(echoEnc)}`)
  check('WX-15b 验证 URL：错误签名 400', verifyBad.status === 400, `status=${verifyBad.status}`)

  const verifyMissing = await fetch(`${BASE}/api/weixinwork_app/callback/${crypto.randomUUID()}?msg_signature=x&timestamp=1&nonce=n&echostr=${encodeURIComponent(echoEnc)}`)
  check('WX-15c 验证 URL：连接不存在 404', verifyMissing.status === 404, `status=${verifyMissing.status}`)

  // ===== @WX-S7：消息推送全链路（单聊 → 桩模型 → message/send） =====
  const push = async (weixinWorkAppId, messageXml, { token = TOKEN, corpId = `wwcorp${TS}` } = {}) => {
    const p = buildPush(AES_KEY, token, corpId, messageXml)
    return fetch(`${BASE}/api/weixinwork_app/callback/${weixinWorkAppId}?msg_signature=${p.msgSignature}&timestamp=${p.timestamp}&nonce=${p.nonce}`, {
      method: 'POST',
      headers: { 'Content-Type': 'text/xml' },
      body: p.body,
    })
  }

  const sentCount = () => qyapi.sent.filter((s) => s.kind === 'message').length

  if (convReady) {
    const r1 = await push(WID3, textMsgXml({ fromUser: 'zhangsan', content: '你好，介绍下自己', msgId: `${TS}0001` }))
    check('WX-16a 推送受理 200 success', r1.status === 200 && (await r1.text()) === 'success', `status=${r1.status}`)
    const before = sentCount()
    const reply = await waitFor(() => qyapi.sent.filter((s) => s.kind === 'message').slice(before).find((s) => String(s.body?.text?.content ?? '').includes('WX-STUB-REPLY')))
    check('WX-16b 单聊回复经 message/send 送达（桩模型回复）', !!reply, JSON.stringify(qyapi.sent.slice(-2)).slice(0, 300))
    check('WX-16c 回复携带 touser 与 access_token', !!reply && reply.body.touser === 'zhangsan' && !!reply.accessToken, JSON.stringify(reply?.body ?? null).slice(0, 200))

    // 群聊：带 ChatId 走 appchat/send
    const appchatBefore = qyapi.sent.filter((s) => s.kind === 'appchat').length
    const r2 = await push(WID3, textMsgXml({ fromUser: 'lisi', content: '群里问一下', msgId: `${TS}0002`, chatId: 'wr' + TS }))
    check('WX-17a 群聊推送受理 200', r2.status === 200)
    const groupReply = await waitFor(() => qyapi.sent.filter((s) => s.kind === 'appchat').slice(appchatBefore)[0])
    check('WX-17b 群聊回复经 appchat/send 送达', !!groupReply && groupReply.body.chatid === 'wr' + TS && String(groupReply.body?.text?.content ?? '').includes('WX-STUB-REPLY'), JSON.stringify(groupReply?.body ?? null).slice(0, 200))

    // 去重：同 MsgId 重发只处理一次
    const dupBefore = sentCount()
    await push(WID3, textMsgXml({ fromUser: 'wangwu', content: '只应回复一次', msgId: `${TS}0003` }))
    const firstSeen = await waitFor(() => sentCount() > dupBefore)
    await push(WID3, textMsgXml({ fromUser: 'wangwu', content: '只应回复一次', msgId: `${TS}0003` }))
    await wait(3000)
    check('WX-18 同 MsgId 重复推送去重', firstSeen && sentCount() === dupBefore + 1, `count=${sentCount()} before=${dupBefore}`)

    // 会话延续：同一成员再发一条，仍能回复（会话映射生效）
    const contBefore = sentCount()
    await push(WID3, textMsgXml({ fromUser: 'zhangsan', content: '再聊一句', msgId: `${TS}0004` }))
    check('WX-19 同成员第二条消息仍回复', !!(await waitFor(() => sentCount() > contBefore)))
  } else {
    skip('WX-16 单聊回复全链路（需桩模型）')
    skip('WX-17 群聊回复全链路（需桩模型）')
    skip('WX-18 同 MsgId 去重（需桩模型）')
    skip('WX-19 会话延续（需桩模型）')
  }

  // 非文本消息：受理但不应回复
  {
    const imgXml = `<xml><ToUserName><![CDATA[TO]]></ToUserName><FromUserName><![CDATA[zhangsan]]></FromUserName><CreateTime>1700000000</CreateTime><MsgType><![CDATA[image]]></MsgType><PicUrl><![CDATA[http://x/y.png]]></PicUrl><MsgId>${TS}0005</MsgId><AgentID>1000002</AgentID></xml>`
    const before = convReady ? sentCount() : 0
    const r = await push(WID3, imgXml)
    await wait(convReady ? 3000 : 500)
    check('WX-20 非文本消息受理且不回复', r.status === 200 && (!convReady || sentCount() === before))
  }

  // ===== @WX-S8：回调防护 =====
  {
    const badSign = await push(WID3, textMsgXml({ fromUser: 'e', content: 'x', msgId: `${TS}0006` }), { token: 'wrong-token' })
    check('WX-21a Token 不符签名 400', badSign.status === 400, `status=${badSign.status}`)

    // receiveid 与连接企业 ID 不一致
    const other = buildPush(AES_KEY, TOKEN, 'other-corp', textMsgXml({ fromUser: 'e', content: 'x', msgId: `${TS}0007` }))
    const r = await fetch(`${BASE}/api/weixinwork_app/callback/${WID3}?msg_signature=${other.msgSignature}&timestamp=${other.timestamp}&nonce=${other.nonce}`, { method: 'POST', headers: { 'Content-Type': 'text/xml' }, body: other.body })
    check('WX-21b receiveid 与连接企业不符 400', r.status === 400, `status=${r.status}`)

    const missing = await fetch(`${BASE}/api/weixinwork_app/callback/${crypto.randomUUID()}?msg_signature=x&timestamp=1&nonce=n`, { method: 'POST', headers: { 'Content-Type': 'text/xml' }, body: '<xml><Encrypt><![CDATA[abc]]></Encrypt></xml>' })
    check('WX-21c 推送到不存在的连接 404', missing.status === 404, `status=${missing.status}`)
  }

  // ===== @WX-S9：禁用/未发布不回复 =====
  {
    // 禁用 appB 的连接（ww-p2）：受理但不转发
    const p2Id = recre.json.value
    await api('PUT', `/api/weixinwork_app/${p2Id}`, { token: owner.token, body: { name: 'ww-p2-' + TS, corpId: `wwcorp${TS}`, agentId: 4000002, isDisable: true } })
    const before = convReady ? sentCount() : 0
    const r = await push(p2Id, textMsgXml({ fromUser: 'zhangsan', content: '禁用期间', msgId: `${TS}0008` }))
    await wait(convReady ? 3000 : 500)
    check('WX-22a 禁用连接推送受理且不回复', r.status === 200 && (!convReady || sentCount() === before))

    // 未发布应用（appB）：启用连接后推送仍不回复（Agent 门禁）
    await api('PUT', `/api/weixinwork_app/${p2Id}`, { token: owner.token, body: { name: 'ww-p2-' + TS, corpId: `wwcorp${TS}`, agentId: 4000002, isDisable: false } })
    const before2 = convReady ? sentCount() : 0
    const r2 = await push(p2Id, textMsgXml({ fromUser: 'zhangsan', content: '未发布应用', msgId: `${TS}0009` }))
    await wait(convReady ? 4000 : 500)
    check('WX-22b 未发布应用推送不回复', r2.status === 200 && (!convReady || sentCount() === before2))
  }

  // ===== 清理 =====
  if (adminToken && chatModelId) {
    const channels = await api('GET', '/api/ai/channel', { token: adminToken })
    const ch = (channels.json?.items ?? []).find((c) => c.name === `ww-stub渠道-${TS}`)
    if (ch) await api('DELETE', `/api/ai/channel/${ch.id}`, { token: adminToken })
  }
  qyapi.server.close()
  modelStub.server.close()

  console.log(`\n==== WX 结果：PASS=${PASS} FAIL=${FAIL} SKIP=${SKIP} ====`)
  if (FAIL > 0) process.exit(1)
}

await main()
