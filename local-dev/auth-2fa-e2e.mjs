// 两步验证（2FA/TOTP）与登录邮箱验证码一体化 E2E（场景 @AUTH-S36+）
// 覆盖：设置校验（LOGIN_FORCED_2FA 布尔 / LOGIN_EMAIL_CODE_MODE 枚举 / LOGIN_CAPTCHA_MODE 枚举 / LOGIN_EMAIL_CODE_FAIL_THRESHOLD 阈值）；TOTP 绑定全流程（setup→enable→status→disable）与错码拒绝；
//       登录 TOTP 挑战（secondFactor=totp + 一次性票据 / 错码 400 / 票据消费后复用 401 / 动态码防重放）；系统纯 node 实现与后端 TotpHelper 端对拍；
//       强制 2FA：未绑定登录拿 mfa_pending 受限 token → 非白名单接口 403 → 白名单（userinfo/two_factor）可用 → 绑定后旧 token 立即恢复；
//       强制期间关闭 400；root 重置用户 2FA（目标保护：root 自身 400 / 非管理员 403）；登录邮箱验证码一体化（原 AUTH-S42 两段式票据流程已退役）：
//       challenge_policy 策略回显 + login_code/send 按用户名预发码（冷却 429 / 防枚举静默）+ 账号密码验证码一体提交直颁令牌 + TOTP 优先 +
//       after_fail 分级递进（图形验证码先行，阈值后加邮箱验证码，图形码答案经 docker redis 读取）+ SMTP 未启用策略失效。
// 用法：node local-dev/auth-2fa-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5000）
// 前置：后端运行中；种子账号 admin / abcd123456（root）；AUTH-S45 分级用例需 Redis 直连读图形验证码答案
//       （E2E_REDIS=host:port 覆盖地址，默认 127.0.0.1:6379；E2E_REDIS_PREFIX 覆盖键前缀，默认 moai:）。
//       开头快照相关设置原值，结束时恢复（不破坏共享实例配置）。
import net from 'node:net'
import crypto from 'node:crypto'

const BASE = process.env.APP_BASE ?? process.argv[2] ?? 'http://127.0.0.1:5000'
// 图形验证码答案读取与限流计数清理走 Redis 直连（E2E_REDIS=host:port，默认 127.0.0.1:6379；键前缀 E2E_REDIS_PREFIX，默认 moai:）
const [REDIS_HOST, REDIS_PORT] = (process.env.E2E_REDIS ?? '127.0.0.1:6379').split(':')
const REDIS_PREFIX = process.env.E2E_REDIS_PREFIX ?? 'moai:'
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

async function login(userName, password, extra) {
  return api('POST', '/api/auth/login', {
    body: {
      userName,
      password: rsa(password),
      emailCode: extra?.emailCode,
      captchaId: extra?.captchaId,
      captchaCode: extra?.captchaCode,
    },
  })
}

/** 获取新的图形验证码 */
async function newCaptcha() {
  const r = await api('GET', '/api/auth/captcha/new')
  if (r.status !== 200) throw new Error(`captcha/new 失败: ${r.status} ${r.text.slice(0, 140)}`)
  return r.json
}

// ---------- 极简 RESP 客户端（仅 GET/KEYS/DEL） ----------
function parseResp(buffer) {
  const type = buffer[0]
  const lineEnd = buffer.indexOf('\r\n')
  if (lineEnd < 0) return null
  const head = buffer.slice(1, lineEnd).toString('utf-8')
  if (type === 0x24) {
    const len = Number.parseInt(head, 10)
    if (len === -1) return [null, buffer.slice(lineEnd + 2)]
    const total = lineEnd + 2 + len + 2
    if (buffer.length < total) return null
    return [buffer.slice(lineEnd + 2, lineEnd + 2 + len).toString('utf-8'), buffer.slice(total)]
  }
  if (type === 0x2a) {
    const n = Number.parseInt(head, 10)
    let rest = buffer.slice(lineEnd + 2)
    const out = []
    for (let i = 0; i < n; i++) {
      const parsed = parseResp(rest)
      if (!parsed) return null
      out.push(parsed[0])
      rest = parsed[1]
    }
    return [out, rest]
  }
  return [type === 0x3a ? Number.parseInt(head, 10) : head, buffer.slice(lineEnd + 2)]
}

