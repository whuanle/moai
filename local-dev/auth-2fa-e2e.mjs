// 两步验证（2FA/TOTP）与登录邮箱验证码 E2E（场景 @AUTH-S36+）
// 覆盖：设置校验（LOGIN_FORCED_2FA 布尔 / LOGIN_EMAIL_CODE_MODE 枚举）；TOTP 绑定全流程（setup→enable→status→disable）与错码拒绝；
//       登录 TOTP 挑战（secondFactor=totp + 一次性票据 / 错码 400 / 票据消费后复用 401 / 动态码防重放）；系统纯 node 实现与后端 TotpHelper 端对拍；
//       强制 2FA：未绑定登录拿 mfa_pending 受限 token → 非白名单接口 403 → 白名单（userinfo/two_factor）可用 → 绑定后旧 token 立即恢复；
//       强制期间关闭 400；root 重置用户 2FA（目标保护：root 自身 400 / 非管理员 403）；登录邮箱验证码三模式
//       （off 直接登录 / always 发码验证 / after_fail 失败一次后需要）+ 公共发码端点拒绝 login 用途 + 重发冷却 429 + SMTP 未启用策略失效。
// 用法：node local-dev/auth-2fa-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5000）
// 前置：后端运行中；种子账号 admin / abcd123456（root）。开头快照相关设置原值，结束时恢复（不破坏共享实例配置）。
import net from 'node:net'
import crypto from 'node:crypto'

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

async function login(userName, password) {
  return api('POST', '/api/auth/login', { body: { userName, password: rsa(password) } })
}

/** 注册用户（注册验证码关闭状态下直接注册），返回 { name, email, password } */
async function registerUser(prefix) {
  const name = uname(prefix)
  const r = await api('POST', '/api/auth/register', {
    body: {
      userName: name,
      email: `${name}@test.local`,
      nickName: name,
      phone: `15${Date.now().toString().slice(-9)}${seq % 10}`.slice(0, 11),
      password: rsa('Test1234'),
    },
  })
  if (r.status !== 200) throw new Error(`注册 ${name} 失败: ${r.status} ${r.text.slice(0, 160)}`)
  return { name, email: `${name}@test.local`, password: 'Test1234' }
}

// ---------- 纯 node TOTP（RFC 6238，与后端 TotpHelper 端对拍） ----------
const B32 = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567'
function base32Decode(s) {
  let bits = 0
  let value = 0
  const out = []
  for (const c of s.replace(/=+$/, '').toUpperCase()) {
    const idx = B32.indexOf(c)
    if (idx < 0) continue
    value = (value << 5) | idx
    bits += 5
    if (bits >= 8) {
      out.push((value >> (bits - 8)) & 0xff)
      bits -= 8
    }
  }
  return Buffer.from(out)
}
function totp(secretBase32, timeStep) {
  const counter = Buffer.alloc(8)
  counter.writeBigInt64BE(BigInt(timeStep))
  const hmac = crypto.createHmac('sha1', base32Decode(secretBase32)).update(counter).digest()
  const offset = hmac[hmac.length - 1] & 0xf
  const binary = ((hmac[offset] & 0x7f) << 24) | (hmac[offset + 1] << 16) | (hmac[offset + 2] << 8) | hmac[offset + 3]
  return String(binary % 1_000_000).padStart(6, '0')
}
const currentStep = () => Math.floor(Date.now() / 1000 / 30)

/** 极简 SMTP 桩：HELO/EHLO → MAIL/RCPT/DATA → QUIT，记录收到的信封与 DATA 原文 */
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
        const cmd = line.toUpperCase()
        if (cmd.startsWith('EHLO') || cmd.startsWith('HELO')) {
          reply('250-stub')
          reply('250 OK')
        } else if (cmd.startsWith('MAIL FROM:')) {
          current = { from: line, rcpts: [], data: '' }
          reply('250 OK')
        } else if (cmd.startsWith('RCPT TO:')) {
          current?.rcpts.push(line)
          reply('250 OK')
        } else if (cmd.startsWith('DATA')) {
          inData = true
          reply('354 end with <CRLF>.<CRLF>')
        } else if (cmd.startsWith('QUIT')) {
          reply('221 bye')
          socket.end()
        } else {
          reply('250 OK')
        }
      }
    })
  })
  return new Promise((resolve) => {
    server.listen(0, '127.0.0.1', () => {
      resolve({ state, port: server.address().port, close: () => server.close() })
    })
  })
}

