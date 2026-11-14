// 图形验证码 E2E（场景 @AUTH-S48+）
// 覆盖：captcha/new 生成结构（captchaId / SVG base64 / 有效期）与图形验证码校验语义（不区分大小写 / 同码错 3 次作废 / 登录消费一次性 / 发码只验不消费）；
//       challenge_policy 三模式策略回显（off / always / after_fail）对登录提交、注册发码、找回发码三个面的覆盖；
//       图形验证码错误不计登录失败（不触发 after_fail 升级）；分级递进阈值（fail<N 仅图形码，fail≥N 加邮箱码）；
//       after_fail 发码面（注册/找回）首发免费、再发需过码；captcha/new IP 限流 429（压轴执行，避免限流窗口影响前置用例）。
// 用法：node local-dev/auth-captcha-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5000）
// 前置：后端运行中；种子账号 admin / abcd123456（root）；Redis 直连（E2E_REDIS=host:port 覆盖地址，
//       默认 127.0.0.1:6379，E2E_REDIS_PREFIX 覆盖键前缀默认 moai:，读取图形验证码答案与清理限流计数）；
//       SMTP 桩脚本自建并绑定系统设置（注册/找回发码用例需真实发码成功以推进 sendct 计数）。
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

/** 清理限流/风控计数残留（脚本可重复执行：发送计数与 IP 失败计数窗口残留会污染 after_fail 判定） */
async function cleanupRateCounters() {
  for (const pattern of [`${REDIS_PREFIX}login:rl:*`, `${REDIS_PREFIX}ls:ipfail:*`]) {
    const keys = await redisCommand('KEYS', pattern)
    if (Array.isArray(keys) && keys.length > 0) await redisCommand('DEL', ...keys)
  }
}

const policy = async (purpose, userName) => {
  const q = userName ? `&userName=${encodeURIComponent(userName)}` : ''
  const r = await api('GET', `/api/auth/challenge_policy?purpose=${purpose}${q}`)
  if (r.status !== 200) throw new Error(`challenge_policy 失败: ${r.status} ${r.text.slice(0, 140)}`)
  return r.json
}

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

const extractCode = (text) => text.match(/(\d{6})/)?.[1] ?? ''

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

const SNAPSHOT_KEYS = [
  'SMTP_ENABLED', 'SMTP_CHANNEL_ID',
  'LOGIN_EMAIL_CODE_MODE', 'LOGIN_CAPTCHA_MODE', 'LOGIN_EMAIL_CODE_FAIL_THRESHOLD',
  'REGISTER_EMAIL_CODE_ENABLED',
]
const SMTP_CHANNEL_NAME = `auth-captcha-e2e-${Date.now().toString().slice(-8)}`