function redisCommand(...args) {
  return new Promise((resolve, reject) => {
    const socket = net.createConnection({ host: REDIS_HOST, port: Number(REDIS_PORT) }, () => {
      let payload = `*${args.length}\r\n`
      for (const a of args) payload += `$${Buffer.byteLength(a)}\r\n${a}\r\n`
      socket.write(payload)
    })
    let buffer = Buffer.alloc(0)
    socket.on('data', (d) => {
      buffer = Buffer.concat([buffer, d])
      const parsed = parseResp(buffer)
      if (parsed) {
        socket.end()
        resolve(parsed[0])
      }
    })
    socket.on('error', reject)
  })
}

/** 经 Redis 读取图形验证码答案（{prefix}login:captcha:{id}，序列化 JSON） */
async function readCaptchaAnswer(captchaId) {
  const raw = await redisCommand('GET', `${REDIS_PREFIX}login:captcha:${captchaId}`)
  if (!raw) return ''
  return raw.match(/"?[Cc]ode"?\s*:\s*"([^"]+)"/)?.[1] ?? ''
}

/** 清理限流/风控计数残留（可重复执行：IP 失败计数残留会让 after_fail 分级用例首登就要求图形码） */
async function cleanupRateCounters() {
  for (const pattern of [`${REDIS_PREFIX}login:rl:*`, `${REDIS_PREFIX}ls:ipfail:*`]) {
    const keys = await redisCommand('KEYS', pattern)
    if (Array.isArray(keys) && keys.length > 0) await redisCommand('DEL', ...keys)
  }
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
  'LOGIN_FORCED_2FA', 'LOGIN_EMAIL_CODE_MODE', 'LOGIN_CAPTCHA_MODE', 'LOGIN_EMAIL_CODE_FAIL_THRESHOLD',
  'REGISTER_EMAIL_CODE_ENABLED',
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
    await saveSetting('LOGIN_CAPTCHA_MODE', 'off')
    await saveSetting('LOGIN_EMAIL_CODE_FAIL_THRESHOLD', '3')
    await saveSetting('REGISTER_EMAIL_CODE_ENABLED', 'false')
    try {
      await cleanupRateCounters()
    } catch {
      console.log('WARN | redis 直连不可用（E2E_REDIS 可覆盖地址），AUTH-S45 分级用例（读图形验证码答案）将失败')
    }

    // ---------- 设置校验（SET-S59~S60 / SET-92） ----------
    let r = await api('PUT', '/api/settings', { token: rootToken, body: { key: 'LOGIN_FORCED_2FA', value: 'yes' } })
    check('SET-59a 强制2FA开关非法值 400', r.status === 400 && r.text.includes('true 或 false'), `${r.status} ${r.text.slice(0, 140)}`)
    r = await api('PUT', '/api/settings', { token: rootToken, body: { key: 'LOGIN_EMAIL_CODE_MODE', value: 'sometimes' } })
    check('SET-60a 登录验证码策略非法值 400', r.status === 400 && r.text.includes('off、always 或 after_fail'), `${r.status} ${r.text.slice(0, 140)}`)
    r = await api('PUT', '/api/settings', { token: rootToken, body: { key: 'LOGIN_CAPTCHA_MODE', value: 'sometimes' } })
    check('SET-92a 图形验证码策略非法值 400', r.status === 400 && r.text.includes('off、always 或 after_fail'), `${r.status} ${r.text.slice(0, 140)}`)
    r = await api('PUT', '/api/settings', { token: rootToken, body: { key: 'LOGIN_EMAIL_CODE_FAIL_THRESHOLD', value: '0' } })
    check('SET-92b 邮箱码阈值越界 400（0）', r.status === 400 && r.text.includes('1~10'), `${r.status} ${r.text.slice(0, 140)}`)
    r = await api('PUT', '/api/settings', { token: rootToken, body: { key: 'LOGIN_EMAIL_CODE_FAIL_THRESHOLD', value: '11' } })
    check('SET-92c 邮箱码阈值越界 400（11）', r.status === 400 && r.text.includes('1~10'), `${r.status} ${r.text.slice(0, 140)}`)
    r = await api('PUT', '/api/settings', { token: rootToken, body: { key: 'LOGIN_EMAIL_CODE_FAIL_THRESHOLD', value: 'abc' } })
    check('SET-92d 邮箱码阈值非法 400', r.status === 400, `${r.status} ${r.text.slice(0, 140)}`)

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

    // ---------- 登录邮箱验证码一体化（AUTH-S43~S46，原 AUTH-S42 两段式票据流程已退役） ----------
    await saveSetting('LOGIN_FORCED_2FA', 'false')
    await saveSetting('LOGIN_CAPTCHA_MODE', 'off')
    await saveSetting('LOGIN_EMAIL_CODE_FAIL_THRESHOLD', '3')
    await smtpUp()

    // 公共匿名发码端点拒绝 login 用途（防轰炸，语义沿用原 AUTH-42a）
    r = await api('POST', '/api/auth/email_code/send', { body: { purpose: 'login', email: userB.email } })
    check('AUTH-43a 公共发码端点拒绝 login 用途 400', r.status === 400, `${r.status} ${r.text.slice(0, 140)}`)

    // off 模式 policy 回显：无需任何验证码
    r = await api('GET', `/api/auth/challenge_policy?purpose=login&userName=${encodeURIComponent(userB.name)}`)
    check('AUTH-43b off 模式 policy 无需验证码', r.status === 200 && r.json.captchaRequired === false && r.json.emailCodeRequired === false, r.text.slice(0, 140))

    // always 模式：policy 要求邮箱验证码（图形验证码仍关闭）
    await saveSetting('LOGIN_EMAIL_CODE_MODE', 'always')
    r = await api('GET', `/api/auth/challenge_policy?purpose=login&userName=${encodeURIComponent(userB.name)}`)
    check('AUTH-43c always 模式 policy 要求邮箱验证码', r.status === 200 && r.json.captchaRequired === false && r.json.emailCodeRequired === true, r.text.slice(0, 140))

    const mailCountBefore = stub.state.messages.length
    r = await api('POST', '/api/auth/login_code/send', { body: { userName: userB.name } })
    check('AUTH-43d 发码端点按用户名发码', r.status === 200 && stub.state.messages.length === mailCountBefore + 1 && stub.state.messages.at(-1).rcpts.join().includes(userB.email), `${r.status} ${JSON.stringify(stub.state.messages.at(-1)?.rcpts)}`)
    const loginCodeB = extractCode(decodeTextBody(stub.state.messages.at(-1).data))
    check('AUTH-43e 登录验证码邮件含 6 位码', loginCodeB.length === 6, decodeTextBody(stub.state.messages.at(-1).data).slice(0, 160))

    r = await api('POST', '/api/auth/login_code/send', { body: { userName: userB.name } })
    check('AUTH-43f 冷却期内重发 429', r.status === 429, `${r.status} ${r.text.slice(0, 140)}`)

    r = await login(userB.name, userB.password)
    check('AUTH-43g 缺邮箱验证码登录 400', r.status === 400 && r.text.includes('请输入邮箱验证码'), `${r.status} ${r.text.slice(0, 140)}`)

    r = await login(userB.name, userB.password, { emailCode: '000000' })
    check('AUTH-43h 错误邮箱验证码 400', r.status === 400 && r.text.includes('验证码错误'), `${r.status} ${r.text.slice(0, 140)}`)

    // 账号 + 密码 + 邮箱验证码一体提交，直接颁发令牌
    r = await login(userB.name, userB.password, { emailCode: loginCodeB })
    check('AUTH-43i 三字段一体登录直颁令牌', r.status === 200 && r.json.accessToken && r.json.secondFactor === 'none' && r.json.mfaPending === false, JSON.stringify({ sf: r.json?.secondFactor, at: !!r.json?.accessToken }))
    const tokenB3 = r.json.accessToken

    // 防枚举：不存在的用户静默成功且不发邮件
    const mailCountSilent = stub.state.messages.length
    r = await api('POST', '/api/auth/login_code/send', { body: { userName: `ghost${Date.now().toString().slice(-8)}` } })
    check('AUTH-43j 不存在用户静默成功且不发邮件', r.status === 200 && stub.state.messages.length === mailCountSilent, `${r.status} mails=${stub.state.messages.length}`)

    // 已绑定 2FA：TOTP 优先，不要求邮箱验证码（语义沿用原 AUTH-42h/42i/42j）
    const secretA2 = await bindTotp(tokenB3)
    const mailCountPrecedence = stub.state.messages.length
    r = await api('GET', `/api/auth/challenge_policy?purpose=login&userName=${encodeURIComponent(userB.name)}`)
    check('AUTH-44a 绑定 TOTP 后 policy 不要求邮箱验证码', r.status === 200 && r.json.emailCodeRequired === false, r.text.slice(0, 140))
    r = await login(userB.name, userB.password)
    check('AUTH-44b TOTP 优先返回 totp 挑战且不发邮件', r.status === 200 && r.json.secondFactor === 'totp' && r.json.loginTicket && stub.state.messages.length === mailCountPrecedence, JSON.stringify({ sf: r.json?.secondFactor }))
    const ticketPrecedence = r.json.loginTicket
    r = await api('POST', '/api/auth/login/verify', { body: { loginTicket: ticketPrecedence, code: totp(secretA2, currentStep()) } })
    check('AUTH-44c TOTP 挑战验证通过颁发令牌', r.status === 200 && r.json.accessToken, `${r.status}`)
    // 恢复非强制后可关闭 2FA
    r = await api('POST', '/api/auth/two_factor/disable', { token: tokenB3, body: { code: totp(secretA2, currentStep()) } })
    check('AUTH-44d 恢复非强制后可关闭 2FA', r.status === 200, `${r.status} ${r.text.slice(0, 140)}`)

    // after_fail 分级递进（AUTH-S45）：阈值临时设 1，失败一次后图形验证码与邮箱验证码同时出现
    await saveSetting('LOGIN_EMAIL_CODE_MODE', 'after_fail')
    await saveSetting('LOGIN_CAPTCHA_MODE', 'after_fail')
    await saveSetting('LOGIN_EMAIL_CODE_FAIL_THRESHOLD', '1')
    const userC = await registerUser('fc')
    r = await login(userC.name, userC.password)
    check('AUTH-45a after_fail 无失败记录时直接登录', r.status === 200 && r.json.secondFactor === 'none' && r.json.accessToken, JSON.stringify({ sf: r.json?.secondFactor }))

    r = await login(userC.name, 'WrongPass999')
    check('AUTH-45b 密码错误 401', r.status === 401, `${r.status}`)

    // 失败一次后 policy 升级：图形验证码 + 邮箱验证码同时要求
    r = await api('GET', `/api/auth/challenge_policy?purpose=login&userName=${encodeURIComponent(userC.name)}`)
    check('AUTH-45c 失败一次后 policy 要求图形+邮箱验证码', r.status === 200 && r.json.captchaRequired === true && r.json.emailCodeRequired === true, r.text.slice(0, 140))

    // 缺图形验证码登录 400（图形码在密码校验前消费，错误不计登录失败）
    r = await login(userC.name, userC.password, {})
    check('AUTH-45d 缺图形验证码登录 400', r.status === 400 && r.text.includes('图形验证码'), `${r.status} ${r.text.slice(0, 140)}`)

    // 错误图形验证码 400（'0000' 不在字符集中必错；错误累计但不作废，仍有剩余机会）
    const cap1 = await newCaptcha()
    r = await login(userC.name, userC.password, { captchaId: cap1.captchaId, captchaCode: '0000' })
    check('AUTH-45e 错误图形验证码 400', r.status === 400 && r.text.includes('图形验证码'), `${r.status} ${r.text.slice(0, 140)}`)

    // 正确图形验证码（docker redis 读答案）：消费后再用同码登录失效
    const cap2 = await newCaptcha()
    const capAnswer2 = await readCaptchaAnswer(cap2.captchaId)
    check('AUTH-45f 图形验证码答案可从 Redis 读取', capAnswer2.length === 4, `answer=${capAnswer2}`)
    r = await login(userC.name, userC.password, { captchaId: cap2.captchaId, captchaCode: capAnswer2 })
    check('AUTH-45g 图形码通过后缺邮箱验证码 400', r.status === 400 && r.text.includes('请输入邮箱验证码'), `${r.status} ${r.text.slice(0, 140)}`)

    r = await login(userC.name, userC.password, { captchaId: cap2.captchaId, captchaCode: capAnswer2 })
    check('AUTH-45h 图形验证码一次性：消费后复用 400', r.status === 400 && r.text.includes('图形验证码'), `${r.status}`)

    // 发码同样需要过图形验证码（策略命中）
    const cap3 = await newCaptcha()
    const capAnswer3 = await readCaptchaAnswer(cap3.captchaId)
    const mailCountC1 = stub.state.messages.filter((m) => m.rcpts.join().includes(userC.email)).length
    r = await api('POST', '/api/auth/login_code/send', { body: { userName: userC.name } })
    check('AUTH-45i 策略命中时发码缺图形验证码 400', r.status === 400 && r.text.includes('图形验证码'), `${r.status} ${r.text.slice(0, 140)}`)
    r = await api('POST', '/api/auth/login_code/send', { body: { userName: userC.name, captchaId: cap3.captchaId, captchaCode: capAnswer3 } })
    check('AUTH-45j 图形码通过后发码成功', r.status === 200 && stub.state.messages.filter((m) => m.rcpts.join().includes(userC.email)).length === mailCountC1 + 1, `${r.status}`)
    const loginCodeC = extractCode(decodeTextBody(stub.state.messages.at(-1).data))

    // 发码不消费图形验证码：同码可继续用于登录提交
    r = await login(userC.name, userC.password, { captchaId: cap3.captchaId, captchaCode: capAnswer3, emailCode: loginCodeC })
    check('AUTH-45k 图形码+邮箱码一体登录成功', r.status === 200 && r.json.accessToken && r.json.secondFactor === 'none', JSON.stringify({ sf: r.json?.secondFactor }))

    r = await login(userC.name, userC.password)
    check('AUTH-45l 成功后失败计数清零，下次直接登录', r.status === 200 && r.json.secondFactor === 'none' && r.json.accessToken, JSON.stringify({ sf: r.json?.secondFactor }))

    // SMTP 关闭：验证码策略失效，直接登录（语义沿用原 AUTH-42q；本段只验邮箱码链路，图形码关闭排除干扰）
    await smtpDown()
    await saveSetting('LOGIN_CAPTCHA_MODE', 'off')
    r = await login(userC.name, 'WrongPass999')
    void r
    const mailCountDown = stub.state.messages.length
    r = await login(userC.name, userC.password)
    check('AUTH-46a SMTP 未启用时验证码策略失效直接登录', r.status === 200 && r.json.secondFactor === 'none' && r.json.accessToken && stub.state.messages.length === mailCountDown, JSON.stringify({ sf: r.json?.secondFactor }))
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