function decodeTextBody(data) {
  const idx = data.indexOf('\r\n\r\n')
  if (idx < 0) return ''
  const headers = data.slice(0, idx)
  const body = data.slice(idx + 4)
  const cte = headers.match(/Content-Transfer-Encoding: (\S+)/i)
  const encoding = cte?.[1]?.toLowerCase()
  if (encoding === 'base64') {
    return Buffer.from(body.replace(/\s+/g, ''), 'base64').toString('utf-8')
  }
  if (encoding === 'quoted-printable') {
    return body
      .replace(/=\r?\n/g, '')
      .replace(/=([A-Fa-f0-9]{2})/g, (_, hex) => Buffer.from(hex, 'hex').toString('latin1'))
  }
  return body
}

const extractCode = (text) => text.match(/(\d{6})/)?.[1] ?? ''

const SNAPSHOT_KEYS = [
  'SMTP_ENABLED', 'SMTP_CHANNEL_ID',
  'LOGIN_FORCED_2FA', 'LOGIN_EMAIL_CODE_MODE', 'REGISTER_EMAIL_CODE_ENABLED',
]
// 渠道名唯一（部分唯一索引），按运行时间戳命名避免异常残留导致重跑 409
const SMTP_CHANNEL_NAME = `auth-2fa-e2e-${Date.now().toString().slice(-8)}`

