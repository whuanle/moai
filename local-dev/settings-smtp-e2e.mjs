// 邮箱验证（SMTP 服务绑定）E2E（场景 @SET-S61+；旧 SET-S48~S53 随 SMTP 配置收敛到通知中心作废）
// 覆盖：SMTP 连接配置统一在通知中心渠道维护（渠道自带 host/port/security/账号/发件人，创建时校验 400）；
//       系统设置仅保留 SMTP_ENABLED 总开关 + SMTP_CHANNEL_ID 绑定（保存回读、非法绑定 id 400、门禁）；
//       serverinfo 公开门禁联动（启用+绑定可用渠道 → passwordResetEnabled=true；停用/解绑/渠道停用/渠道不存在 → false）；
//       渠道自包含配置真实发信（channel/test 对本地 SMTP 桩：免认证链路信封与 DATA 头、AUTH PLAIN 认证链路）。
// 用法：node local-dev/settings-smtp-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5000）
// 前置：后端运行中；种子账号 admin / abcd123456（root）。开头快照两个设置键原值，结束时恢复并清理所建渠道（共享开发实例不破坏现场）。
import net from 'node:net'

const BASE = process.env.APP_BASE ?? process.argv[2] ?? 'http://127.0.0.1:5000'
let PASS = 0, FAIL = 0
const check = (name, cond, detail = '') => {
  if (cond) { PASS++; console.log(`PASS | ${name}`) }
  else { FAIL++; console.log(`FAIL | ${name} ${detail ? '— ' + detail : ''}`) }
}

let RSA_KEY = ''
const rsa = (plain) => {
  const key = crypto.createPublicKey({ key: Buffer.from(RSA_KEY, 'base64'), format: 'der', type: 'spki' })
  return crypto.publicEncrypt({ key, padding: crypto.constants.RSA_PKCS1_PADDING }, Buffer.from(plain, 'utf-8')).toString('base64')
}
import crypto from 'node:crypto'

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

let seq = 0
const uname = (p) => `${p}${Date.now().toString().slice(-8)}${String(seq++).padStart(2, '0')}`

async function mkuser(p) {
  const name = uname(p)
  const r = await api('POST', '/api/auth/register', { body: { userName: name, email: `${name}@test.local`, nickName: name, phone: `15${Date.now().toString().slice(-8)}${String(seq++)}`.slice(0, 11), password: rsa('Test1234') } })
  if (r.status !== 200) throw new Error(`注册 ${name} 失败: ${r.status} ${r.text.slice(0, 120)}`)
  const l = await api('POST', '/api/auth/login', { body: { userName: name, password: rsa('Test1234') } })
  return { name, token: l.json.accessToken }
}

