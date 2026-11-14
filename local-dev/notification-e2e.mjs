// 通知中心 · E2E（场景 @NT-S1..）
// 覆盖：管理员门禁（401/403）/ 渠道 CRUD 与配置校验（七类渠道类型；SMTP/短信渠道收件人已移出渠道配置，
//       由发送时动态指定：试发带 recipients、广播带 emailRecipients/phoneNumbers（NT-114..119））/ 渠道测试同步试发（飞书加签、钉钉加签、
//       企业微信 markdown、Telegram sendMessage、通用 webhook 自定义头与模板渲染、SMTP 渠道自带连接配置（系统设置仅绑定）、站内推送
//       toAdmins）/ 广播异步投递（Hangfire 落发送记录：success/failed、禁用渠道不投递、渠道删除后历史保留、
//       发送记录过滤分页与关联用户快照回显（广播为空串：NT-36）与实际收件人快照（NT-119）/
//       通知模板（NT-80..：门禁/CRUD 校验/渲染预览与花括号字面保留/缺变量 400/内置模板 isBuiltIn 回显·删除保护·可编辑；占位符 {{变量}} 双花括号，与云邮件平台提审形态一致）/
//       阿里云·腾讯云短信渠道（NT-94..：配置校验/无模板直发失败/试发请求参数断言（POP 签名与 TC3 签名头、
//       TemplateParam JSON、TemplateParamSet 顺序）/平台错误码失败归一）/
//       HTML 模板（NT-106..107：SMTP 发 text/html、webhook 降级去标签）/
//       广播带模板（NT-108..：标题正文可空/缺变量 400/渲染文本落记录与站内信/模板删除不影响历史）/
//       应用机器人渠道（飞书/钉钉/企微 App 凭证按 oauth 绑定反查个人推送：NT-56..）/
//       站内信托底（NT-120..：TargetUserIds 定向通知站内信必达且先于外部渠道、与渠道成败解耦；
//       渠道试发改走领域服务——已保存渠道落发送记录留痕、试发人收站内信副本；未绑定渠道试发失败但站内信仍送达）。
// 前置：后端运行中；种子账号 admin / abcd123456（root 即管理员）。内置 webhook 收集桩与 SMTP 桩，零真实外部依赖。
// 应用机器人场景（NT-56..）额外前置：后端需把三平台登录客户端端点覆盖到本脚本的桩（固定端口 19081，可用
// NT_APPBOT_PORT 覆盖），启动示例：
//   MoAI__Feishu__Endpoint=http://127.0.0.1:19081 MoAI__DingTalk__Endpoint=http://127.0.0.1:19081 \
//   MoAI__WeixinWork__Endpoint=http://127.0.0.1:19081 ASPNETCORE_ENVIRONMENT=Development dotnet run
// 未覆盖端点时脚本自动跳过该段（探测 oauth_login 是否命中桩）。
// 用法：node local-dev/notification-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5000，可用 NT_BASE 覆盖）
import crypto from 'node:crypto'
import http from 'node:http'
import net from 'node:net'