async function main() {
  // ---------- 准备：RSA 公钥 + root 登录 + 快照设置 ----------
  RSA_KEY = (await api('GET', '/api/common/serverinfo')).json.rsaPublic

  const loginRes = await login('admin', 'abcd123456')
  if (loginRes.status !== 200) throw new Error(`admin 登录失败: ${loginRes.status} ${loginRes.text.slice(0, 200)}`)
  const rootToken = loginRes.json.accessToken
  const rootUserId = loginRes.json.userId

  const settingsRes = await api('GET', '/api/settings', { token: rootToken })
  const valueOf = (key) => settingsRes.json?.items?.find((s) => s.key === key)?.value ?? ''
  const snapshot = {}
  for (const key of SNAPSHOT_KEYS) snapshot[key] = valueOf(key)

  const saveSetting = async (key, value) => {
    const r = await api('PUT', '/api/settings', { token: rootToken, body: { key, value } })
    if (r.status !== 200) throw new Error(`保存设置 ${key}=${value} 失败: ${r.status} ${r.text.slice(0, 140)}`)
  }

  const stub = await startSmtpStub()
  // SMTP 连接配置统一在通知中心渠道维护：upsert 指向本地桩的 SMTP 渠道并绑定到系统设置
  let smtpChannelId = null
  const upsertSmtpChannel = async () => {
    const config = JSON.stringify({
      host: '127.0.0.1',
      port: stub.port,
      security: 'none',
      username: '',
      password: '',
      fromAddress: 'moai-test@test.local',
      fromName: 'MoAI E2E',
    })
    if (smtpChannelId == null) {
      const r = await api('POST', '/api/notification/channel/create', { token: rootToken, body: { name: SMTP_CHANNEL_NAME, type: 'smtp', config, enabled: true, description: 'auth-2fa-e2e' } })
      if (r.status !== 200) throw new Error(`创建 SMTP 渠道失败: ${r.status} ${r.text.slice(0, 140)}`)
      smtpChannelId = r.json?.id
      if (smtpChannelId == null) throw new Error('创建 SMTP 渠道未返回 id')
    } else {
      const r = await api('PUT', `/api/notification/channel/${smtpChannelId}`, { token: rootToken, body: { id: smtpChannelId, name: SMTP_CHANNEL_NAME, type: 'smtp', config, enabled: true, description: 'auth-2fa-e2e' } })
      if (r.status !== 200) throw new Error(`更新 SMTP 渠道失败: ${r.status} ${r.text.slice(0, 140)}`)
    }
    await saveSetting('SMTP_CHANNEL_ID', String(smtpChannelId))
  }
  const smtpUp = async () => {
    await upsertSmtpChannel()
    await saveSetting('SMTP_ENABLED', 'true')
  }
  const smtpDown = async () => {
    await saveSetting('SMTP_ENABLED', 'false')
  }

  /** 完成一次 2FA 绑定：setup → enable(当前动态码) */
  const bindTotp = async (token) => {
    const setup = await api('POST', '/api/auth/two_factor/setup', { token })
    if (setup.status !== 200) throw new Error(`setup 失败: ${setup.status} ${setup.text.slice(0, 140)}`)
    const enable = await api('POST', '/api/auth/two_factor/enable', { token, body: { code: totp(setup.json.secret, currentStep()) } })
    if (enable.status !== 200) throw new Error(`enable 失败: ${enable.status} ${enable.text.slice(0, 140)}`)
    return setup.json.secret
  }

  try {
    // ---------- 基线 ----------
    await smtpDown()
    await saveSetting('LOGIN_FORCED_2FA', 'false')
    await saveSetting('LOGIN_EMAIL_CODE_MODE', 'off')
    await saveSetting('REGISTER_EMAIL_CODE_ENABLED', 'false')

    // ---------- 设置校验（SET-S59~S60） ----------
    let r = await api('PUT', '/api/settings', { token: rootToken, body: { key: 'LOGIN_FORCED_2FA', value: 'yes' } })
    check('SET-59a 强制2FA开关非法值 400', r.status === 400 && r.text.includes('true 或 false'), `${r.status} ${r.text.slice(0, 140)}`)
    r = await api('PUT', '/api/settings', { token: rootToken, body: { key: 'LOGIN_EMAIL_CODE_MODE', value: 'sometimes' } })
    check('SET-60a 登录验证码策略非法值 400', r.status === 400 && r.text.includes('off、always 或 after_fail'), `${r.status} ${r.text.slice(0, 140)}`)

    // ---------- 策略基线：off + 不强制，登录直达 ----------
    const userA = await registerUser('fa')
    r = await login(userA.name, userA.password)
    check('AUTH-36a 策略关闭时登录直接颁发令牌', r.status === 200 && r.json.accessToken && r.json.secondFactor === 'none' && r.json.mfaPending === false, JSON.stringify({ sf: r.json?.secondFactor, mfa: r.json?.mfaPending, at: !!r.json?.accessToken }))
    const tokenA = r.json.accessToken

    // ---------- 2FA 绑定全流程（AUTH-S37） ----------
    r = await api('GET', '/api/auth/two_factor/status', {})
    check('AUTH-37a 未登录查询状态 401', r.status === 401, `${r.status}`)

    r = await api('GET', '/api/auth/two_factor/status', { token: tokenA })
    check('AUTH-37b 初始状态 enabled=false forced=false', r.status === 200 && r.json.enabled === false && r.json.forced === false, r.text.slice(0, 140))

    r = await api('POST', '/api/auth/two_factor/setup', { token: tokenA })
    const secretA = r.json?.secret ?? ''
    check('AUTH-37c setup 返回密钥与 otpauth URI', r.status === 200 && secretA.length === 32 && r.json.otpauthUri.includes(`secret=${secretA}`) && r.json.otpauthUri.includes('otpauth://totp/') && r.json.otpauthUri.includes('issuer='), r.text.slice(0, 200))

    r = await api('POST', '/api/auth/two_factor/enable', { token: tokenA, body: { code: '999999' } })
    check('AUTH-37d 错误动态码启用 400', r.status === 400 && r.text.includes('动态码错误'), `${r.status} ${r.text.slice(0, 140)}`)

    r = await api('POST', '/api/auth/two_factor/enable', { token: tokenA, body: { code: '12345' } })
    check('AUTH-37e 非 6 位动态码校验 400', r.status === 400, `${r.status} ${r.text.slice(0, 140)}`)

    r = await api('POST', '/api/auth/two_factor/enable', { token: tokenA, body: { code: totp(secretA, currentStep()) } })
    check('AUTH-37f 正确动态码启用成功', r.status === 200, `${r.status} ${r.text.slice(0, 140)}`)

    r = await api('POST', '/api/auth/two_factor/setup', { token: tokenA })
    check('AUTH-37g 已启用后重复 setup 400', r.status === 400 && r.text.includes('无需重复绑定'), `${r.status} ${r.text.slice(0, 140)}`)

    r = await api('GET', '/api/auth/two_factor/status', { token: tokenA })
    check('AUTH-37h 启用后状态 enabled=true', r.status === 200 && r.json.enabled === true, r.text.slice(0, 140))

    // ---------- TOTP 登录挑战（AUTH-S38） ----------
    r = await login(userA.name, userA.password)
    check('AUTH-38a 登录返回 totp 挑战（无令牌有票据）', r.status === 200 && r.json.secondFactor === 'totp' && r.json.loginTicket && !r.json.accessToken && r.json.mfaPending === false, JSON.stringify({ sf: r.json?.secondFactor, tk: !!r.json?.loginTicket, at: !!r.json?.accessToken }))

    // 错误动态码（消耗一次性票据）
    r = await api('POST', '/api/auth/login/verify', { body: { loginTicket: r.json.loginTicket, code: '999999' } })
    check('AUTH-38b 错误动态码验证 400', r.status === 400 && r.text.includes('动态码错误'), `${r.status} ${r.text.slice(0, 140)}`)

    // 正确动态码（新票据）
    const verifyStep = currentStep()
    r = await login(userA.name, userA.password)
    const okTicket = r.json.loginTicket
    r = await api('POST', '/api/auth/login/verify', { body: { loginTicket: okTicket, code: totp(secretA, verifyStep) } })
    check('AUTH-38c 正确动态码验证颁发令牌', r.status === 200 && r.json.accessToken && r.json.secondFactor === 'none', JSON.stringify({ at: !!r.json?.accessToken, sf: r.json?.secondFactor }))
    const tokenA2 = r.json.accessToken

    r = await api('POST', '/api/auth/login/verify', { body: { loginTicket: okTicket, code: totp(secretA, verifyStep) } })
    check('AUTH-38d 票据一次性：消费后复用 401', r.status === 401, `${r.status} ${r.text.slice(0, 140)}`)

    // 动态码防重放：同一时间步长的动态码只能用一次，下一时间步的码可用
    r = await login(userA.name, userA.password)
    const replayTicket = r.json.loginTicket
    r = await api('POST', '/api/auth/login/verify', { body: { loginTicket: replayTicket, code: totp(secretA, verifyStep) } })
    check('AUTH-38e 同一步长动态码重放 400', r.status === 400 && r.text.includes('已被使用'), `${r.status} ${r.text.slice(0, 140)}`)

    r = await login(userA.name, userA.password)
    const nextTicket = r.json.loginTicket
    r = await api('POST', '/api/auth/login/verify', { body: { loginTicket: nextTicket, code: totp(secretA, verifyStep + 1) } })
    check('AUTH-38f 下一时间步动态码可验证（防重放不误伤）', r.status === 200 && r.json.accessToken, `${r.status} ${r.text.slice(0, 140)}`)

    // ---------- 关闭 2FA（AUTH-S39） ----------
    r = await api('POST', '/api/auth/two_factor/disable', { token: tokenA2, body: { code: '999999' } })
    check('AUTH-39a 错误动态码关闭 400', r.status === 400 && r.text.includes('动态码错误'), `${r.status} ${r.text.slice(0, 140)}`)

    r = await api('POST', '/api/auth/two_factor/disable', { token: tokenA2, body: { code: totp(secretA, currentStep()) } })
    check('AUTH-39b 正确动态码关闭成功', r.status === 200, `${r.status} ${r.text.slice(0, 140)}`)

    r = await api('GET', '/api/auth/two_factor/status', { token: tokenA2 })
    check('AUTH-39c 关闭后状态 enabled=false', r.status === 200 && r.json.enabled === false, r.text.slice(0, 140))

    r = await login(userA.name, userA.password)
    check('AUTH-39d 关闭后登录直达（无挑战）', r.status === 200 && r.json.accessToken && r.json.secondFactor === 'none', JSON.stringify({ sf: r.json?.secondFactor }))
    const tokenA3 = r.json.accessToken

    r = await api('POST', '/api/auth/two_factor/disable', { token: tokenA3, body: { code: '123456' } })
    check('AUTH-39e 未启用时关闭 400', r.status === 400 && r.text.includes('尚未开启'), `${r.status} ${r.text.slice(0, 140)}`)

    // ---------- 强制 2FA（AUTH-S40） ----------
    await saveSetting('LOGIN_FORCED_2FA', 'true')
    const userB = await registerUser('fb')
    r = await login(userB.name, userB.password)
    check('AUTH-40a 强制模式下未绑定登录带 mfaPending', r.status === 200 && r.json.accessToken && r.json.mfaPending === true && r.json.secondFactor === 'none', JSON.stringify({ mfa: r.json?.mfaPending }))
    const tokenB = r.json.accessToken

    r = await api('GET', '/api/account/bound_accounts', { token: tokenB })
    check('AUTH-40b 未绑定时非白名单接口 403', r.status === 403 && r.text.includes('请先绑定两步验证'), `${r.status} ${r.text.slice(0, 140)}`)

    r = await api('GET', '/api/account/userinfo', { token: tokenB })
    check('AUTH-40c 白名单 userinfo 可用且回显 mfaPending', r.status === 200 && r.json.mfaPending === true && r.json.twoFactorEnabled === false, r.text.slice(0, 160))

    r = await api('GET', '/api/auth/two_factor/status', { token: tokenB })
    check('AUTH-40d 白名单 2FA 状态可用且 forced=true', r.status === 200 && r.json.forced === true, r.text.slice(0, 140))

    await bindTotp(tokenB)

    r = await api('GET', '/api/account/userinfo', { token: tokenB })
    check('AUTH-40e 绑定后 userinfo mfaPending 复位', r.status === 200 && r.json.mfaPending === false && r.json.twoFactorEnabled === true, r.text.slice(0, 160))

    r = await api('GET', '/api/account/bound_accounts', { token: tokenB })
    check('AUTH-40f 绑定后旧 token 立即恢复完整访问（无需重登）', r.status === 200, `${r.status} ${r.text.slice(0, 140)}`)

    r = await api('POST', '/api/auth/two_factor/disable', { token: tokenB, body: { code: '999999' } })
    check('AUTH-40g 强制模式下关闭 400', r.status === 400 && r.text.includes('强制开启'), `${r.status} ${r.text.slice(0, 140)}`)

    // ---------- root 重置 2FA（AUTH-S41） ----------
    const usersRes = await api('GET', '/api/usermanage/users?pageNo=1&pageSize=50&searchText=' + userB.name, { token: rootToken })
    const userBId = usersRes.json?.items?.find((u) => u.userName === userB.name)?.id
    check('AUTH-41a 管理员用户列表可查到目标用户', !!userBId, usersRes.text.slice(0, 140))

    r = await api('PUT', `/api/usermanage/user/${userBId}/reset_2fa`, { token: rootToken })
    check('AUTH-41b root 重置用户 2FA 成功', r.status === 200, `${r.status} ${r.text.slice(0, 140)}`)

    r = await api('GET', '/api/auth/two_factor/status', { token: tokenB })
    check('AUTH-41c 重置后状态 enabled=false', r.status === 200 && r.json.enabled === false, r.text.slice(0, 140))

    r = await login(userB.name, userB.password)
    check('AUTH-41d 重置后登录回到强制绑定流程（mfaPending）', r.status === 200 && r.json.mfaPending === true, JSON.stringify({ mfa: r.json?.mfaPending }))
    const tokenB2 = r.json.accessToken

    r = await api('PUT', `/api/usermanage/user/${rootUserId}/reset_2fa`, { token: rootToken })
    check('AUTH-41e root 自身受保护 400', r.status === 400 && r.text.includes('超级管理员'), `${r.status} ${r.text.slice(0, 140)}`)

    r = await api('PUT', `/api/usermanage/user/${userBId}/reset_2fa`, { token: tokenA3 })
    check('AUTH-41f 非管理员重置 403', r.status === 403, `${r.status} ${r.text.slice(0, 140)}`)

    // 未绑定 2FA 的用户重置 400
    const userE = await registerUser('fe')
    const usersResE = await api('GET', '/api/usermanage/users?pageNo=1&pageSize=50&searchText=' + userE.name, { token: rootToken })
    const userEId = usersResE.json?.items?.find((u) => u.userName === userE.name)?.id
    r = await api('PUT', `/api/usermanage/user/${userEId}/reset_2fa`, { token: rootToken })
    check('AUTH-41g 未绑定 2FA 时重置 400', r.status === 400 && r.text.includes('尚未绑定'), `${r.status} ${r.text.slice(0, 140)}`)
    void tokenB2

    // ---------- 登录邮箱验证码（AUTH-S42） ----------
    await saveSetting('LOGIN_FORCED_2FA', 'false')
    await smtpUp()

    // 公共匿名发码端点拒绝 login 用途（防轰炸）
    r = await api('POST', '/api/auth/email_code/send', { body: { purpose: 'login', email: userB.email } })
    check('AUTH-42a 公共发码端点拒绝 login 用途 400', r.status === 400, `${r.status} ${r.text.slice(0, 140)}`)

    // always 模式
    await saveSetting('LOGIN_EMAIL_CODE_MODE', 'always')
    const mailCountBefore = stub.state.messages.length
    r = await login(userB.name, userB.password)
    check('AUTH-42b always 模式登录返回 emailCode 挑战', r.status === 200 && r.json.secondFactor === 'emailCode' && r.json.loginTicket && !r.json.accessToken, JSON.stringify({ sf: r.json?.secondFactor }))
    const ticketB = r.json.loginTicket
    check('AUTH-42c always 模式自动发送验证码邮件', stub.state.messages.length === mailCountBefore + 1 && stub.state.messages.at(-1).rcpts.join().includes(userB.email), JSON.stringify(stub.state.messages.at(-1)?.rcpts))
    const loginCodeB = extractCode(decodeTextBody(stub.state.messages.at(-1).data))
    check('AUTH-42d 登录验证码邮件含 6 位码', loginCodeB.length === 6, decodeTextBody(stub.state.messages.at(-1).data).slice(0, 160))

    r = await api('POST', '/api/auth/login_code/send', { body: { loginTicket: ticketB } })
    check('AUTH-42e 冷却期内重发 429', r.status === 429, `${r.status} ${r.text.slice(0, 140)}`)

    r = await api('POST', '/api/auth/login/verify', { body: { loginTicket: ticketB, code: '000000' } })
    check('AUTH-42f 错误邮箱验证码 400', r.status === 400 && r.text.includes('验证码错误'), `${r.status} ${r.text.slice(0, 140)}`)

    // 冷却期内重新提交登录：容忍 429 发新票据，续用仍在有效期的验证码（不重发邮件）
    const mailCountRetry = stub.state.messages.length
    r = await login(userB.name, userB.password)
    check('AUTH-42g1 冷却期内重登不重发邮件且签发新票据', r.status === 200 && r.json.secondFactor === 'emailCode' && r.json.loginTicket && stub.state.messages.length === mailCountRetry, JSON.stringify({ sf: r.json?.secondFactor, mails: stub.state.messages.length }))
    const ticketB2 = r.json.loginTicket

    r = await api('POST', '/api/auth/login/verify', { body: { loginTicket: ticketB2, code: loginCodeB } })
    check('AUTH-42g 正确邮箱验证码登录成功', r.status === 200 && r.json.accessToken && r.json.mfaPending === false, `${r.status} ${r.text.slice(0, 140)}`)
    const tokenB3 = r.json.accessToken

    // 已绑定 2FA 的用户：TOTP 优先，不走邮箱验证码
    const secretA2 = await bindTotp(tokenB3)
    const mailCountPrecedence = stub.state.messages.length
    r = await login(userB.name, userB.password)
    check('AUTH-42h 已绑定 2FA 时 TOTP 优先于邮箱验证码', r.status === 200 && r.json.secondFactor === 'totp' && stub.state.messages.length === mailCountPrecedence, JSON.stringify({ sf: r.json?.secondFactor }))
    const ticketPrecedence = r.json.loginTicket
    r = await api('POST', '/api/auth/login/verify', { body: { loginTicket: ticketPrecedence, code: totp(secretA2, currentStep()) } })
    check('AUTH-42i TOTP 优先链路验证通过', r.status === 200 && r.json.accessToken, `${r.status}`)
    // 恢复非强制后可关闭 2FA
    r = await api('POST', '/api/auth/two_factor/disable', { token: tokenB3, body: { code: totp(secretA2, currentStep()) } })
    check('AUTH-42j 恢复非强制后可关闭 2FA', r.status === 200, `${r.status} ${r.text.slice(0, 140)}`)

    // after_fail 模式：失败一次后需要验证码，成功后解除
    await saveSetting('LOGIN_EMAIL_CODE_MODE', 'after_fail')
    const userC = await registerUser('fc')
    r = await login(userC.name, userC.password)
    check('AUTH-42k after_fail 无失败记录时直接登录', r.status === 200 && r.json.secondFactor === 'none' && r.json.accessToken, JSON.stringify({ sf: r.json?.secondFactor }))

    r = await login(userC.name, 'WrongPass999')
    check('AUTH-42l 密码错误 401', r.status === 401, `${r.status}`)

    const mailToUserC = () => stub.state.messages.filter((m) => m.rcpts.join().includes(userC.email)).length
    const mailCountAfterFail = mailToUserC()
    r = await login(userC.name, userC.password)
    check('AUTH-42m 失败一次后登录要求邮箱验证码', r.status === 200 && r.json.secondFactor === 'emailCode' && r.json.loginTicket && !r.json.accessToken, JSON.stringify({ sf: r.json?.secondFactor }))
    const ticketC = r.json.loginTicket
    // 只统计发给 userC 的邮件：登录失败可能触发登录风控通知邮件（发管理员，非本用户）
    check('AUTH-42n 失败后自动发送验证码邮件', mailToUserC() === mailCountAfterFail + 1, `${mailToUserC()} vs ${mailCountAfterFail}`)
    const loginCodeC = extractCode(decodeTextBody(stub.state.messages.at(-1).data))
    r = await api('POST', '/api/auth/login/verify', { body: { loginTicket: ticketC, code: loginCodeC } })
    check('AUTH-42o 验证码验证成功登录', r.status === 200 && r.json.accessToken, `${r.status} ${r.text.slice(0, 140)}`)

    r = await login(userC.name, userC.password)
    check('AUTH-42p 成功后失败计数清零，下次直接登录', r.status === 200 && r.json.secondFactor === 'none' && r.json.accessToken, JSON.stringify({ sf: r.json?.secondFactor }))

    // SMTP 关闭：验证码策略失效，直接登录
    await smtpDown()
    r = await login(userC.name, 'WrongPass999')
    void r
    const mailCountDown = stub.state.messages.length
    r = await login(userC.name, userC.password)
    check('AUTH-42q SMTP 未启用时验证码策略失效直接登录', r.status === 200 && r.json.secondFactor === 'none' && r.json.accessToken && stub.state.messages.length === mailCountDown, JSON.stringify({ sf: r.json?.secondFactor }))
  } finally {
    // ---------- 恢复快照设置 + 清理所建渠道 ----------
    for (const key of SNAPSHOT_KEYS) {
      if (snapshot[key] !== undefined && snapshot[key] !== '') {
        const rr = await api('PUT', '/api/settings', { token: rootToken, body: { key, value: snapshot[key] } })
        if (rr.status !== 200) console.log(`WARN | 恢复设置 ${key} 失败: ${rr.status}`)
      } else if (snapshot[key] === '') {
        // 快照为空串的键（如未绑定过的 SMTP 渠道）保持空值写回，避免残留测试值
        await api('PUT', '/api/settings', { token: rootToken, body: { key, value: '' } })
      }
    }
    if (smtpChannelId != null) {
      const rr = await api('DELETE', `/api/notification/channel/${smtpChannelId}`, { token: rootToken })
      if (rr.status !== 200) console.log(`WARN | 删除 SMTP 渠道失败: ${rr.status}`)
    }
    stub.close()
  }

  console.log(`\nRESULT: PASS=${PASS} FAIL=${FAIL}`)
  process.exit(FAIL > 0 ? 1 : 0)
}

main().catch((err) => {
  console.error('E2E error:', err)
  process.exit(1)
})