/** 极简 SMTP 桩：HELO/EHLO → AUTH(PLAIN/LOGIN) → MAIL/RCPT/DATA → QUIT，记录收到的信封与 DATA 原文 */
function startSmtpStub({ requireAuth = false } = {}) {
  const state = { messages: [], authLines: [], authOk: false }
  const server = net.createServer((socket) => {
    let buffer = ''
    let inData = false
    let current = null
    let authStep = 0 // 0=命令态 1=等 AUTH LOGIN 用户名 2=等密码 3=等 AUTH PLAIN 无初始响应的载荷
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
          if (line === '.') {
            inData = false
            state.messages.push(current)
            current = null
            reply('250 OK queued')
          } else {
            current.data += line + '\r\n'
          }
          continue
        }
        if (authStep === 1) {
          state.authLines.push('user:' + Buffer.from(line, 'base64').toString('utf-8'))
          authStep = 2
          reply('334 UGFzc3dvcmQ6')
          continue
        }
        if (authStep === 2) {
          state.authLines.push('pass:' + Buffer.from(line, 'base64').toString('utf-8'))
          authStep = 0
          state.authOk = true
          reply('235 OK')
          continue
        }
        if (authStep === 3) {
          state.authLines.push('plain:' + Buffer.from(line, 'base64').toString('utf-8'))
          authStep = 0
          state.authOk = true
          reply('235 OK')
          continue
        }
        const cmd = line.toUpperCase()
        if (cmd.startsWith('EHLO') || cmd.startsWith('HELO')) {
          reply('250-stub')
          reply('250-SIZE 26214400')
          reply(requireAuth ? '250-AUTH PLAIN LOGIN' : '250 OK')
          if (!requireAuth) { /* 已以 250 OK 结束 */ } else { reply('250 OK') }
        } else if (cmd.startsWith('AUTH')) {
          const parts = line.split(' ')
          if (parts[1] === 'PLAIN') {
            if (parts[2]) {
              state.authLines.push('plain:' + Buffer.from(parts[2], 'base64').toString('utf-8'))
              state.authOk = true
              reply('235 OK')
            } else {
              authStep = 3
              reply('334 ')
            }
          } else if (parts[1] === 'LOGIN') {
            authStep = 1
            reply('334 VXNlcm5hbWU6')
          } else {
            reply('504 not supported')
          }
        } else if (cmd.startsWith('MAIL FROM:')) {
          if (requireAuth && !state.authOk) {
            reply('530 authentication required')
          } else {
            current = { from: line, rcpts: [], data: '' }
            reply('250 OK')
          }
        } else if (cmd.startsWith('RCPT TO:')) {
          current?.rcpts.push(line)
          reply('250 OK')
        } else if (cmd === 'DATA') {
          inData = true
          reply('354 end with <CRLF>.<CRLF>')
        } else if (cmd === 'QUIT') {
          reply('221 bye')
          socket.end()
        } else {
          reply('250 OK')
        }
      }
    })
  })
  return new Promise((resolve) => {
    server.listen(0, '127.0.0.1', () => resolve({ server, port: server.address().port, state }))
  })
}

const putSetting = (token, key, value) => api('PUT', '/api/settings', { token, body: { key, value } })
const BIND_KEYS = ['SMTP_ENABLED', 'SMTP_CHANNEL_ID']
const getSettingValue = async (token, key) => {
  const q = await api('GET', '/api/settings', { token })
  return (q.json?.items ?? []).find((s) => s.key === key)?.value
}
const getServerInfo = async () => (await api('GET', '/api/common/serverinfo')).json