async function main() {
  // redis 直连可用性前置检查（读图形验证码答案依赖）
  await redisCommand('PING')
  await cleanupRateCounters()

  // ---------- 准备：RSA 公钥 + root 登录 + 快照设置 ----------
  RSA_KEY = (await api('GET', '/api/common/serverinfo')).json.rsaPublic

  const loginRes = await login('admin', 'abcd123456')
  if (loginRes.status !== 200) throw new Error(`admin 登录失败: ${loginRes.status} ${loginRes.text.slice(0, 200)}`)
  const rootToken = loginRes.json.accessToken

  const settingsRes = await api('GET', '/api/settings', { token: rootToken })
  const valueOf = (key) => settingsRes.json?.items?.find((s) => s.key === key)?.value ?? ''
  const snapshot = {}
  for (const key of SNAPSHOT_KEYS) snapshot[key] = valueOf(key)

  const saveSetting = async (key, value) => {
    const r = await api('PUT', '/api/settings', { token: rootToken, body: { key, value } })
    if (r.status !== 200) throw new Error(`保存设置 ${key}=${value} 失败: ${r.status} ${r.text.slice(0, 140)}`)
  }

  const stub = await startSmtpStub()
  let smtpChannelId = null
  const smtpUp = async () => {
    const config = JSON.stringify({
      host: '127.0.0.1',
      port: stub.port,
      security: 'none',
      username: '',
      password: '',
      fromAddress: 'moai-test@test.local',
      fromName: 'MoAI E2E',
    })
    const r = await api('POST', '/api/notification/channel/create', { token: rootToken, body: { name: SMTP_CHANNEL_NAME, type: 'smtp', config, enabled: true, description: 'auth-captcha-e2e' } })
    if (r.status !== 200) throw new Error(`创建 SMTP 渠道失败: ${r.status} ${r.text.slice(0, 140)}`)
    smtpChannelId = r.json?.id
    await saveSetting('SMTP_CHANNEL_ID', String(smtpChannelId))
    await saveSetting('SMTP_ENABLED', 'true')
  }

  try {
    // ---------- 基线 ----------
    await saveSetting('LOGIN_EMAIL_CODE_MODE', 'off')
    await saveSetting('LOGIN_CAPTCHA_MODE', 'off')
    await saveSetting('LOGIN_EMAIL_CODE_FAIL_THRESHOLD', '3')
    await saveSetting('REGISTER_EMAIL_CODE_ENABLED', 'false')
    await smtpUp()

    // ---------- 生成结构 + off 模式策略（AUTH-S48） ----------
    const cap = await newCaptcha()
    const svg = Buffer.from(cap.image, 'base64').toString('utf-8')
    check('AUTH-48a captcha/new 返回结构齐全', !!cap.captchaId && cap.imageMimeType === 'image/svg+xml' && cap.expiresInSeconds === 300, JSON.stringify({ id: !!cap.captchaId, mime: cap.imageMimeType, exp: cap.expiresInSeconds }))
    check('AUTH-48b 图片为自绘 SVG', svg.includes('<svg') && svg.includes('</svg>') && svg.includes('rotate'), svg.slice(0, 120))

    const answer = await readCaptchaAnswer(cap.captchaId)
    check('AUTH-48c 图形验证码答案可从 Redis 读取', answer.length === 4, `answer=${answer}`)

    let p = await policy('login')
    check('AUTH-48d off 模式登录策略无需验证码', p.captchaRequired === false && p.emailCodeRequired === false, JSON.stringify(p))
    p = await policy('register')
    check('AUTH-48e off 模式注册发码策略无需验证码', p.captchaRequired === false, JSON.stringify(p))
    p = await policy('forgot')
    check('AUTH-48f off 模式找回发码策略无需验证码', p.captchaRequired === false, JSON.stringify(p))

    // ---------- 校验语义（AUTH-S49）：always 模式登录只需要图形验证码 ----------
    const userA = await registerUser('ca')
    await saveSetting('LOGIN_CAPTCHA_MODE', 'always')
    p = await policy('login', userA.name)
    check('AUTH-49a always 模式 policy 要求图形验证码', p.captchaRequired === true && p.emailCodeRequired === false, JSON.stringify(p))

    let r = await login(userA.name, userA.password)
    check('AUTH-49b 缺图形验证码登录 400', r.status === 400 && r.text.includes('图形验证码'), `${r.status} ${r.text.slice(0, 140)}`)

    // 错误答案累计：同一验证码错 3 次后作废（第 4 次即使答案正确也失效）
    const cap3 = await newCaptcha()
    for (let i = 0; i < 3; i++) {
      r = await login(userA.name, userA.password, { captchaId: cap3.captchaId, captchaCode: '0000' })
    }
    check('AUTH-49c 同码错 3 次均 400', r.status === 400 && r.text.includes('图形验证码'), `${r.status}`)
    const answer3 = await readCaptchaAnswer(cap3.captchaId)
    r = await login(userA.name, userA.password, { captchaId: cap3.captchaId, captchaCode: answer3 })
    check('AUTH-49d 错满 3 次后真答案也失效 400', r.status === 400 && r.text.includes('图形验证码'), `${r.status}`)

    // 正确答案（不区分大小写）登录成功并消费：复用即失效
    const cap4 = await newCaptcha()
    const answer4 = await readCaptchaAnswer(cap4.captchaId)
    r = await login(userA.name, userA.password, { captchaId: cap4.captchaId, captchaCode: answer4.toLowerCase() })
    check('AUTH-49e 答案不区分大小写登录成功', r.status === 200 && !!r.json.accessToken, `${r.status} ${r.text.slice(0, 140)}`)
    r = await login(userA.name, userA.password, { captchaId: cap4.captchaId, captchaCode: answer4 })
    check('AUTH-49f 登录消费后复用 400（一码一登录）', r.status === 400 && r.text.includes('图形验证码'), `${r.status}`)

    // ---------- 图形码错误不计登录失败（AUTH-S50） ----------
    // 此时尚处 always 模式（图形码强制校验），两次图形码错误（密码正确）不得计入登录失败
    const userB = await registerUser('cb')
    const cap5 = await newCaptcha()
    r = await login(userB.name, userB.password, { captchaId: cap5.captchaId, captchaCode: '0000' })
    check('AUTH-50a 图形码错误登录 400', r.status === 400 && r.text.includes('图形验证码'), `${r.status}`)
    r = await login(userB.name, userB.password, { captchaId: cap5.captchaId, captchaCode: '0000' })
    check('AUTH-50b 图形码错误累计 400', r.status === 400, `${r.status}`)

    // 切到 after_fail：若图形码错误被计为登录失败，这里 policy 应已升级；预期未升级（错误不计失败）
    await saveSetting('LOGIN_CAPTCHA_MODE', 'after_fail')
    await saveSetting('LOGIN_EMAIL_CODE_MODE', 'off')
    p = await policy('login', userB.name)
    check('AUTH-50c 图形码错误不计登录失败（policy 未升级）', p.captchaRequired === false && p.emailCodeRequired === false, JSON.stringify(p))

    // ---------- 分级递进阈值（AUTH-S51）：阈值 3，fail<3 仅图形码，fail≥3 加邮箱码 ----------
    // 首次失败（failCount=0 时图形码未要求）不带图形码；failCount≥1 后每次登录都必须带正确图形码（错密码也不例外）
    await saveSetting('LOGIN_EMAIL_CODE_MODE', 'after_fail')
    await saveSetting('LOGIN_EMAIL_CODE_FAIL_THRESHOLD', '3')

    // 首次错密码：不带图形码直达密码校验 → 401（failCount 0→1）
    r = await login(userB.name, 'WrongPass999')
    check('AUTH-51a 首次密码错误 401', r.status === 401, `${r.status}`)

    // failCount≥1 后：错密码也必须过图形码（带正确图形码才进入密码校验 → 401，failCount 1→2）
    const capA = await newCaptcha()
    r = await login(userB.name, 'WrongPass999', { captchaId: capA.captchaId, captchaCode: await readCaptchaAnswer(capA.captchaId) })
    check('AUTH-51b 图形码+错密码 401（计数 2）', r.status === 401, `${r.status}`)

    p = await policy('login', userB.name)
    check('AUTH-51c fail<3 仅要求图形验证码', p.captchaRequired === true && p.emailCodeRequired === false, JSON.stringify(p))

    // 阈值未到：图形码 + 正确密码即可登录（无需邮箱码）
    const cap6 = await newCaptcha()
    const answer6 = await readCaptchaAnswer(cap6.captchaId)
    r = await login(userB.name, userB.password, { captchaId: cap6.captchaId, captchaCode: answer6 })
    check('AUTH-51d fail<3 时图形码+密码登录成功', r.status === 200 && !!r.json.accessToken, `${r.status} ${r.text.slice(0, 140)}`)

    r = await login(userB.name, 'WrongPass999')
    check('AUTH-51e 密码错误 401（计数 1）', r.status === 401, `${r.status}`)
    const capB = await newCaptcha()
    r = await login(userB.name, 'WrongPass999', { captchaId: capB.captchaId, captchaCode: await readCaptchaAnswer(capB.captchaId) })
    check('AUTH-51f 图形码+错密码 401（计数 2）', r.status === 401, `${r.status}`)
    const capC = await newCaptcha()
    r = await login(userB.name, 'WrongPass999', { captchaId: capC.captchaId, captchaCode: await readCaptchaAnswer(capC.captchaId) })
    check('AUTH-51g 图形码+错密码 401（计数 3）', r.status === 401, `${r.status}`)

    p = await policy('login', userB.name)
    check('AUTH-51h fail≥3 升级要求图形+邮箱验证码', p.captchaRequired === true && p.emailCodeRequired === true, JSON.stringify(p))

    // 发码（策略命中需过码）→ 图形码 + 邮箱码一体登录
    const cap7 = await newCaptcha()
    const answer7 = await readCaptchaAnswer(cap7.captchaId)
    const mailCountB = stub.state.messages.filter((m) => m.rcpts.join().includes(userB.email)).length
    r = await api('POST', '/api/auth/login_code/send', { body: { userName: userB.name } })
    check('AUTH-51i 策略命中时发码缺图形验证码 400', r.status === 400 && r.text.includes('图形验证码'), `${r.status}`)
    r = await api('POST', '/api/auth/login_code/send', { body: { userName: userB.name, captchaId: cap7.captchaId, captchaCode: answer7 } })
    check('AUTH-51j 过码后发码成功', r.status === 200 && stub.state.messages.filter((m) => m.rcpts.join().includes(userB.email)).length === mailCountB + 1, `${r.status}`)
    const loginCodeB = extractCode(decodeTextBody(stub.state.messages.at(-1).data))
    check('AUTH-51k 登录验证码邮件含 6 位码', loginCodeB.length === 6, decodeTextBody(stub.state.messages.at(-1).data).slice(0, 160))

    r = await login(userB.name, userB.password, { captchaId: cap7.captchaId, captchaCode: answer7, emailCode: loginCodeB })
    check('AUTH-51l 图形码+邮箱码一体登录成功', r.status === 200 && !!r.json.accessToken, `${r.status} ${r.text.slice(0, 140)}`)

    r = await login(userB.name, userB.password)
    check('AUTH-51m 成功后失败计数清零恢复直达', r.status === 200 && !!r.json.accessToken, `${r.status}`)

    // ---------- 发码面 after_fail：首发免费、再发需过码（AUTH-S52） ----------
    await saveSetting('REGISTER_EMAIL_CODE_ENABLED', 'true')
    p = await policy('register')
    check('AUTH-52a after_fail 注册首发免费（policy 未要求）', p.captchaRequired === false, JSON.stringify(p))

    const regEmail1 = `reg${Date.now().toString().slice(-8)}@test.local`
    r = await api('POST', '/api/auth/email_code/send', { body: { purpose: 'register', email: regEmail1 } })
    check('AUTH-52b 注册首码无图形码发送成功', r.status === 200, `${r.status} ${r.text.slice(0, 140)}`)

    p = await policy('register')
    check('AUTH-52c 发码一次后注册面 policy 升级', p.captchaRequired === true, JSON.stringify(p))

    r = await api('POST', '/api/auth/email_code/send', { body: { purpose: 'register', email: `reg2${Date.now().toString().slice(-8)}@test.local` } })
    check('AUTH-52d 再发缺图形验证码 400', r.status === 400 && r.text.includes('图形验证码'), `${r.status} ${r.text.slice(0, 140)}`)

    // 发码只验不消费：同码可再次发码（换邮箱绕开 60s 冷却）
    const cap8 = await newCaptcha()
    const answer8 = await readCaptchaAnswer(cap8.captchaId)
    r = await api('POST', '/api/auth/email_code/send', { body: { purpose: 'register', email: `reg3${Date.now().toString().slice(-8)}@test.local`, captchaId: cap8.captchaId, captchaCode: answer8 } })
    check('AUTH-52e 过码后注册发码成功', r.status === 200, `${r.status} ${r.text.slice(0, 140)}`)
    r = await api('POST', '/api/auth/email_code/send', { body: { purpose: 'register', email: `reg4${Date.now().toString().slice(-8)}@test.local`, captchaId: cap8.captchaId, captchaCode: answer8.toLowerCase() } })
    check('AUTH-52f 同码可重复用于发码（不消费）', r.status === 200, `${r.status} ${r.text.slice(0, 140)}`)

    // 找回密码面独立计数：发一次后同样升级（重置验证码只发给已注册邮箱）
    p = await policy('forgot')
    check('AUTH-52g 找回面首发免费', p.captchaRequired === false, JSON.stringify(p))
    r = await api('POST', '/api/auth/email_code/send', { body: { purpose: 'resetPassword', email: userB.email } })
    check('AUTH-52h 找回首码发送成功', r.status === 200, `${r.status} ${r.text.slice(0, 140)}`)
    p = await policy('forgot')
    check('AUTH-52i 找回发码一次后 policy 升级', p.captchaRequired === true, JSON.stringify(p))
    r = await api('POST', '/api/auth/email_code/send', { body: { purpose: 'resetPassword', email: `fp2${Date.now().toString().slice(-8)}@test.local` } })
    check('AUTH-52j 找回再发缺图形验证码 400', r.status === 400 && r.text.includes('图形验证码'), `${r.status} ${r.text.slice(0, 140)}`)

    // always 模式：发码面立即要求图形码
    await saveSetting('LOGIN_CAPTCHA_MODE', 'always')
    p = await policy('forgot')
    check('AUTH-52k always 模式找回面立即要求图形码', p.captchaRequired === true, JSON.stringify(p))

    // ---------- captcha/new IP 限流（AUTH-S53，压轴避免限流窗口影响前置用例） ----------
    // 60 秒固定窗口与前置用例的取图共用计数，此处只断言「超限后 429」
    await saveSetting('LOGIN_CAPTCHA_MODE', 'off')
    let saw429 = false
    let okCount = 0
    for (let i = 0; i < 25; i++) {
      const rr = await api('GET', '/api/auth/captcha/new')
      if (rr.status === 200) okCount++
      else if (rr.status === 429) saw429 = true
    }
    check('AUTH-53a captcha/new 超限 429（20 次/分钟/IP）', saw429 && okCount >= 5, `ok=${okCount}`)
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
  console.log(`RESULT: PASS=${PASS} FAIL=${FAIL + 1}`)
  process.exit(1)
})