const BASE = process.env.NT_BASE ?? process.argv[2] ?? 'http://127.0.0.1:5000'
let PASS = 0
let FAIL = 0
const check = (name, cond, detail = '') => {
  if (cond) {
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

async function mkuser(p) {
  const name = `${p}${Date.now().toString().slice(-8)}`
  const phone = `15${Date.now().toString().slice(-8)}${String(Math.floor(Math.random() * 90) + 10)}`.slice(0, 11)
  await api('POST', '/api/auth/register', { body: { userName: name, email: `${name}@test.local`, nickName: name, phone, password: rsa('Test1234') } })
  const l = await api('POST', '/api/auth/login', { body: { userName: name, password: rsa('Test1234') } })
  return { name, token: l.json.accessToken }
}

const sleep = (ms) => new Promise((r) => setTimeout(r, ms))

// ============ webhook 收集桩：按路径返回不同响应，记录请求供断言 ============
function startWebhookStub() {
  const state = { hits: [] } // { path, query, headers, raw }
  const server = http.createServer((req, res) => {
    let raw = ''
    req.on('data', (c) => { raw += c })
    req.on('end', () => {
      const hit = { path: req.url.split('?')[0], query: new URL(req.url, 'http://stub').searchParams, headers: req.headers, raw }
      state.hits.push(hit)
      const ok200 = () => { res.writeHead(200, { 'Content-Type': 'application/json' }); res.end('{"ok":true}') }
      switch (hit.path) {
        case '/feishu':
          res.writeHead(200, { 'Content-Type': 'application/json' })
          res.end('{"code":0,"msg":"success"}')
          break
        case '/feishu-bad':
          res.writeHead(200, { 'Content-Type': 'application/json' })
          res.end('{"code":19021,"msg":"sign match fail"}')
          break
        case '/dingtalk':
          res.writeHead(200, { 'Content-Type': 'application/json' })
          res.end('{"errcode":0,"errmsg":"ok"}')
          break
        case '/wecom':
          res.writeHead(200, { 'Content-Type': 'application/json' })
          res.end('{"errcode":0,"errmsg":"ok"}')
          break
        case hit.path === '/discord' ? hit.path : '/__none3__':
          res.writeHead(204)
          res.end()
          break
        case hit.path === '/push' ? hit.path : '/__none4__':
          res.writeHead(200, { 'Content-Type': 'application/json' })
          res.end('{"code":200,"message":"success"}')
          break
        case hit.path.endsWith('.send') ? hit.path : '/__none5__':
          res.writeHead(200, { 'Content-Type': 'application/json' })
          res.end('{"code":0,"message":"ok"}')
          break
        case hit.path === '/send' ? hit.path : '/__none6__':
          res.writeHead(200, { 'Content-Type': 'application/json' })
          res.end('{"code":200,"message":"ok"}')
          break
        case /^\/botWRONG/.test(hit.path) ? hit.path : '/__none__':
          res.writeHead(200, { 'Content-Type': 'application/json' })
          res.end('{"ok":false,"description":"Unauthorized"}')
          break
        case hit.path.startsWith('/bot') && hit.path.endsWith('/sendMessage') ? hit.path : '/__none2__':
          res.writeHead(200, { 'Content-Type': 'application/json' })
          res.end('{"ok":true,"result":{"message_id":1}}')
          break
        case '/fail':
          res.writeHead(500)
          res.end('boom')
          break
        case '/alisms':
          res.writeHead(200, { 'Content-Type': 'application/json' })
          res.end('{"Code":"OK","Message":"OK","BizId":"9006124000","RequestId":"nt-req-ali"}')
          break
        case '/alisms-bad':
          res.writeHead(200, { 'Content-Type': 'application/json' })
          res.end('{"Code":"isv.BUSINESS_LIMIT_CONTROL","Message":"触发业务流控","RequestId":"nt-req-alibad"}')
          break
        case '/tencent':
          res.writeHead(200, { 'Content-Type': 'application/json' })
          res.end('{"Response":{"SendStatusSet":[{"SerialNo":"100","PhoneNumber":"13800138000","Fee":1,"SessionContext":"","Code":"Ok","Message":"send success"},{"SerialNo":"101","PhoneNumber":"13800138001","Fee":1,"SessionContext":"","Code":"Ok","Message":"send success"}],"RequestId":"nt-req-tc"}}')
          break
        case '/tencent-bad':
          res.writeHead(200, { 'Content-Type': 'application/json' })
          res.end('{"Response":{"SendStatusSet":[{"SerialNo":"102","PhoneNumber":"13800138000","Fee":0,"SessionContext":"","Code":"LimitExceeded.PhoneNumberLimit","Message":"发送数量超限"},{"SerialNo":"103","PhoneNumber":"13800138001","Fee":1,"SessionContext":"","Code":"Ok","Message":"send success"}],"RequestId":"nt-req-tcbad"}}')
          break
        case '/tencent-error':
          res.writeHead(200, { 'Content-Type': 'application/json' })
          res.end('{"Response":{"Error":{"Code":"AuthFailure.SignatureFailure","Message":"TC3-HMAC-SHA256 signature failure"},"RequestId":"nt-req-tcerr"}}')
          break
        default:
          ok200()
      }
    })
  })
  return new Promise((resolve) => server.listen(0, '127.0.0.1', () => resolve({ server, state, port: server.address().port })))
}

// ============ SMTP 桩（简化版，无认证）：记录信封与 DATA ============
function startSmtpStub() {
  const state = { messages: [] }
  const server = net.createServer((socket) => {
    let buffer = ''
    let inData = false
    let current = null
    const reply = (line) => socket.write(line + '\r\n')
    reply('220 stub ESMTP')
    socket.on('error', () => {})
    socket.on('data', (chunk) => {
      buffer += chunk.toString('utf-8')
      let idx
      while ((idx = buffer.indexOf('\r\n')) >= 0) {
        const line = buffer.slice(0, idx)
        buffer = buffer.slice(idx + 2)
        if (inData) {
          if (line === '.') { inData = false; state.messages.push(current); current = null; reply('250 OK queued') } else { current.data += line + '\r\n' }
          continue
        }
        const cmd = line.toUpperCase()
        if (cmd.startsWith('EHLO') || cmd.startsWith('HELO')) { reply('250-stub'); reply('250 OK') } else if (cmd.startsWith('QUIT')) { reply('221 bye'); socket.end() } else if (cmd.startsWith('MAIL FROM:')) { current = { from: line, rcpts: [], data: '' }; reply('250 OK') } else if (cmd.startsWith('RCPT TO:')) { current?.rcpts.push(line); reply('250 OK') } else if (cmd.startsWith('DATA')) { inData = true; reply('354 end with .') } else { reply('250 OK') }
      }
    })
  })
  return new Promise((resolve) => server.listen(0, '127.0.0.1', () => resolve({ server, state, port: server.address().port })))
}

const hmacB64 = (key, data) => crypto.createHmac('sha256', Buffer.from(key, 'utf8')).update(Buffer.from(data, 'utf8')).digest('base64')

// MailKit 对 text/html 正文常做 quoted-printable 或 base64 编码：解码后拼回原文，便于断言邮件正文
function decodeMailBody(data) {
  let text = String(data)
  if (/Content-Transfer-Encoding:\s*quoted-printable/i.test(text)) {
    const headEnd = text.indexOf('\r\n\r\n')
    const head = text.slice(0, headEnd)
    let body = text.slice(headEnd + 4).replace(/=\r\n/g, '')
    const bytes = []
    for (let i = 0; i < body.length; i++) {
      if (body[i] === '=' && /^[0-9A-Fa-f]{2}$/.test(body.slice(i + 1, i + 3))) {
        bytes.push(parseInt(body.slice(i + 1, i + 3), 16))
        i += 2
      } else {
        bytes.push(body.charCodeAt(i) & 0xff)
      }
    }
    text = `${head}\r\n\r\n${Buffer.from(bytes).toString('utf8')}`
  }

  const lines = text.split('\r\n')
  const out = []
  for (let i = 0; i < lines.length; i++) {
    if (/^Content-Transfer-Encoding:\s*base64/i.test(lines[i])) {
      let j = i + 1
      if (j < lines.length && lines[j].trim() === '') j++ // 跳过头部与正文间空行
      let b64 = ''
      while (j < lines.length && lines[j].trim() !== '') { b64 += lines[j]; j++ }
      try { out.push(Buffer.from(b64, 'base64').toString('utf8')) } catch { /* 非法 base64 按原样 */ }
      i = j - 1
      continue
    }
    out.push(lines[i])
  }
  return out.join('\r\n')
}

// ============ 应用机器人桩：三平台登录侧（token/用户信息）+ 推送侧（应用机器人单聊）二合一 ============
// 登录换码：oauth_login 传 code=ntcode|<platform>|<who>|<RUN>，桩据此派生平台侧用户标识——
//   飞书 open_id=ou-<who>-<RUN> / union_id=un-<who>-<RUN>；钉钉 unionId=dtun-<who>-<RUN>（push 侧换算 dtstaff-<who>-<RUN>）；
//   企微 userid=wxuid-<who>-<RUN>。getbyunionid 可经 /__control/unionid_fail 切换全失败（模拟通知应用跨企业）。
function startAppBotStub(port) {
  const state = { hits: [], failUnionId: false, lastFeishuCode: '', lastDingCode: '' }
  const server = http.createServer((req, res) => {
    let raw = ''
    req.on('data', (c) => { raw += c })
    req.on('end', () => {
      const hit = { path: req.url.split('?')[0], query: new URL(req.url, 'http://stub').searchParams, headers: req.headers, raw }
      state.hits.push(hit)
      const json = (obj) => { res.writeHead(200, { 'Content-Type': 'application/json' }); res.end(JSON.stringify(obj)) }
      const body = (() => { try { return JSON.parse(raw || '{}') } catch { return {} } })()
      if (hit.path === '/__control/unionid_fail') { state.failUnionId = raw.includes('true'); return json({ ok: true }) }
      switch (hit.path) {
        // ---- 登录侧：飞书 ----
        case '/open-apis/authen/v2/oauth/token':
          state.lastFeishuCode = String(body.code ?? '')
          return json({ code: 0, access_token: 'nt-stub-feishu-user-token', expires_in: 7200 })
        case '/open-apis/authen/v1/user_info': {
          const parts = String(state.lastFeishuCode).split('|')
          return json({ code: 0, data: { name: `NT-${parts[2] ?? 'x'}`, open_id: `ou-${parts[2] ?? 'x'}-${parts[3] ?? 'x'}`, union_id: `un-${parts[2] ?? 'x'}-${parts[3] ?? 'x'}` } })
        }
        // ---- 登录侧：钉钉 ----
        case '/v1.0/oauth2/userAccessToken':
          state.lastDingCode = String(body.code ?? '')
          return json({ access_token: 'nt-stub-ding-user-token', expireIn: 7200 })
        case '/v1.0/contact/users/me': {
          const parts = String(state.lastDingCode).split('|')
          return json({ nick: `NT-${parts[2] ?? 'x'}`, unionId: `dtun-${parts[2] ?? 'x'}-${parts[3] ?? 'x'}` })
        }
        // ---- 登录侧 + 推送侧：企业微信 gettoken 共用 ----
        case '/cgi-bin/gettoken':
          return json({ errcode: 0, errmsg: 'ok', access_token: 'nt-stub-wx-token', expires_in: 7200 })
        case '/cgi-bin/auth/getuserinfo': {
          const parts = String(body.code ?? '').split('|')
          return json({ errcode: 0, userid: `wxuid-${parts[2] ?? 'x'}-${parts[3] ?? 'x'}` })
        }
        // ---- 推送侧：飞书应用机器人 ----
        case '/open-apis/auth/v3/tenant_access_token/internal':
          return json({ code: 0, msg: 'ok', tenant_access_token: 'nt-stub-tenant-token', expire: 7200 })
        case '/open-apis/im/v1/messages':
          return json({ code: 0, msg: 'success' })
        // ---- 推送侧：钉钉工作通知 ----
        case '/gettoken':
          return json({ errcode: 0, errmsg: 'ok', access_token: 'nt-stub-dt-token' })
        case '/topapi/user/getbyunionid': {
          if (state.failUnionId) return json({ errcode: 60111, errmsg: 'userid not found' })
          const unionid = String(body.unionid ?? '')
          return json({ errcode: 0, result: { userid: unionid.replace(/^dtun-/, 'dtstaff-') } })
        }
        case '/topapi/message/corpconversation/asyncsend_v2':
          return json({ errcode: 0, errmsg: 'ok', task_id: 1 })
        // ---- 推送侧：企业微信应用消息 ----
        case '/cgi-bin/message/send':
          return json({ errcode: 0, errmsg: 'ok' })
        default:
          return json({ errcode: 400, errmsg: `unexpected path ${hit.path}` })
      }
    })
  })
  return new Promise((resolve, reject) => server.once('error', reject).listen(port, '127.0.0.1', () => resolve({ server, state, port })))
}

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic
  const l = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
  if (l.status !== 200) throw new Error('种子账号 admin 登录失败')
  const root = l.json.accessToken
  const member = await mkuser('nt')

  const hook = await startWebhookStub()
  const smtp = await startSmtpStub()
  const STUB = `http://127.0.0.1:${hook.port}`
  const RUN = Date.now().toString().slice(-6) // 每次运行的唯一标识：渠道命名/广播标题隔离，可重复执行
  const chName = (s) => `nt${RUN}-${s}`

  // 应用机器人桩：固定端口（后端端点覆盖需指向同端口）；端口被占则直接失败并提示
  const APPBOT_PORT = Number(process.env.NT_APPBOT_PORT ?? 19081)
  let appbot = null
  try {
    appbot = await startAppBotStub(APPBOT_PORT)
  } catch (e) {
    console.error(`\nSKIP | 应用机器人桩端口 ${APPBOT_PORT} 被占用（${e.message}）：换端口需同步调整后端 MoAI:Feishu/DingTalk/WeixinWork:Endpoint 覆盖`)
    process.exit(2)
  }
  const APPBOT = `http://127.0.0.1:${APPBOT_PORT}`

  // SMTP 系统绑定快照（结束恢复）：渠道自带连接配置后，渠道发信不再依赖系统设置，此处仅用于证明独立性
  const SMTP_KEYS = ['SMTP_ENABLED', 'SMTP_CHANNEL_ID']
  const getSettingValue = async (key) => {
    const q = await api('GET', '/api/settings', { token: root })
    return (q.json?.items ?? []).find((s) => s.key === key)?.value
  }
  const ORIG = {}
  for (const key of SMTP_KEYS) ORIG[key] = await getSettingValue(key)

  // ============ 门禁 ============
  check('NT-01 未登录查询渠道 401', (await api('GET', '/api/notification/channel/list')).status === 401)
  check('NT-02 非管理员查询渠道 403', (await api('GET', '/api/notification/channel/list', { token: member.token })).status === 403)
  // 请求体需通过模型验证，门禁在 Controller 层后置校验
  check('NT-03 非管理员创建渠道 403', (await api('POST', '/api/notification/channel/create', { token: member.token, body: { name: chName('x'), type: 'webhook', config: JSON.stringify({ url: `${STUB}/generic` }) } })).status === 403)
  check('NT-04 非管理员广播 403', (await api('POST', '/api/notification/broadcast', { token: member.token, body: { title: 't', content: 'c', sendInSite: true } })).status === 403)

  // ============ 渠道创建与校验 ============
  const create = (name, type, config, enabled = true) => api('POST', '/api/notification/channel/create', { token: root, body: { name, type, config: JSON.stringify(config), enabled, description: `e2e ${name}` } })
  const mk = async (name, type, config, enabled) => {
    const r = await create(name, type, config, enabled)
    if (r.status !== 200) throw new Error(`创建渠道 ${name} 失败: ${r.status} ${r.text.slice(0, 200)}`)
    return r.json.id
  }

  const feishuId = await mk(chName('飞书'), 'feishuWebhook', { webhookUrl: `${STUB}/feishu`, secret: 'ntFeishuSecret' })
  const dingId = await mk(chName('钉钉'), 'dingtalkWebhook', { webhookUrl: `${STUB}/dingtalk`, secret: 'SECntDingtalk' })
  const wecomId = await mk(chName('企微'), 'wecomWebhook', { webhookUrl: `${STUB}/wecom` })
  const tgId = await mk(chName('TG'), 'telegramBot', { baseUrl: STUB, botToken: 'NTSTUBTOKEN123', chatId: '@moai_nt' })
  const genericId = await mk(chName('通用'), 'webhook', { url: `${STUB}/generic`, headers: { 'X-Token': 'abc123' } })
  const tplId = await mk(chName('模板'), 'webhook', { url: `${STUB}/generic`, bodyTemplate: '{"t":"{title}","c":"{content}","lv":"{level}","s":"{source}"}', headers: { 'Content-Type': 'application/json' } })
  const smtpId = await mk(chName('邮件'), 'smtp', { host: '127.0.0.1', port: smtp.port, security: 'none', username: '', password: '', fromAddress: 'moai@nt.local', fromName: 'NT E2E' })
  const insiteId = await mk(chName('站内'), 'inSite', { userIds: [], toAdmins: true })
  const failId = await mk(chName('失败'), 'webhook', { url: `${STUB}/fail` })
  const badFeishuId = await mk(chName('坏飞书'), 'feishuWebhook', { webhookUrl: `${STUB}/feishu-bad` })
  const slackId = await mk(chName('Slack'), 'slack', { webhookUrl: `${STUB}/slack` })
  const discordId = await mk(chName('Discord'), 'discord', { webhookUrl: `${STUB}/discord` })
  const teamsId = await mk(chName('Teams'), 'microsoftTeams', { webhookUrl: `${STUB}/teams` })
  const gotifyId = await mk(chName('Gotify'), 'gotify', { baseUrl: STUB, token: 'gotify-token' })
  const ntfyId = await mk(chName('ntfy'), 'ntfy', { baseUrl: STUB, topic: 'moai-nt' })
  const barkId = await mk(chName('Bark'), 'bark', { baseUrl: STUB, deviceKey: 'bark-key' })
  const scId = await mk(chName('ServerChan'), 'serverChan', { baseUrl: STUB, sendKey: 'SCkey123' })
  const ppId = await mk(chName('PushPlus'), 'pushPlus', { baseUrl: STUB, token: 'pp-token' })
  const createdIds = [feishuId, dingId, wecomId, tgId, genericId, tplId, smtpId, insiteId, failId, badFeishuId, slackId, discordId, teamsId, gotifyId, ntfyId, barkId, scId, ppId]

  check('NT-05 创建飞书渠道返回 id', Number(feishuId) > 0)
  const dup = await create(chName('飞书'), 'feishuWebhook', { webhookUrl: `${STUB}/feishu` })
  check('NT-06 重名 400', dup.status === 400, `${dup.status}`)
  const badUrl = await create(chName('坏URL'), 'webhook', { url: 'not-a-url' })
  check('NT-07 非法 url 400', badUrl.status === 400, `${badUrl.status}`)
  const badJson = await api('POST', '/api/notification/channel/create', { token: root, body: { name: chName('坏JSON'), type: 'webhook', config: '{oops' } })
  check('NT-08 配置非法 JSON 400', badJson.status === 400, `${badJson.status}`)
  const noChat = await create(chName('无chatId'), 'telegramBot', { botToken: 'x' })
  check('NT-09 telegram 缺 chatId 400', noChat.status === 400, `${noChat.status}`)
  const noTarget = await create(chName('无收件人'), 'inSite', { userIds: [], toAdmins: false })
  check('NT-11 inSite 无收件人 400', noTarget.status === 400, `${noTarget.status}`)

  const list1 = await api('GET', `/api/notification/channel/list?search=${RUN}`, { token: root })
  const items = list1.json?.items ?? []
  check('NT-12 列表回显 18 渠道（含审计人名）', items.length === 18 && items.every((x) => x.createUserName?.length > 0), `got ${items.length}`)
  check('NT-13 search 过滤命中', items.every((x) => x.name.includes(RUN)) && items.some((x) => x.name === chName('飞书')))

  // NT-10（smtp 无效邮箱 400）随收件人移出渠道配置退役，编号不复用
  const noRcpt = await create(chName('无收件人邮件'), 'smtp', { host: '127.0.0.1', port: 25, security: 'none' })
  check('NT-114 smtp 渠道创建不再要求收件人（发送时动态指定）', noRcpt.status === 200, `${noRcpt.status}`)
  createdIds.push(noRcpt.json?.id)

  // ============ 更新与删除校验 ============
  const upd = await api('PUT', `/api/notification/channel/${failId}`, { token: root, body: { name: chName('失败2号'), type: 'webhook', config: JSON.stringify({ url: `${STUB}/fail` }), enabled: true, description: 'renamed' } })
  check('NT-14 更新成功改名', upd.status === 200 && (await api('GET', '/api/notification/channel/list', { token: root })).json.items.find((x) => String(x.id) === String(failId))?.name === chName('失败2号'), `upd=${upd.status}`)
  const updType = await api('PUT', `/api/notification/channel/${failId}`, { token: root, body: { name: chName('失败2号'), type: 'feishuWebhook', config: JSON.stringify({ webhookUrl: `${STUB}/feishu` }), enabled: true, description: '' } })
  check('NT-15 更新改类型 400', updType.status === 400, `${updType.status}`)
  const updDup = await api('PUT', `/api/notification/channel/${failId}`, { token: root, body: { name: chName('钉钉'), type: 'webhook', config: JSON.stringify({ url: `${STUB}/fail` }), enabled: true, description: '' } })
  check('NT-16 更新重名冲突 400', updDup.status === 400, `${updDup.status}`)
  check('NT-17 更新不存在 404', (await api('PUT', '/api/notification/channel/99999999', { token: root, body: { name: 'x', type: 'webhook', config: JSON.stringify({ url: `${STUB}/fail` }), enabled: true, description: '' } })).status === 404)
  check('NT-18 删除不存在 404', (await api('DELETE', '/api/notification/channel/99999999', { token: root })).status === 404)

  // ============ 渠道测试（同步试发） ============
  hook.state.hits.length = 0
  const testCh = (body) => api('POST', '/api/notification/channel/test', { token: root, body })
  const t1 = await testCh({ id: feishuId })
  const feishuHit = hook.state.hits.find((h) => h.path === '/feishu')
  let feishuPayload = null
  try { feishuPayload = JSON.parse(feishuHit?.raw ?? 'null') } catch { /* ignore */ }
  const feishuSignOk = feishuPayload?.sign === hmacB64(`${feishuPayload?.timestamp}\nntFeishuSecret`, '')
  check('NT-19 test 飞书成功且验签通过', t1.json?.success === true && feishuPayload?.msg_type === 'text' && feishuPayload?.timestamp?.length > 0 && feishuSignOk, JSON.stringify(feishuPayload)?.slice(0, 160))
  const t2 = await testCh({ type: 'dingtalkWebhook', config: JSON.stringify({ webhookUrl: `${STUB}/dingtalk`, secret: 'SECntDingtalk' }) })
  const dingHit = hook.state.hits.filter((h) => h.path === '/dingtalk').pop()
  const dingTs = dingHit?.query.get('timestamp')
  const dingSignOk = dingHit && dingHit.query.get('sign') === hmacB64('SECntDingtalk', `${dingTs}\nSECntDingtalk`)
  check('NT-20 test 钉钉试发加签通过', t2.json?.success === true && dingSignOk, `${t2.text?.slice(0, 120)} sign=${dingHit?.query.get('sign')}`)
  const t3 = await testCh({ id: wecomId })
  const wecomPayload = JSON.parse(hook.state.hits.filter((h) => h.path === '/wecom').pop()?.raw ?? '{}')
  check('NT-21 test 企微 markdown', t3.json?.success === true && wecomPayload?.msgtype === 'markdown' && String(wecomPayload?.markdown?.content).includes('**'), JSON.stringify(wecomPayload)?.slice(0, 120))
  const t4 = await testCh({ id: tgId })
  const tgHit = hook.state.hits.filter((h) => h.path === '/botNTSTUBTOKEN123/sendMessage').pop()
  const tgPayload = JSON.parse(tgHit?.raw ?? '{}')
  check('NT-22 test telegram sendMessage', t4.json?.success === true && tgPayload?.chat_id === '@moai_nt' && String(tgPayload?.text).includes('【MoAI】'), JSON.stringify(tgPayload)?.slice(0, 120))
  hook.state.hits.length = 0
  const t5 = await testCh({ id: genericId })
  const genericHit = hook.state.hits.find((h) => h.path === '/generic')
  const genericPayload = JSON.parse(genericHit?.raw ?? '{}')
  check('NT-23 test 通用 webhook 默认结构与自定义头', t5.json?.success === true && genericPayload?.title?.length > 0 && genericPayload?.level === 'info' && genericHit?.headers['x-token'] === 'abc123', JSON.stringify(genericPayload)?.slice(0, 160))
  const t6 = await testCh({ type: 'webhook', config: JSON.stringify({ url: `${STUB}/generic`, bodyTemplate: '{"t":"{title}","c":"{content}","lv":"{level}","s":"{source}"}' }) })
  const tplPayload = JSON.parse(hook.state.hits.filter((h) => h.path === '/generic').pop()?.raw ?? '{}')
  check('NT-24 test 通用 webhook 模板渲染', t6.json?.success === true && tplPayload?.lv === 'info' && String(tplPayload?.t).includes('测试') && tplPayload?.s === 'notification', JSON.stringify(tplPayload)?.slice(0, 160))
  const t7 = await testCh({ id: badFeishuId })
  check('NT-25 test 飞书 code!=0 失败回显', t7.json?.success === false && String(t7.json?.error).includes('sign match fail'), t7.text?.slice(0, 160))
  const t8 = await testCh({ id: failId })
  check('NT-26 test webhook 500 失败回显', t8.json?.success === false && String(t8.json?.error).includes('500'), t8.text?.slice(0, 160))
  const t9 = await testCh({ type: 'telegramBot', config: JSON.stringify({ baseUrl: STUB, botToken: 'WRONGTOKEN', chatId: '@x' }) })
  check('NT-27 test telegram 错 token 失败回显', t9.json?.success === false && String(t9.json?.error).includes('Unauthorized'), t9.text?.slice(0, 160))
  check('NT-28 test 不存在渠道 404', (await testCh({ id: 99999999 })).status === 404)

  // SMTP：渠道自带连接配置（复用系统设置已被自包含连接取代），直连本地 SMTP 桩；收件人发送时动态指定
  // NT-29b 证明独立性：系统「邮箱验证」关闭时渠道发信仍成功
  await api('PUT', '/api/settings', { token: root, body: { key: 'SMTP_ENABLED', value: 'false' } })
  const t10 = await testCh({ id: smtpId, recipients: ['nt@test.local'] })
  check('NT-29 test SMTP 自带连接直达桩（系统设置关闭仍可发）', t10.json?.success === true, t10.text?.slice(0, 160))
  smtp.state.messages.length = 0
  const t11 = await testCh({ id: smtpId, recipients: ['nt@test.local'] })
  const smtpMsg = smtp.state.messages.pop()
  check('NT-30 test SMTP 桩捕获收件人与主题', t11.json?.success === true && smtpMsg?.rcpts.join(',').includes('nt@test.local') && smtpMsg?.data.includes('Subject:'), JSON.stringify({ err: t11.json?.error, rcpts: smtpMsg?.rcpts })?.slice(0, 200))
  const t12 = await testCh({ id: insiteId })
  check('NT-31 test 站内推送 toAdmins 成功', t12.json?.success === true, t12.text?.slice(0, 160))

  // 新增八类渠道试发（Slack/Discord/Teams/Gotify/ntfy/Bark/Server酱/PushPlus）
  const tSlack = await testCh({ id: slackId })
  const slackPayload = JSON.parse(hook.state.hits.filter((h) => h.path === '/slack').pop()?.raw ?? '{}')
  check('NT-48 test Slack 文本载荷', tSlack.json?.success === true && String(slackPayload?.text).includes('【MoAI】'), tSlack.text?.slice(0, 160))
  const tDiscord = await testCh({ id: discordId })
  check('NT-49 test Discord（204 亦算成功）', tDiscord.json?.success === true && hook.state.hits.some((h) => h.path === '/discord'), tDiscord.text?.slice(0, 160))
  const tTeams = await testCh({ id: teamsId })
  const teamsPayload = JSON.parse(hook.state.hits.filter((h) => h.path === '/teams').pop()?.raw ?? '{}')
  check('NT-50 test Teams 文本载荷', tTeams.json?.success === true && String(teamsPayload?.text).includes('【MoAI】'), tTeams.text?.slice(0, 160))
  const tGotify = await testCh({ id: gotifyId })
  const gotifyHit = hook.state.hits.filter((h) => h.path === '/message').pop()
  const gotifyPayload = JSON.parse(gotifyHit?.raw ?? '{}')
  check('NT-51 test Gotify token 查询参数与优先级', tGotify.json?.success === true && gotifyHit?.query.get('token') === 'gotify-token' && gotifyPayload?.priority === 3 && String(gotifyPayload?.title).includes('【MoAI】'), tGotify.text?.slice(0, 160))
  const tNtfy = await testCh({ id: ntfyId })
  const ntfyPayload = JSON.parse(hook.state.hits.filter((h) => h.path === '/').pop()?.raw ?? '{}')
  check('NT-52 test ntfy 根路径 JSON 模式', tNtfy.json?.success === true && ntfyPayload?.topic === 'moai-nt' && ntfyPayload?.priority === 3 && String(ntfyPayload?.title).includes('【MoAI】'), tNtfy.text?.slice(0, 160))
  const tBark = await testCh({ id: barkId })
  const barkPayload = JSON.parse(hook.state.hits.filter((h) => h.path === '/push').pop()?.raw ?? '{}')
  check('NT-53 test Bark 设备键与分组', tBark.json?.success === true && barkPayload?.device_key === 'bark-key' && barkPayload?.group === 'MoAI', tBark.text?.slice(0, 160))
  const tSc = await testCh({ id: scId })
  const scRaw = hook.state.hits.filter((h) => h.path === '/SCkey123.send').pop()?.raw ?? ''
  check('NT-54 test Server酱 form 载荷', tSc.json?.success === true && scRaw.includes('title=') && decodeURIComponent(scRaw).includes('【MoAI】'), tSc.text?.slice(0, 160))
  const tPp = await testCh({ id: ppId })
  const ppPayload = JSON.parse(hook.state.hits.filter((h) => h.path === '/send').pop()?.raw ?? '{}')
  check('NT-55 test PushPlus token 与模板', tPp.json?.success === true && ppPayload?.token === 'pp-token' && ppPayload?.template === 'txt', tPp.text?.slice(0, 160))

  // ============ 站内信托底：渠道试发改走领域服务（落发送记录留痕 + 试发人站内信必达） ============
  // 已保存渠道试发（如 NT-26 对失败渠道的试发）现在落 notification_log；站内信在领域服务内先行同步发送，响应返回时已写入
  const testFailLog = ((await api('GET', `/api/notification/log/list?pageNo=1&pageSize=50&channelId=${failId}`, { token: root })).json?.items ?? [])
    .find((x) => x.title === `通知渠道测试 - ${chName('失败2号')}` && x.status === 'failed') ?? null
  check('NT-120 已保存渠道试发落发送记录留痕（含定向用户快照）', testFailLog != null && String(testFailLog?.targetUserIds ?? '').length > 0, JSON.stringify(testFailLog)?.slice(0, 200))
  let testInSite = false
  for (let i = 0; i < 5 && !testInSite; i++) {
    await sleep(1000)
    const ml = await api('GET', '/api/message/list?pageNo=1&pageSize=50', { token: root })
    testInSite = (ml.json?.items ?? []).some((x) => x.title?.startsWith('通知渠道测试') && String(x.content).includes('收到即表示渠道配置可用'))
  }
  check('NT-121 渠道试发站内信托底送达试发人', testInSite)

  // ============ 广播（异步 Hangfire；渠道含 SMTP 需动态邮箱收件人） ============
  const enabledCount = (await api('GET', '/api/notification/channel/list', { token: root })).json.items.filter((x) => x.enabled).length
  const bc = await api('POST', '/api/notification/broadcast', { token: root, body: { title: `NT${RUN} 广播一`, content: '通知中心广播正文', level: 'warning', allChannels: true, emailRecipients: ['nt@test.local'] } })
  check('NT-32 广播返回启用渠道数', bc.status === 200 && bc.json?.channelCount === enabledCount, `expect ${enabledCount} got ${bc.json?.channelCount}`)

  // 轮询发送记录终态（Hangfire 调度间隔 10s，最长等 120s）
  let logs = []
  let settled = false
  const failNames = new Set([chName('失败2号'), chName('坏飞书')])
  const okNames = new Set([
    chName('飞书'), chName('钉钉'), chName('企微'), chName('TG'), chName('通用'), chName('模板'), chName('邮件'), chName('站内'),
    chName('Slack'), chName('Discord'), chName('Teams'), chName('Gotify'), chName('ntfy'), chName('Bark'), chName('ServerChan'), chName('PushPlus'),
  ])
  for (let i = 0; i < 24; i++) {
    await sleep(5000)
    const r = await api('GET', '/api/notification/log/list?pageNo=1&pageSize=100', { token: root })
    logs = r.json?.items ?? []
    const bcs = logs.filter((x) => x.title?.startsWith(`NT${RUN} 广播一`))
    if (bcs.length >= enabledCount && bcs.every((x) => x.status !== 'pending')) { settled = true; break }
  }
  const bcLogs = logs.filter((x) => x.title?.startsWith(`NT${RUN} 广播一`))
  check('NT-33 广播每启用渠道一条记录', settled && bcLogs.length === enabledCount, `settled=${settled} logs=${bcLogs.length}/${enabledCount}`)
  const okSettled = bcLogs.filter((x) => okNames.has(x.channelName)).every((x) => x.status === 'success')
  const failSettled = bcLogs.filter((x) => failNames.has(x.channelName)).every((x) => x.status === 'failed')
  check('NT-34 十六渠道投递成功', okSettled && bcLogs.filter((x) => okNames.has(x.channelName)).length === okNames.size, JSON.stringify(bcLogs.map((x) => [x.channelName, x.status])))
  check('NT-35 失败渠道终态 failed 且带原因', failSettled && bcLogs.filter((x) => failNames.has(x.channelName)).every((x) => x.status === 'failed' && x.error.length > 0), JSON.stringify(bcLogs.filter((x) => failNames.has(x.channelName)).map((x) => [x.channelName, x.error.slice(0, 60)])))
  const feishuBc = bcLogs.find((x) => x.channelName === chName('飞书'))
  check('NT-36 记录快照含类型/级别/耗时且广播无关联用户', feishuBc && feishuBc.channelType === 'feishuWebhook' && feishuBc.level === 'warning' && feishuBc.durationMs >= 0 && !feishuBc.targetUserIds && !feishuBc.targetUserNames)
  const smtpBc = bcLogs.find((x) => x.channelName === chName('邮件'))
  check('NT-119 发送记录快照实际收件人邮箱（SMTP）/其余渠道留空', smtpBc?.recipients === 'nt@test.local' && (!feishuBc?.recipients || feishuBc.recipients.length === 0), JSON.stringify({ smtp: smtpBc?.recipients, feishu: feishuBc?.recipients }))

  // 过滤与分页
  const byChannel = await api('GET', `/api/notification/log/list?pageNo=1&pageSize=50&channelId=${feishuId}`, { token: root })
  check('NT-37 记录按渠道过滤', byChannel.json?.items?.length >= 1 && byChannel.json.items.every((x) => String(x.channelId) === String(feishuId)))
  const byStatus = await api('GET', '/api/notification/log/list?pageNo=1&pageSize=100&status=failed', { token: root })
  check('NT-38 记录按状态过滤', (byStatus.json?.items ?? []).length >= 2 && byStatus.json.items.every((x) => x.status === 'failed'))

  // 菜单角标：发送失败记录数（@NT-S35，此时广播一已产生失败记录）
  check('NT-123a 未登录查失败数量 401', (await api('GET', '/api/notification/log/failed-count')).status === 401)
  check('NT-123b 普通用户查失败数量 403', (await api('GET', '/api/notification/log/failed-count', { token: member.token })).status === 403)
  const fcRoot = await api('GET', '/api/notification/log/failed-count', { token: root })
  check('NT-123c 失败数量与记录列表 totalCount 一致', fcRoot.status === 200 && fcRoot.json?.count === byStatus.json?.totalCount, `count=${fcRoot.json?.count} total=${byStatus.json?.totalCount}`)

  // 禁用渠道不投递
  await api('PUT', `/api/notification/channel/${failId}`, { token: root, body: { name: chName('失败2号'), type: 'webhook', config: JSON.stringify({ url: `${STUB}/fail` }), enabled: false, description: '' } })
  const bc2 = await api('POST', '/api/notification/broadcast', { token: root, body: { title: `NT${RUN} 广播二`, content: '禁用后不投递', level: 'info', allChannels: true, emailRecipients: ['nt@test.local'], sendInSite: true } })
  check('NT-39 禁用后广播渠道数减一', bc2.json?.channelCount === enabledCount - 1, `expect ${enabledCount - 1} got ${bc2.json?.channelCount}`)

  // 站内信收到广播
  let inSiteHit = false
  for (let i = 0; i < 24 && !inSiteHit; i++) {
    await sleep(5000)
    const ml = await api('GET', '/api/message/list?pageNo=1&pageSize=50', { token: root })
    inSiteHit = (ml.json?.items ?? []).some((x) => x.title?.startsWith(`NT${RUN} 广播二`))
  }
  check('NT-40 站内推送渠道送达站内信', inSiteHit)

  // 删除渠道：列表移除、test 404、历史记录保留
  const del = await api('DELETE', `/api/notification/channel/${tplId}`, { token: root })
  const listAfter = await api('GET', '/api/notification/channel/list', { token: root })
  const logsAfter = await api('GET', '/api/notification/log/list?pageNo=1&pageSize=100', { token: root })
  check('NT-41 删除渠道生效且历史保留', del.status === 200 && !(listAfter.json.items ?? []).some((x) => String(x.id) === String(tplId)) && (logsAfter.json.items ?? []).some((x) => String(x.channelId) === String(tplId)))

  // 空标题 400
  check('NT-42 广播空标题 400', (await api('POST', '/api/notification/broadcast', { token: root, body: { title: '', content: 'x' } })).status === 400)

  // ============ 广播目标组合（渠道多选 / 仅站内信 / 均不选） ============
  const bc3 = await api('POST', '/api/notification/broadcast', { token: root, body: { title: `NT${RUN} 广播三`, content: '仅飞书渠道', level: 'info', channelIds: [String(feishuId)] } })
  check('NT-43 广播指定单渠道 channelCount=1', bc3.status === 200 && bc3.json?.channelCount === 1, `expect 1 got ${bc3.json?.channelCount}`)
  let bc3Log = false
  for (let i = 0; i < 10 && !bc3Log; i++) {
    await sleep(4000)
    const r = await api('GET', `/api/notification/log/list?pageNo=1&pageSize=20&channelId=${feishuId}`, { token: root })
    bc3Log = (r.json?.items ?? []).some((x) => x.title?.startsWith(`NT${RUN} 广播三`) && x.status === 'success')
  }
  check('NT-44 指定渠道广播落发送记录', bc3Log)
  const bc4 = await api('POST', '/api/notification/broadcast', { token: root, body: { title: `NT${RUN} 广播四`, content: '仅站内信', level: 'info', sendInSite: true } })
  check('NT-45 仅站内信广播 channelCount=0', bc4.status === 200 && bc4.json?.channelCount === 0, JSON.stringify(bc4.json))
  let inSiteOnlyHit = false
  for (let i = 0; i < 12 && !inSiteOnlyHit; i++) {
    await sleep(3000)
    const ml = await api('GET', '/api/message/list?pageNo=1&pageSize=50', { token: root })
    inSiteOnlyHit = (ml.json?.items ?? []).some((x) => x.title?.startsWith(`NT${RUN} 广播四`))
  }
  check('NT-46 仅站内信广播送达站内信', inSiteOnlyHit)
  check('NT-47 渠道与站内信均不选 400', (await api('POST', '/api/notification/broadcast', { token: root, body: { title: `NT${RUN} 广播五`, content: '无目标', level: 'info' } })).status === 400)
  check('NT-115 广播含 SMTP 渠道缺邮箱收件人 400', (await api('POST', '/api/notification/broadcast', { token: root, body: { title: `NT${RUN} 广播五b`, content: '缺收件人', level: 'info', allChannels: true } })).status === 400)

  // ============ 通知模板：门禁 / CRUD 校验 / 渲染预览 ============
  const tcreate = (body) => api('POST', '/api/notification/template/create', { token: root, body })
  const mkTpl = async (payload) => {
    const r = await tcreate(payload)
    if (r.status !== 200) throw new Error(`创建模板 ${payload.name} 失败: ${r.status} ${r.text.slice(0, 200)}`)
    return r.json.id
  }
  const tplText = {
    name: chName('文本模板'),
    title: `NT${RUN} 模板标题 {{site}}`,
    content: '服务 {{site}} 例行通知，验证码 {{code}}；花括号字面保留 {a:1} {0} {{otherVar}} json 片段',
    contentType: 'text',
    variables: JSON.stringify([
      { name: 'site', description: '平台名', example: 'moai-e2e' },
      { name: 'code', description: '验证码', example: '246810' },
    ]),
    aliyunTemplateCode: '',
    tencentTemplateId: '',
    description: 'e2e 文本模板',
  }
  const tplSms = {
    name: chName('短信模板'),
    title: '',
    content: '{{code}} 已发送至 {{phone}}，{{minutes}} 分钟内有效',
    contentType: 'text',
    variables: JSON.stringify([
      { name: 'code', description: '验证码', example: '668812' },
      { name: 'phone', description: '手机号', example: '13800138000' },
      { name: 'minutes', description: '有效期（分钟）', example: '5' },
    ]),
    aliyunTemplateCode: 'SMS_10086',
    tencentTemplateId: '2012345',
    description: 'e2e 短信模板',
  }
  const tplHtml = {
    name: chName('HTML模板'),
    title: `NT${RUN} HTML 通知`,
    content: '<html><head><style>p{color:#333}</style></head><body><h1>{{name}} 的余额提醒</h1><p>当前余额 {{amount}} 元</p></body></html>',
    contentType: 'html',
    variables: JSON.stringify([
      { name: 'name', description: '姓名', example: '王五' },
      { name: 'amount', description: '金额', example: '123.45' },
    ]),
    aliyunTemplateCode: '',
    tencentTemplateId: '',
    description: 'e2e HTML 模板',
  }

  check('NT-80 未登录查询模板 401', (await api('GET', '/api/notification/template/list')).status === 401)
  check('NT-81 非管理员创建模板 403', (await api('POST', '/api/notification/template/create', { token: member.token, body: { name: 'x', content: 'y', variables: '[]' } })).status === 403)
  const textTplId = await mkTpl(tplText)
  const smsTplId = await mkTpl(tplSms)
  const htmlTplId = await mkTpl(tplHtml)
  check('NT-82 创建模板返回 id', Number(textTplId) > 0 && Number(smsTplId) > 0 && Number(htmlTplId) > 0)
  check('NT-83 模板重名 400', (await tcreate({ ...tplText, name: chName('文本模板') })).status === 400)
  check('NT-84 模板空正文 400', (await tcreate({ name: chName('空正文'), content: '', variables: '[]' })).status === 400)
  check('NT-85 变量定义非法 JSON 400', (await tcreate({ name: chName('坏变量'), content: 'x', variables: '{oops' })).status === 400)
  check('NT-86 变量名不合法 400', (await tcreate({ name: chName('坏变量名'), content: 'x', variables: JSON.stringify([{ name: '1abc' }]) })).status === 400)
  check('NT-86b 变量名重复 400', (await tcreate({ name: chName('重复变量'), content: 'x', variables: JSON.stringify([{ name: 'a' }, { name: 'a' }]) })).status === 400)

  const tplUpd = await api('PUT', `/api/notification/template/${textTplId}`, { token: root, body: { ...tplText, title: `NT${RUN} 改后标题 {{site}}`, description: 'renamed' } })
  check('NT-87 更新模板成功', tplUpd.status === 200)
  check('NT-87b 更新后列表回显新标题与审计', ((await api('GET', `/api/notification/template/list?search=${RUN}`, { token: root })).json?.items ?? []).find((x) => String(x.id) === String(textTplId))?.title === `NT${RUN} 改后标题 {{site}}`)
  check('NT-87c 更新重名 400', (await api('PUT', `/api/notification/template/${textTplId}`, { token: root, body: { ...tplText, name: chName('短信模板') } })).status === 400)
  check('NT-88 更新不存在模板 404', (await api('PUT', '/api/notification/template/99999999', { token: root, body: { ...tplText } })).status === 404)
  const tplList = (await api('GET', `/api/notification/template/list?search=${RUN}`, { token: root })).json?.items ?? []
  check('NT-89 模板列表回显 3 条含审计人名与映射', tplList.length === 3 && tplList.every((x) => x.createUserName?.length > 0) && tplList.find((x) => x.name === chName('短信模板'))?.aliyunTemplateCode === 'SMS_10086' && tplList.find((x) => x.name === chName('HTML模板'))?.contentType === 'html', JSON.stringify(tplList.map((x) => [x.name, x.contentType])))

  // 内置模板（种子提供：注册/登录/找回密码验证码邮件，HTML）：isBuiltIn 回显 + 不可删除 + 可编辑
  const builtinList = ((await api('GET', '/api/notification/template/list', { token: root })).json?.items ?? []).filter((x) => x.isBuiltIn === true)
  check('NT-89b 内置模板 3 条且 isBuiltIn 回显', builtinList.length >= 3 && builtinList.every((x) => x.contentType === 'html'), JSON.stringify(builtinList.map((x) => x.name)))
  const builtinRegister = builtinList.find((x) => x.name === '注册验证码邮件')
  check('NT-89c 内置模板列表含注册/登录/找回密码', builtinRegister != null && builtinList.some((x) => x.name === '登录验证码邮件') && builtinList.some((x) => x.name === '找回密码验证码邮件'), JSON.stringify(builtinList.map((x) => x.name)))
  if (builtinRegister != null) {
    check('NT-89d 内置模板删除 400', (await api('DELETE', `/api/notification/template/${builtinRegister.id}`, { token: root })).status === 400)
    const builtinEdit = await api('PUT', `/api/notification/template/${builtinRegister.id}`, {
      token: root,
      body: {
        name: builtinRegister.name,
        title: `${builtinRegister.title}（改）`,
        content: builtinRegister.content,
        contentType: 'html',
        variables: builtinRegister.variables,
        description: builtinRegister.description,
      },
    })
    check('NT-89e 内置模板可编辑内容', builtinEdit.status === 200, `${builtinEdit.status} ${builtinEdit.text.slice(0, 120)}`)
    const builtinEdited = ((await api('GET', '/api/notification/template/list', { token: root })).json?.items ?? []).find((x) => String(x.id) === String(builtinRegister.id))
    check('NT-89f 内置模板编辑后回显', builtinEdited?.title === `${builtinRegister.title}（改）`, builtinEdited?.title)
    // 还原标题，避免影响其它脚本对内置模板内容的断言
    await api('PUT', `/api/notification/template/${builtinRegister.id}`, {
      token: root,
      body: {
        name: builtinRegister.name,
        title: builtinRegister.title,
        content: builtinRegister.content,
        contentType: 'html',
        variables: builtinRegister.variables,
        description: builtinRegister.description,
      },
    })
  }

  // 渲染预览（不指定 channelId 仅渲染）
  const tplTest = (body) => api('POST', '/api/notification/template/test', { token: root, body })
  const preview = await tplTest({ templateId: textTplId, variables: { site: 'MoAI-E2E', code: '246810' } })
  check('NT-90 渲染预览插值正确且花括号字面保留', preview.json?.renderedTitle === `NT${RUN} 改后标题 MoAI-E2E` && String(preview.json?.renderedContent).includes('服务 MoAI-E2E 例行通知，验证码 246810') && String(preview.json?.renderedContent).includes('{a:1} {0}') && String(preview.json?.renderedContent).includes('{{otherVar}}'), JSON.stringify(preview.json)?.slice(0, 200))
  check('NT-91 缺变量 400 列出缺失名', (await tplTest({ templateId: textTplId, variables: { site: 'x' } })).status === 400 && JSON.stringify((await tplTest({ templateId: textTplId, variables: { site: 'x' } })).json).includes('code'))
  check('NT-92 模板不存在 404', (await tplTest({ templateId: 99999999, variables: {} })).status === 404)
  const htmlPreview = await tplTest({ templateId: htmlTplId, variables: { name: '王五', amount: '123.45' } })
  check('NT-93 HTML 模板渲染返回 HTML 文本', htmlPreview.json?.renderedContent?.startsWith('<html>') && String(htmlPreview.json?.renderedContent).includes('<h1>王五 的余额提醒</h1>'), JSON.stringify(htmlPreview.json)?.slice(0, 160))

  // ============ 短信渠道：配置校验 + 无模板失败 + 平台参数断言（收件人发送时动态指定） ============
  const aliId = await mk(chName('阿里短信'), 'aliyunSms', { accessKeyId: 'ntLTAIKey', accessKeySecret: 'ntAliSecret', signName: 'MoAI测试', endpoint: `${STUB}/alisms` })
  const tcId = await mk(chName('腾讯短信'), 'tencentSms', { secretId: 'ntAKIDkey', secretKey: 'ntTcSecret', smsSdkAppId: '140000001', signName: 'MoAI测试', endpoint: `${STUB}/tencent` })
  createdIds.push(aliId, tcId)
  check('NT-94 aliyunSms 缺签名 400', (await create(chName('阿里缺签名'), 'aliyunSms', { accessKeyId: 'k', accessKeySecret: 's' })).status === 400)
  // NT-95（渠道配置无效手机号 400）随手机号移出渠道配置退役，编号不复用
  check('NT-116 广播含短信渠道缺手机号收件人 400', (await api('POST', '/api/notification/broadcast', { token: root, body: { title: `NT${RUN} 广播六`, content: '缺手机号', level: 'info', channelIds: [String(aliId)] } })).status === 400)
  check('NT-117 广播手机号格式非法 400', (await api('POST', '/api/notification/broadcast', { token: root, body: { title: `NT${RUN} 广播七`, content: '坏手机号', level: 'info', channelIds: [String(aliId)], phoneNumbers: ['12ab'] } })).status === 400)
  check('NT-95b tencentSms 缺 SdkAppId 400', (await create(chName('腾讯缺AppId'), 'tencentSms', { secretId: 'k', secretKey: 's', signName: 's' })).status === 400)
  const smsList = (await api('GET', `/api/notification/channel/list?search=${RUN}`, { token: root })).json?.items ?? []
  check('NT-95c 短信渠道创建回显类型', smsList.some((x) => String(x.id) === String(aliId) && x.type === 'aliyunSms') && smsList.some((x) => String(x.id) === String(tcId) && x.type === 'tencentSms'))

  // 短信渠道直发（无模板）→ 该渠道 failed，错误提示需模板；模板试发缺手机号时提示动态收件人
  // （渠道试发不带模板，发送器先卡模板检查；缺收件人检查走下方 NT-118 的模板试发路径）
  const noPhoneAli = await tplTest({ templateId: smsTplId, variables: { code: '1', phone: '13800138000', minutes: '5' }, channelId: String(aliId) })
  check('NT-118 模板试发缺手机号失败提示动态收件人', noPhoneAli.json?.success === false && String(noPhoneAli.json?.error).includes('收件人手机号'), JSON.stringify(noPhoneAli.json)?.slice(0, 160))
  const noTplAli = await testCh({ type: 'aliyunSms', config: JSON.stringify({ accessKeyId: 'ntLTAIKey', accessKeySecret: 'ntAliSecret', signName: 'MoAI测试', endpoint: STUB }), recipients: ['13800138000'] })
  check('NT-96 短信渠道无模板直发失败提示需模板', noTplAli.json?.success === false && String(noTplAli.json?.error).includes('模板'), JSON.stringify(noTplAli.json)?.slice(0, 160))
  const noMapAli = await tplTest({ templateId: textTplId, variables: { site: 'x', code: '1' }, channelId: String(aliId) })
  check('NT-97 模板未映射阿里云 Code 试发失败', noMapAli.json?.success === false && String(noMapAli.json?.error).includes('阿里云模板 Code'), JSON.stringify(noMapAli.json)?.slice(0, 160))
  const noMapTc = await tplTest({ templateId: textTplId, variables: { site: 'x', code: '1' }, channelId: String(tcId) })
  check('NT-98 模板未映射腾讯云 Id 试发失败', noMapTc.json?.success === false && String(noMapTc.json?.error).includes('腾讯云模板 Id'), JSON.stringify(noMapTc.json)?.slice(0, 160))

  // 阿里云短信真实发送：GET 查询串断言公共/业务参数
  hook.state.hits.length = 0
  const aliSend = await tplTest({ templateId: smsTplId, variables: { code: '668812', phone: '13800138000', minutes: '5' }, channelId: String(aliId), recipients: ['13800138000'] })
  const aliHit = hook.state.hits.find((h) => h.path === '/alisms')
  check('NT-99 阿里云试发成功', aliSend.json?.success === true && aliHit != null, JSON.stringify(aliSend.json)?.slice(0, 160))
  const aliParam = aliHit?.query
  let aliTplParam = null
  try { aliTplParam = JSON.parse(aliParam?.get('TemplateParam') ?? 'null') } catch { /* ignore */ }
  check('NT-100 阿里云请求参数完整（签名/时间戳/号码/签名名/模板Code）', aliParam?.get('Action') === 'SendSms' && aliParam?.get('Version') === '2017-05-25' && aliParam?.get('Signature')?.length > 0 && aliParam?.get('SignatureMethod') === 'HMAC-SHA1' && aliParam?.get('Timestamp')?.endsWith('Z') && aliParam?.get('PhoneNumbers') === '13800138000' && aliParam?.get('SignName') === 'MoAI测试' && aliParam?.get('TemplateCode') === 'SMS_10086', aliHit?.url ?? 'no hit')
  check('NT-100b 阿里云 TemplateParam 为变量 JSON', JSON.stringify(aliTplParam) === JSON.stringify({ code: '668812', phone: '13800138000', minutes: '5' }), aliParam?.get('TemplateParam'))
  hook.state.hits.length = 0
  const aliBadChId = await mk(chName('阿里坏响应'), 'aliyunSms', { accessKeyId: 'k2', accessKeySecret: 's2', signName: 's2', endpoint: `${STUB}/alisms-bad` })
  const aliBad = await tplTest({ templateId: smsTplId, variables: { code: '1', phone: '13800138000', minutes: '5' }, channelId: String(aliBadChId), recipients: ['13800138000'] })
  createdIds.push(aliBadChId)
  const aliBadHit = hook.state.hits.find((h) => h.path === '/alisms-bad')
  check('NT-101 阿里云业务错误码失败归一', aliBad.json?.success === false && String(aliBad.json?.error).includes('isv.BUSINESS_LIMIT_CONTROL') && String(aliBad.json?.error).includes('触发业务流控') && aliBadHit != null, JSON.stringify(aliBad.json)?.slice(0, 200))

  // 腾讯云短信真实发送：TC3 请求头 + JSON 体断言
  hook.state.hits.length = 0
  const tcSend = await tplTest({ templateId: smsTplId, variables: { code: '668812', phone: '13800138000', minutes: '5' }, channelId: String(tcId), recipients: ['13800138000', '13800138001'] })
  const tcHit = hook.state.hits.find((h) => h.path === '/tencent')
  let tcPayload = null
  try { tcPayload = JSON.parse(tcHit?.raw ?? 'null') } catch { /* ignore */ }
  check('NT-102 腾讯云试发成功', tcSend.json?.success === true && tcHit != null, JSON.stringify(tcSend.json)?.slice(0, 160))
  check('NT-103 腾讯云 TC3 签名头完整', tcHit?.headers['x-tc-action'] === 'SendSms' && tcHit?.headers['x-tc-version'] === '2021-01-11' && tcHit?.headers['x-tc-timestamp']?.length > 0 && String(tcHit?.headers['authorization'] ?? '').startsWith('TC3-HMAC-SHA256 Credential=ntAKIDkey/') && String(tcHit?.headers['authorization'] ?? '').includes('SignedHeaders=content-type;host;x-tc-action'), JSON.stringify(tcHit?.headers)?.slice(0, 240))
  check('NT-103b 腾讯云请求体参数完整（SdkAppId/签名/模板Id/号码集）', tcPayload?.SmsSdkAppId === '140000001' && tcPayload?.SignName === 'MoAI测试' && tcPayload?.TemplateId === '2012345' && JSON.stringify(tcPayload?.PhoneNumberSet) === JSON.stringify(['13800138000', '13800138001']), JSON.stringify(tcPayload)?.slice(0, 200))
  check('NT-103c 腾讯云 TemplateParamSet 按正文出现顺序', JSON.stringify(tcPayload?.TemplateParamSet) === JSON.stringify(['668812', '13800138000', '5']), JSON.stringify(tcPayload?.TemplateParamSet))
  hook.state.hits.length = 0
  const tcBadStatusId = await mk(chName('腾讯坏状态'), 'tencentSms', { secretId: 'k3', secretKey: 's3', smsSdkAppId: '140000002', signName: 's3', endpoint: `${STUB}/tencent-bad` })
  const tcBad = await tplTest({ templateId: smsTplId, variables: { code: '1', phone: '13800138000', minutes: '5' }, channelId: String(tcBadStatusId), recipients: ['13800138000'] })
  check('NT-104 腾讯云 SendStatusSet 非 Ok 失败归一', tcBad.json?.success === false && String(tcBad.json?.error).includes('LimitExceeded.PhoneNumberLimit') && String(tcBad.json?.error).includes('发送数量超限'), JSON.stringify(tcBad.json)?.slice(0, 200))
  const tcErrorId = await mk(chName('腾讯Error'), 'tencentSms', { secretId: 'k4', secretKey: 's4', smsSdkAppId: '140000003', signName: 's4', endpoint: `${STUB}/tencent-error` })
  const tcErr = await tplTest({ templateId: smsTplId, variables: { code: '1', phone: '13800138000', minutes: '5' }, channelId: String(tcErrorId), recipients: ['13800138000'] })
  check('NT-105 腾讯云平台 Error 对象失败归一', tcErr.json?.success === false && String(tcErr.json?.error).includes('AuthFailure.SignatureFailure'), JSON.stringify(tcErr.json)?.slice(0, 200))
  createdIds.push(tcBadStatusId, tcErrorId)

  // ============ HTML 模板：SMTP 发 HTML、非邮件渠道降级纯文本 ============
  smtp.state.messages.length = 0
  const htmlMail = await tplTest({ templateId: htmlTplId, variables: { name: '王五', amount: '123.45' }, channelId: String(smtpId), recipients: ['nt@test.local'] })
  const htmlMsgRaw = smtp.state.messages.pop()
  const htmlMsgData = decodeMailBody(htmlMsgRaw?.data ?? '')
  check('NT-106 HTML 模板经 SMTP 发送 text/html 正文', htmlMail.json?.success === true && htmlMsgData.includes('text/html') && htmlMsgData.includes('<h1>王五 的余额提醒</h1>') && htmlMsgData.includes('123.45'), JSON.stringify({ err: htmlMail.json?.error, head: htmlMsgData.slice(0, 240) }))
  hook.state.hits.length = 0
  const htmlHook = await tplTest({ templateId: htmlTplId, variables: { name: '王五', amount: '123.45' }, channelId: String(genericId) })
  const htmlHookPayload = JSON.parse(hook.state.hits.find((h) => h.path === '/generic')?.raw ?? '{}')
  check('NT-107 HTML 模板经 webhook 降级去标签纯文本', htmlHook.json?.success === true && !String(htmlHookPayload?.content).includes('<h1>') && String(htmlHookPayload?.content).includes('王五 的余额提醒') && String(htmlHookPayload?.content).includes('123.45'), JSON.stringify(htmlHookPayload)?.slice(0, 200))

  // ============ 广播带模板：渲染后文本落记录与站内信 ============
  const bcTpl = await api('POST', '/api/notification/broadcast', { token: root, body: { title: '', content: '', level: 'info', templateId: String(textTplId), templateVariables: { site: 'MoAI-E2E', code: '246810' }, channelIds: [String(feishuId)], sendInSite: true } })
  check('NT-108 广播带模板入队成功（标题正文可空）', bcTpl.status === 200 && bcTpl.json?.channelCount === 1, JSON.stringify(bcTpl.json))
  check('NT-109 广播带模板缺变量 400', (await api('POST', '/api/notification/broadcast', { token: root, body: { title: '', content: '', level: 'info', templateId: String(textTplId), templateVariables: { site: 'only-one' }, channelIds: [String(feishuId)] } })).status === 400)
  check('NT-110 广播模板不存在 404', (await api('POST', '/api/notification/broadcast', { token: root, body: { title: '', content: '', level: 'info', templateId: '99999999', templateVariables: {}, channelIds: [String(feishuId)] } })).status === 404)
  let bcTplLog = null
  for (let i = 0; i < 10 && !bcTplLog; i++) {
    await sleep(4000)
    const r = await api('GET', `/api/notification/log/list?pageNo=1&pageSize=20&channelId=${feishuId}`, { token: root })
    bcTplLog = (r.json?.items ?? []).find((x) => x.title === `NT${RUN} 改后标题 MoAI-E2E` && x.status === 'success') ?? null
  }
  check('NT-111 广播渲染后标题落发送记录', bcTplLog != null && String(bcTplLog?.content).includes('服务 MoAI-E2E 例行通知，验证码 246810') && String(bcTplLog?.content).includes('{a:1}'), JSON.stringify(bcTplLog)?.slice(0, 200))
  let bcTplInSite = false
  for (let i = 0; i < 10 && !bcTplInSite; i++) {
    await sleep(3000)
    const ml = await api('GET', '/api/message/list?pageNo=1&pageSize=50', { token: root })
    bcTplInSite = (ml.json?.items ?? []).some((x) => x.title === `NT${RUN} 改后标题 MoAI-E2E` && String(x.content).includes('验证码 246810'))
  }
  check('NT-112 广播模板渲染文本送达站内信', bcTplInSite)

  // 模板删除：软删除，列表移除；已发送记录不受影响
  const delTpl = await api('DELETE', `/api/notification/template/${htmlTplId}`, { token: root })
  const tplListAfter = (await api('GET', `/api/notification/template/list?search=${RUN}`, { token: root })).json?.items ?? []
  check('NT-113 模板删除后列表移除且历史记录保留', delTpl.status === 200 && !tplListAfter.some((x) => String(x.id) === String(htmlTplId)) && (await api('GET', '/api/notification/log/list?pageNo=1&pageSize=50', { token: root })).json?.items?.some((x) => x.title === `NT${RUN} HTML 通知`))

  // ============ 应用机器人渠道（飞书/钉钉/企微 App 凭证，按 oauth 绑定反查个人推送） ============
  // 先探测后端是否把三平台登录端点覆盖到桩：oauth_login 同步调用平台换码，未命中桩说明未配置端点覆盖 → 跳过本段
  const probeName = `ntoauth${RUN}-probe`
  await api('POST', '/api/oauthconnect/connections', { token: root, body: { name: probeName, provider: 'feishu', key: 'ntkey-feishu', secret: 'ntsecret-feishu', iconUrl: `https://127.0.0.1/feishu.png` } })
  const probeConnId = ((await api('GET', '/api/oauthconnect/connections', { token: root })).json?.items ?? []).find((x) => x.name === probeName)?.id
  appbot.state.hits.length = 0
  await api('POST', '/api/auth/oauth_login', { body: { oAuthId: probeConnId, code: `ntcode|feishu|probe|${RUN}`, redirectUrl: 'http://127.0.0.1:4000' } })
  const probeHit = appbot.state.hits.some((h) => h.path === '/open-apis/authen/v2/oauth/token')

  if (!probeHit) {
    await api('DELETE', `/api/oauthconnect/connections/${probeConnId}`, { token: root })
    console.warn('\nSKIP | 应用机器人场景（NT-56..70）未执行：后端未把三平台登录端点覆盖到桩，见文件头启动示例')
  } else {
    // 缺凭证校验
    check('NT-56 feishuAppBot 缺 appId 400', (await create(chName('飞书应用'), 'feishuAppBot', { baseUrl: APPBOT, appSecret: 'nt-app-secret' })).status === 400)
    check('NT-57 dingtalkAppBot 缺 agentId 400', (await create(chName('钉钉应用'), 'dingtalkAppBot', { baseUrl: APPBOT, appKey: 'nt-app-key', appSecret: 'nt-app-secret' })).status === 400)
    check('NT-58 wecomAppBot 缺 corpSecret 400', (await create(chName('企微应用'), 'wecomAppBot', { baseUrl: APPBOT, corpId: 'nt-corp-id', agentId: '1000002' })).status === 400)

    // 三渠道创建（App 凭证独立于登录连接；baseUrl 指向桩）
    const feishuAppId = await mk(chName('飞书应用'), 'feishuAppBot', { baseUrl: APPBOT, appId: 'nt-app-id', appSecret: 'nt-app-secret' })
    const dingAppId = await mk(chName('钉钉应用'), 'dingtalkAppBot', { baseUrl: APPBOT, appKey: 'nt-app-key', appSecret: 'nt-app-secret', agentId: '1000002' })
    const wecomAppId = await mk(chName('企微应用'), 'wecomAppBot', { baseUrl: APPBOT, corpId: 'nt-corp-id', corpSecret: 'nt-corp-secret', agentId: '1000002' })
    const appBotIds = [feishuAppId, dingAppId, wecomAppId]
    const listApp = (await api('GET', `/api/notification/channel/list?search=${RUN}`, { token: root })).json?.items ?? []
    check('NT-59 三类应用机器人渠道创建并回显类型', appBotIds.every((id) => listApp.some((x) => String(x.id) === String(id) && x.type?.endsWith('AppBot'))), JSON.stringify(listApp.map((x) => [x.name, x.type])))

    // 六个 OAuth 连接（假凭证；登录换码与推送 API 均指向桩）。oauth_bind_account 既有校验为
    // 「同一连接仅允许一个用户绑定」，故 admin 与 member 各用一套连接；解析按 provider 跨连接匹配不受影响
    const oauthConns = []
    for (const [provider, extra] of [['feishu', {}], ['dingtalk', {}], ['weixinwork', { agentId: '1000002' }]]) {
      for (const who of ['member', 'admin']) {
        const name = `ntoauth${RUN}-${provider}-${who}`
        await api('POST', '/api/oauthconnect/connections', { token: root, body: { name, provider, key: `ntkey-${provider}`, secret: `ntsecret-${provider}`, iconUrl: `https://127.0.0.1/${provider}.png`, ...extra } })
        const item = ((await api('GET', '/api/oauthconnect/connections', { token: root })).json?.items ?? []).find((x) => x.name === name)
        if (item) oauthConns.push({ id: item.id, provider, who })
      }
    }
    check('NT-60 六个 OAuth 连接创建成功', oauthConns.length === 6, JSON.stringify(oauthConns))

    // 绑定链路：oauth_login（桩换码取平台标识）→ oauth_bind_account
    const bindPlatform = async (who, token, provider) => {
      const conn = oauthConns.find((x) => x.provider === provider && x.who === who)
      const l = await api('POST', '/api/auth/oauth_login', { body: { oAuthId: conn.id, code: `ntcode|${provider}|${who}|${RUN}`, redirectUrl: 'http://127.0.0.1:4000' } })
      if (l.json?.isBindUser === true) return { ok: true }
      const b = await api('POST', '/api/account/oauth_bind_account', { token, body: { tempOAuthBindId: l.json?.tempOAuthBindId } })
      return { ok: b.status === 200, loginStatus: l.status, bindStatus: b.status, err: l.json?.message ?? b.json?.message }
    }
    const mFeishu = await bindPlatform('member', member.token, 'feishu')
    check('NT-61 member 绑定飞书（桩换 open_id/union_id）', mFeishu.ok, JSON.stringify(mFeishu))
    const mDing = await bindPlatform('member', member.token, 'dingtalk')
    check('NT-62 member 绑定钉钉（unionId 入绑定表）', mDing.ok, JSON.stringify(mDing))
    const mWecom = await bindPlatform('member', member.token, 'weixinwork')
    check('NT-63 member 绑定企微（userid 入绑定表）', mWecom.ok, JSON.stringify(mWecom))

    // 试发定向当前管理员：root 尚未绑定企微 → 无可投递绑定
    const tWx0 = await testCh({ id: wecomAppId })
    check('NT-64 未绑定时试发企微渠道失败回显', tWx0.json?.success === false && String(tWx0.json?.error).includes('没有站内用户绑定企业微信账号'), tWx0.text?.slice(0, 160))

    // 站内信托底：渠道零绑定失败不影响站内信先行必达（试发人收到站内信副本）
    let unboundInSite = false
    for (let i = 0; i < 5 && !unboundInSite; i++) {
      await sleep(1000)
      const ml = await api('GET', '/api/message/list?pageNo=1&pageSize=50', { token: root })
      unboundInSite = (ml.json?.items ?? []).some((x) => x.title === `通知渠道测试 - ${chName('企微应用')}` && String(x.content).includes('收到即表示渠道配置可用'))
    }
    check('NT-122 未绑定渠道试发失败但站内信托底仍送达', unboundInSite)

    const rFeishu = await bindPlatform('admin', root, 'feishu')
    const rDing = await bindPlatform('admin', root, 'dingtalk')
    const rWecom = await bindPlatform('admin', root, 'weixinwork')
    check('NT-65 root 绑定三平台成功', rFeishu.ok && rDing.ok && rWecom.ok, JSON.stringify([rFeishu, rDing, rWecom]))

    // 试发飞书：union_id 优先收件 + 文本含【MoAI】（content 为 JSON 字符串套 JSON，先解析内层再断言）
    appbot.state.hits.length = 0
    const tFs = await testCh({ id: feishuAppId })
    const fsHit = appbot.state.hits.filter((h) => h.path === '/open-apis/im/v1/messages').pop()
    const fsPayload = JSON.parse(fsHit?.raw ?? '{}')
    const fsText = (() => { try { return JSON.parse(fsPayload?.content ?? '{}')?.text ?? '' } catch { return '' } })()
    check('NT-66 试发飞书 union_id 收件', tFs.json?.success === true && fsHit?.query.get('receive_id_type') === 'union_id' && fsPayload?.receive_id === `un-admin-${RUN}` && fsText.includes('【MoAI】'), `${tFs.text?.slice(0, 100)} | ${fsHit?.raw.slice(0, 120)}`)

    // 试发钉钉：unionId 换算 userid + agent_id 投递
    appbot.state.hits.length = 0
    const tDt = await testCh({ id: dingAppId })
    const dtUnion = appbot.state.hits.filter((h) => h.path === '/topapi/user/getbyunionid').pop()
    const dtSend = appbot.state.hits.filter((h) => h.path === '/topapi/message/corpconversation/asyncsend_v2').pop()
    const dtPayload = JSON.parse(dtSend?.raw ?? '{}')
    check('NT-67 试发钉钉 unionId 换算 userid 投递', tDt.json?.success === true && JSON.parse(dtUnion?.raw ?? '{}')?.unionid === `dtun-admin-${RUN}` && dtPayload?.agent_id === 1000002 && dtPayload?.userid_list === `dtstaff-admin-${RUN}`, `${tDt.text?.slice(0, 100)} | ${dtSend?.raw.slice(0, 120)}`)

    // 试发企微：绑定 userid 直投 + agentid
    appbot.state.hits.length = 0
    const tWx = await testCh({ id: wecomAppId })
    const wxHit = appbot.state.hits.filter((h) => h.path === '/cgi-bin/message/send').pop()
    const wxPayload = JSON.parse(wxHit?.raw ?? '{}')
    check('NT-68 试发企微 touser 直投', tWx.json?.success === true && wxPayload?.touser === `wxuid-admin-${RUN}` && wxPayload?.agentid === 1000002 && String(wxPayload?.text?.content ?? '').includes('【MoAI】'), `${tWx.text?.slice(0, 100)} | ${wxHit?.raw.slice(0, 120)}`)

    // 广播三渠道：飞书逐用户单聊 2 条 / 钉钉批量 userid_list / 企微 touser 串
    appbot.state.hits.length = 0
    const bc6 = await api('POST', '/api/notification/broadcast', { token: root, body: { title: `NT${RUN} 广播六`, content: '应用机器人投递', level: 'info', channelIds: appBotIds.map(String) } })
    check('NT-69a 广播三渠道 channelCount=3', bc6.status === 200 && bc6.json?.channelCount === 3, JSON.stringify(bc6.json))
    let bc6Done = false
    for (let i = 0; i < 24 && !bc6Done; i++) {
      await sleep(5000)
      const r = await api('GET', '/api/notification/log/list?pageNo=1&pageSize=50', { token: root })
      const rows = (r.json?.items ?? []).filter((x) => x.title?.startsWith(`NT${RUN} 广播六`))
      bc6Done = rows.length >= 3 && rows.every((x) => x.status !== 'pending')
    }
    const fsBcHits = appbot.state.hits.filter((h) => h.path === '/open-apis/im/v1/messages').map((h) => JSON.parse(h.raw)?.receive_id).sort()
    const dtBcHit = JSON.parse(appbot.state.hits.filter((h) => h.path === '/topapi/message/corpconversation/asyncsend_v2').pop()?.raw ?? '{}')
    const wxBcHit = JSON.parse(appbot.state.hits.filter((h) => h.path === '/cgi-bin/message/send').pop()?.raw ?? '{}')
    check(
      'NT-69b 广播按绑定反查投递个人（飞书 2 单聊/钉钉批量/企微 touser）',
      bc6Done
        && JSON.stringify(fsBcHits) === JSON.stringify([`un-admin-${RUN}`, `un-member-${RUN}`])
        && String(dtBcHit?.userid_list ?? '').split('|').sort().join('|') === [`dtstaff-admin-${RUN}`, `dtstaff-member-${RUN}`].sort().join('|')
        && String(wxBcHit?.touser ?? '').split('|').sort().join('|') === [`wxuid-admin-${RUN}`, `wxuid-member-${RUN}`].sort().join('|'),
      `fs=${JSON.stringify(fsBcHits)} dt=${dtBcHit?.userid_list} wx=${wxBcHit?.touser}`,
    )

    // 通知应用与登录应用跨企业：unionId 全部换算失败 → 渠道 failed 带原因
    await fetch(`${APPBOT}/__control/unionid_fail`, { method: 'POST', body: 'true' })
    const bc7 = await api('POST', '/api/notification/broadcast', { token: root, body: { title: `NT${RUN} 广播七`, content: 'unionId 换算失败', level: 'info', channelIds: [String(dingAppId)] } })
    check('NT-70a 广播钉钉渠道入队', bc7.status === 200 && bc7.json?.channelCount === 1)
    let dtFailLog = null
    for (let i = 0; i < 24 && !dtFailLog; i++) {
      await sleep(5000)
      const r = await api('GET', `/api/notification/log/list?pageNo=1&pageSize=50&channelId=${dingAppId}`, { token: root })
      dtFailLog = (r.json?.items ?? []).find((x) => x.title?.startsWith(`NT${RUN} 广播七`) && x.status !== 'pending') ?? null
    }
    check('NT-70b unionId 均未换算渠道 failed 带原因', dtFailLog?.status === 'failed' && String(dtFailLog?.error).includes('均未换算'), JSON.stringify(dtFailLog)?.slice(0, 200))
    await fetch(`${APPBOT}/__control/unionid_fail`, { method: 'POST', body: 'false' })

    // 清理：渠道 + OAuth 连接 + 本轮绑定（软删，历史保留）
    for (const id of appBotIds) await api('DELETE', `/api/notification/channel/${id}`, { token: root })
    for (const conn of [{ id: probeConnId }, ...oauthConns]) await api('DELETE', `/api/oauthconnect/connections/${conn.id}`, { token: root })
    for (const conn of oauthConns) {
      const token = conn.who === 'admin' ? root : member.token
      await api('POST', '/api/account/unbind_account', { token, body: { providerId: conn.id } })
    }
  }

  // ============ 清理本轮渠道与模板（历史发送记录保留） ============
  for (const id of [...createdIds.filter((x) => String(x) !== String(tplId))]) {
    await api('DELETE', `/api/notification/channel/${id}`, { token: root })
  }
  for (const id of [textTplId, smsTplId, htmlTplId]) {
    await api('DELETE', `/api/notification/template/${id}`, { token: root })
  }

  // ============ 收尾：恢复 SMTP 系统绑定快照，关闭桩 ============
  for (const key of SMTP_KEYS) {
    const v = ORIG[key]
    if (v !== undefined) await api('PUT', '/api/settings', { token: root, body: { key, value: v } })
  }
  hook.server.close()
  smtp.server.close()
  appbot.server.close()

  console.log(`\n==== NT 结果: PASS ${PASS} / FAIL ${FAIL} ====`)
  process.exit(FAIL > 0 ? 1 : 0)
}

main().catch((e) => {
  console.error('E2E 异常终止:', e)
  process.exit(1)
})