// 渠道配置不再含收件人（后端收敛到请求体）：试发收件人在 channel/test 请求体显式传入
const TEST_RCPT = 'inbox@test.local'
const smtpConfig = (port, extra = {}) => JSON.stringify({
  host: '127.0.0.1',
  port,
  security: 'none',
  username: '',
  password: '',
  fromAddress: 'moai@test.local',
  fromName: 'MoAI E2E',
  ...extra,
})
const createChannel = (token, config, name) => api('POST', '/api/notification/channel/create', { token, body: { name, type: 'smtp', config, enabled: true, description: 'settings-smtp-e2e' } })
const updateChannel = (token, id, config, enabled = true) => api('PUT', `/api/notification/channel/${id}`, { token, body: { id, name: `smtp-e2e-${id}`, type: 'smtp', config, enabled, description: 'settings-smtp-e2e' } })
const deleteChannel = (token, id) => api('DELETE', `/api/notification/channel/${id}`, { token })
const testChannel = (token, id) => api('POST', '/api/notification/channel/test', { token, body: { id: String(id), recipients: [TEST_RCPT] } })

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const l = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
  const root = l.json?.accessToken ?? null
  check('SET-61a root 登录', Boolean(root))

  // ============ 快照原值（结束时恢复） ============
  const ORIG = {}
  for (const key of BIND_KEYS) ORIG[key] = await getSettingValue(root, key)

  const member = await mkuser('ss')
  const stamp = Date.now().toString().slice(-8)

  // ============ 渠道连接配置校验（连接配置统一在通知中心） ============
  // SET-62 渠道创建时校验连接配置：host 必填、端口范围、加密方式枚举
  check('SET-62a host 为空 400', (await createChannel(root, smtpConfig(1000, { host: '' }), `smtp-e2e-${stamp}-a`)).status === 400)
  check('SET-62b port 70000 400', (await createChannel(root, smtpConfig(1000, { port: 70000 }), `smtp-e2e-${stamp}-b`)).status === 400)
  check('SET-62c security tls 400', (await createChannel(root, smtpConfig(1000, { security: 'tls' }), `smtp-e2e-${stamp}-c`)).status === 400)
  check('SET-62d fromAddress 非法 400', (await createChannel(root, smtpConfig(1000, { fromAddress: 'not-an-email' }), `smtp-e2e-${stamp}-d`)).status === 400)
  const stub1 = await startSmtpStub()
  const created = await createChannel(root, smtpConfig(stub1.port), `smtp-e2e-${stamp}`)
  check('SET-62e 合法配置创建渠道 200', created.status === 200 && Boolean(created.json?.id), `${created.status} ${created.text.slice(0, 120)}`)
  const channelId = created.json?.id

  // ============ 绑定保存与校验 ============
  // SET-63 系统设置仅绑定渠道 id：保存回读、非法绑定值 400
  check('SET-63a 绑定渠道 id 保存 200', (await putSetting(root, 'SMTP_CHANNEL_ID', String(channelId))).status === 200)
  check('SET-63b 开关保存 200', (await putSetting(root, 'SMTP_ENABLED', 'true')).status === 200)
  check('SET-63c 绑定回读一致', (await getSettingValue(root, 'SMTP_CHANNEL_ID')) === String(channelId))
  check('SET-63d 绑定 id 非数字 400', (await putSetting(root, 'SMTP_CHANNEL_ID', 'abc')).status === 400)
  check('SET-63e 绑定 id 负数 400', (await putSetting(root, 'SMTP_CHANNEL_ID', '-1')).status === 400)
  check('SET-63f 开关 yes 400', (await putSetting(root, 'SMTP_ENABLED', 'yes')).status === 400)

  // SET-63g~j 验证码通知模板绑定 id：空合法（回退内置模板）、格式非法 400
  check('SET-63g 注册验证码模板 id 空值合法', (await putSetting(root, 'SMTP_REGISTER_CODE_TEMPLATE_ID', '')).status === 200)
  check('SET-63h 注册验证码模板 id 非数字 400', (await putSetting(root, 'SMTP_REGISTER_CODE_TEMPLATE_ID', 'abc')).status === 400)
  check('SET-63i 登录验证码模板 id 负数 400', (await putSetting(root, 'SMTP_LOGIN_CODE_TEMPLATE_ID', '-1')).status === 400)
  check('SET-63j 找回密码验证码模板 id 空值合法', (await putSetting(root, 'SMTP_RESET_CODE_TEMPLATE_ID', '')).status === 200)

  // ============ 门禁 ============
  // SET-64 非 root 修改绑定 403、未登录 401
  check('SET-64a member 保存开关 403', (await putSetting(member.token, 'SMTP_ENABLED', 'true')).status === 403)
  check('SET-64b 未登录保存绑定 401', (await putSetting(undefined, 'SMTP_CHANNEL_ID', '1')).status === 401)

  // ============ serverinfo 公开门禁联动 ============
  // SET-65 passwordResetEnabled = 开关开启 且 绑定的 SMTP 渠道存在并启用
  await putSetting(root, 'SMTP_ENABLED', 'false')
  check('SET-65a 关闭开关时 passwordResetEnabled=false', (await getServerInfo()).passwordResetEnabled === false)

  await putSetting(root, 'SMTP_ENABLED', 'true')
  check('SET-65b 启用+绑定可用渠道时 passwordResetEnabled=true', (await getServerInfo()).passwordResetEnabled === true)
  check('SET-65c 注册验证码开关关闭时 registerEmailCodeRequired=false', (await getServerInfo()).registerEmailCodeRequired === false)

  await putSetting(root, 'SMTP_CHANNEL_ID', '')
  check('SET-65d 解绑后 passwordResetEnabled=false', (await getServerInfo()).passwordResetEnabled === false)
  await putSetting(root, 'SMTP_CHANNEL_ID', String(channelId))

  check('SET-65e 绑定不存在的渠道时 passwordResetEnabled=false', await (async () => {
    await putSetting(root, 'SMTP_CHANNEL_ID', '999999999')
    return (await getServerInfo()).passwordResetEnabled === false
  })())
  await putSetting(root, 'SMTP_CHANNEL_ID', String(channelId))

  await updateChannel(root, channelId, smtpConfig(stub1.port), false)
  check('SET-65f 绑定渠道停用后 passwordResetEnabled=false', (await getServerInfo()).passwordResetEnabled === false)
  await updateChannel(root, channelId, smtpConfig(stub1.port), true)
  check('SET-65g 渠道恢复启用后 passwordResetEnabled=true', (await getServerInfo()).passwordResetEnabled === true)

  // ============ 渠道自包含配置真实发信（免认证中继） ============
  // SET-66 渠道连接配置自带主机/端口/发件人，channel/test 直连本地 SMTP 桩投递
  {
    const r = await testChannel(root, channelId)
    check('SET-66a 渠道测试发送 success=true', r.status === 200 && r.json?.success === true, `${r.status} ${r.text.slice(0, 160)}`)
    await new Promise((resolve) => setTimeout(resolve, 300))
    const msg = stub1.state.messages[0]
    check('SET-66b 桩收到 1 封邮件', stub1.state.messages.length === 1, `实际 ${stub1.state.messages.length}`)
    check('SET-66c 信封 MAIL FROM=渠道发件人', Boolean(msg?.from.includes('moai@test.local')), msg?.from)
    check('SET-66d 信封 RCPT TO=试发指定收件人', Boolean(msg?.rcpts.some((x) => x.includes(TEST_RCPT))), JSON.stringify(msg?.rcpts))
    check('SET-66e DATA 含 Subject 与 Message-Id 头', Boolean(msg?.data.includes('Subject:') && /Message-Id:/i.test(msg.data)), (msg?.data ?? '').slice(0, 200))
  }

  // ============ SMTP 认证链路（渠道自带账号密码） ============
  // SET-67 渠道配置账号密码后测试发送，桩要求认证并核对 AUTH PLAIN 凭据
  const stub2 = await startSmtpStub({ requireAuth: true })
  await updateChannel(root, channelId, smtpConfig(stub2.port, { username: 'notify@test.local', password: 'authcode-123', security: 'none' }), true)
  {
    const r = await testChannel(root, channelId)
    check('SET-67a 带认证渠道测试发送 success=true', r.status === 200 && r.json?.success === true, `${r.status} ${r.text.slice(0, 160)}`)
    await new Promise((resolve) => setTimeout(resolve, 300))
    const auth = stub2.state.authLines.join(' | ')
    check('SET-67b 桩记录 AUTH 认证', stub2.state.authOk, auth)
    check('SET-67c AUTH 凭据与渠道配置一致', auth.includes('notify@test.local') && auth.includes('authcode-123'), auth.slice(0, 160))
    check('SET-67d 认证后桩收到邮件', stub2.state.messages.length === 1, `实际 ${stub2.state.messages.length}`)
  }

  // ============ 清理与恢复执行前原值 ============
  check('SET-91 删除所建渠道 200', (await deleteChannel(root, channelId)).status === 200)
  for (const key of BIND_KEYS) {
    if (ORIG[key] !== undefined) await putSetting(root, key, ORIG[key])
  }
  {
    const readback = {}
    for (const key of BIND_KEYS) readback[key] = await getSettingValue(root, key)
    const ok = BIND_KEYS.every((k) => (ORIG[k] ?? '') === (readback[k] ?? ''))
    check('SET-90 结束恢复执行前原值（开关与绑定）', ok, JSON.stringify({ ORIG, readback }))
  }

  stub1.server.close()
  stub2.server.close()
  console.log(`\n总计: PASS ${PASS} / FAIL ${FAIL}`)
  if (FAIL > 0) process.exit(1)
}

main().catch((e) => { console.error('E2E 执行异常:', e); process.exit(1) })
