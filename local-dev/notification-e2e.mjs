// 通知中心 · E2E（场景 @NT-S1..）
// 覆盖：管理员门禁（401/403）/ 渠道 CRUD 与配置校验（七类渠道类型）/ 渠道测试同步试发（飞书加签、钉钉加签、
//       企业微信 markdown、Telegram sendMessage、通用 webhook 自定义头与模板渲染、SMTP 复用系统设置、站内推送
//       toAdmins）/ 广播异步投递（Hangfire 落发送记录：success/failed、禁用渠道不投递、渠道删除后历史保留、
//       发送记录过滤分页）。
// 前置：后端运行中；种子账号 admin / abcd123456（root 即管理员）。内置 webhook 收集桩与 SMTP 桩，零真实外部依赖。
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

  // SMTP 设置快照（结束恢复）
  const SMTP_KEYS = ['SMTP_ENABLED', 'SMTP_HOST', 'SMTP_PORT', 'SMTP_SECURITY', 'SMTP_USERNAME', 'SMTP_PASSWORD', 'SMTP_FROM_ADDRESS', 'SMTP_FROM_NAME']
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
  check('NT-04 非管理员广播 403', (await api('POST', '/api/notification/broadcast', { token: member.token, body: { title: 't', content: 'c' } })).status === 403)

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
  const smtpId = await mk(chName('邮件'), 'smtp', { recipients: ['nt@test.local'], subjectPrefix: '' })
  const insiteId = await mk(chName('站内'), 'inSite', { userIds: [], toAdmins: true })
  const failId = await mk(chName('失败'), 'webhook', { url: `${STUB}/fail` })
  const badFeishuId = await mk(chName('坏飞书'), 'feishuWebhook', { webhookUrl: `${STUB}/feishu-bad` })
  const createdIds = [feishuId, dingId, wecomId, tgId, genericId, tplId, smtpId, insiteId, failId, badFeishuId]

  check('NT-05 创建飞书渠道返回 id', Number(feishuId) > 0)
  const dup = await create(chName('飞书'), 'feishuWebhook', { webhookUrl: `${STUB}/feishu` })
  check('NT-06 重名 400', dup.status === 400, `${dup.status}`)
  const badUrl = await create(chName('坏URL'), 'webhook', { url: 'not-a-url' })
  check('NT-07 非法 url 400', badUrl.status === 400, `${badUrl.status}`)
  const badJson = await api('POST', '/api/notification/channel/create', { token: root, body: { name: chName('坏JSON'), type: 'webhook', config: '{oops' } })
  check('NT-08 配置非法 JSON 400', badJson.status === 400, `${badJson.status}`)
  const noChat = await create(chName('无chatId'), 'telegramBot', { botToken: 'x' })
  check('NT-09 telegram 缺 chatId 400', noChat.status === 400, `${noChat.status}`)
  const badMail = await create(chName('坏邮箱'), 'smtp', { recipients: ['not-an-email'] })
  check('NT-10 smtp 无效邮箱 400', badMail.status === 400, `${badMail.status}`)
  const noTarget = await create(chName('无收件人'), 'inSite', { userIds: [], toAdmins: false })
  check('NT-11 inSite 无收件人 400', noTarget.status === 400, `${noTarget.status}`)

  const list1 = await api('GET', `/api/notification/channel/list?search=${RUN}`, { token: root })
  const items = list1.json?.items ?? []
  check('NT-12 列表回显 10 渠道（含审计人名）', items.length === 10 && items.every((x) => x.createUserName?.length > 0), `got ${items.length}`)
  check('NT-13 search 过滤命中', items.every((x) => x.name.includes(RUN)) && items.some((x) => x.name === chName('飞书')))

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

  // SMTP：未启用时失败 → 启用指向桩后成功
  const t10 = await testCh({ id: smtpId })
  check('NT-29 test SMTP 未启用失败', t10.json?.success === false && String(t10.json?.error).includes('SMTP'), t10.text?.slice(0, 160))
  for (const [key, value] of Object.entries({ SMTP_ENABLED: 'true', SMTP_HOST: '127.0.0.1', SMTP_PORT: String(smtp.port), SMTP_SECURITY: 'none', SMTP_USERNAME: '', SMTP_PASSWORD: '', SMTP_FROM_ADDRESS: 'moai@nt.local', SMTP_FROM_NAME: 'NT E2E' })) {
    const r = await api('PUT', '/api/settings', { token: root, body: { key, value } })
    if (r.status !== 200) throw new Error(`保存设置 ${key} 失败: ${r.status}`)
  }
  smtp.state.messages.length = 0
  const t11 = await testCh({ id: smtpId })
  const smtpMsg = smtp.state.messages.pop()
  check('NT-30 test SMTP 启用后成功且桩捕获', t11.json?.success === true && smtpMsg?.rcpts.join(',').includes('nt@test.local') && smtpMsg?.data.includes('Subject:'), JSON.stringify({ err: t11.json?.error, rcpts: smtpMsg?.rcpts })?.slice(0, 200))
  const t12 = await testCh({ id: insiteId })
  check('NT-31 test 站内推送 toAdmins 成功', t12.json?.success === true, t12.text?.slice(0, 160))

  // ============ 广播（异步 Hangfire） ============
  const enabledCount = (await api('GET', '/api/notification/channel/list', { token: root })).json.items.filter((x) => x.enabled).length
  const bc = await api('POST', '/api/notification/broadcast', { token: root, body: { title: `NT${RUN} 广播一`, content: '通知中心广播正文', level: 'warning' } })
  check('NT-32 广播返回启用渠道数', bc.status === 200 && bc.json?.channelCount === enabledCount, `expect ${enabledCount} got ${bc.json?.channelCount}`)

  // 轮询发送记录终态（Hangfire 调度间隔 10s，最长等 120s）
  let logs = []
  let settled = false
  const failNames = new Set([chName('失败2号'), chName('坏飞书')])
  const okNames = new Set([chName('飞书'), chName('钉钉'), chName('企微'), chName('TG'), chName('通用'), chName('模板'), chName('邮件'), chName('站内')])
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
  check('NT-34 八渠道投递成功', okSettled && bcLogs.filter((x) => okNames.has(x.channelName)).length === 8, JSON.stringify(bcLogs.map((x) => [x.channelName, x.status])))
  check('NT-35 失败渠道终态 failed 且带原因', failSettled && bcLogs.filter((x) => failNames.has(x.channelName)).every((x) => x.status === 'failed' && x.error.length > 0), JSON.stringify(bcLogs.filter((x) => failNames.has(x.channelName)).map((x) => [x.channelName, x.error.slice(0, 60)])))
  const feishuBc = bcLogs.find((x) => x.channelName === chName('飞书'))
  check('NT-36 记录快照含类型/级别/耗时', feishuBc && feishuBc.channelType === 'feishuWebhook' && feishuBc.level === 'warning' && feishuBc.durationMs >= 0)

  // 过滤与分页
  const byChannel = await api('GET', `/api/notification/log/list?pageNo=1&pageSize=50&channelId=${feishuId}`, { token: root })
  check('NT-37 记录按渠道过滤', byChannel.json?.items?.length >= 1 && byChannel.json.items.every((x) => String(x.channelId) === String(feishuId)))
  const byStatus = await api('GET', '/api/notification/log/list?pageNo=1&pageSize=100&status=failed', { token: root })
  check('NT-38 记录按状态过滤', (byStatus.json?.items ?? []).length >= 2 && byStatus.json.items.every((x) => x.status === 'failed'))

  // 禁用渠道不投递
  await api('PUT', `/api/notification/channel/${failId}`, { token: root, body: { name: chName('失败2号'), type: 'webhook', config: JSON.stringify({ url: `${STUB}/fail` }), enabled: false, description: '' } })
  const bc2 = await api('POST', '/api/notification/broadcast', { token: root, body: { title: `NT${RUN} 广播二`, content: '禁用后不投递', level: 'info' } })
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

  // ============ 清理本轮渠道（历史发送记录保留） ============
  for (const id of [...createdIds.filter((x) => String(x) !== String(tplId))]) {
    await api('DELETE', `/api/notification/channel/${id}`, { token: root })
  }

  // ============ 收尾：恢复 SMTP 设置，关闭桩 ============
  for (const key of SMTP_KEYS) {
    const v = ORIG[key]
    if (v !== undefined) await api('PUT', '/api/settings', { token: root, body: { key, value: v } })
  }
  hook.server.close()
  smtp.server.close()

  console.log(`\n==== NT 结果: PASS ${PASS} / FAIL ${FAIL} ====`)
  process.exit(FAIL > 0 ? 1 : 0)
}

main().catch((e) => {
  console.error('E2E 异常终止:', e)
  process.exit(1)
})
