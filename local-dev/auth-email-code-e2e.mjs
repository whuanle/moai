// 注册/找回密码邮箱验证码 E2E（场景 @AUTH-S26+）
// 覆盖：serverinfo 暴露 registerEmailCodeRequired/passwordResetEnabled 开关；未启用 SMTP 时发送验证码被拒；
//       找回密码发码对未注册邮箱 404；注册验证码开关开启后注册必须携带验证码（缺码 400、错码 400、冷却 429）；
//       本地 SMTP 桩捕获验证码邮件（未绑定模板时回退内置模板，HTML 正文插值 {{code}}/{{siteName}}/{{expireMinutes}}）；
//       验证码一次性消费与 5 次错误作废；
//       绑定通知中心自定义模板插值回显 + 解绑回退内置模板；找回密码：发码→重置→新密码登录→旧密码失效；关闭 SMTP 后全链路拒绝。
// 用法：node local-dev/auth-email-code-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5000）
// 前置：后端运行中；种子账号 admin / abcd123456（root）。开头快照 SMTP 相关设置原值，结束时恢复（不破坏共享实例配置）。
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

function decodeSubject(data) {
  // 折行头部：Subject: 行 + 空白开头的续行；encoded-word 与字面片段交替出现
  let subject = ''
  const lines = data.split(/\r?\n/)
  for (let i = 0; i < lines.length; i++) {
    if (/^Subject: /i.test(lines[i])) {
      subject = lines[i].slice(8)
      for (let j = i + 1; j < lines.length && /^[ \t]/.test(lines[j]); j++) {
        subject += lines[j].replace(/^[ \t]+/, '')
      }
      break
    }
  }
  let out = ''
  const re = /=\?utf-8\?B\?([A-Za-z0-9+/=]+)\?=/gi
  let last = 0
  let m
  while ((m = re.exec(subject))) {
    out += subject.slice(last, m.index)
    out += Buffer.from(m[1], 'base64').toString('utf-8')
    last = m.index + m[0].length
  }
  out += subject.slice(last)
  return out.trim()
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

const isHtmlMail = (data) => /Content-Type: text\/html/i.test(data)

const extractCode = (text) => text.match(/(\d{6})/)?.[1] ?? ''

const SMTP_KEYS = ['SMTP_ENABLED', 'SMTP_CHANNEL_ID']
const CODE_KEYS = ['REGISTER_EMAIL_CODE_ENABLED', 'SMTP_REGISTER_CODE_TEMPLATE_ID', 'SMTP_RESET_CODE_TEMPLATE_ID', 'SMTP_LOGIN_CODE_TEMPLATE_ID']
// 渠道名唯一（部分唯一索引），按运行时间戳命名避免异常残留导致重跑 409
const SMTP_CHANNEL_NAME = `auth-email-code-e2e-${Date.now().toString().slice(-8)}`

async function main() {
  // ---------- 准备：RSA 公钥 + root 登录 + 快照设置 ----------
  RSA_KEY = (await api('GET', '/api/common/serverinfo')).json.rsaPublic

  const loginRes = await login('admin', 'abcd123456')
  if (loginRes.status !== 200) throw new Error(`admin 登录失败: ${loginRes.status} ${loginRes.text.slice(0, 200)}`)
  const rootToken = loginRes.json.accessToken

  const settingsRes = await api('GET', '/api/settings', { token: rootToken })
  const valueOf = (key) => settingsRes.json?.items?.find((s) => s.key === key)?.value ?? ''
  const snapshot = {}
  for (const key of [...SMTP_KEYS, ...CODE_KEYS]) snapshot[key] = valueOf(key)

  const saveSetting = async (key, value) => {
    const r = await api('PUT', '/api/settings', { token: rootToken, body: { key, value } })
    if (r.status !== 200) throw new Error(`保存设置 ${key}=${value} 失败: ${r.status} ${r.text.slice(0, 140)}`)
  }
  const serverinfo = async () => (await api('GET', '/api/common/serverinfo')).json

  const stub = await startSmtpStub()
  // SMTP 连接配置统一在通知中心渠道维护：upsert 指向本地桩的 SMTP 渠道并绑定到系统设置
  let smtpChannelId = null
  let customTemplateId = ''
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
      const r = await api('POST', '/api/notification/channel/create', { token: rootToken, body: { name: SMTP_CHANNEL_NAME, type: 'smtp', config, enabled: true, description: 'auth-email-code-e2e' } })
      if (r.status !== 200) throw new Error(`创建 SMTP 渠道失败: ${r.status} ${r.text.slice(0, 140)}`)
      smtpChannelId = r.json?.id
      if (smtpChannelId == null) throw new Error('创建 SMTP 渠道未返回 id')
    } else {
      const r = await api('PUT', `/api/notification/channel/${smtpChannelId}`, { token: rootToken, body: { id: smtpChannelId, name: SMTP_CHANNEL_NAME, type: 'smtp', config, enabled: true, description: 'auth-email-code-e2e' } })
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

  try {
    // ---------- 基线：未启用 SMTP ----------
    await smtpDown()
    await saveSetting('REGISTER_EMAIL_CODE_ENABLED', 'false')

    let info = await serverinfo()
    check('AUTH-26a serverinfo 基线：注册验证码关闭', info.registerEmailCodeRequired === false, JSON.stringify(info))
    check('AUTH-26b serverinfo 基线：找回密码关闭', info.passwordResetEnabled === false, JSON.stringify(info))

    let r = await api('POST', '/api/auth/email_code/send', { body: { purpose: 'register', email: 'any@test.local' } })
    check('AUTH-26c 未启用 SMTP 时注册验证码发送 400', r.status === 400 && r.text.includes('未开启'), `${r.status} ${r.text.slice(0, 140)}`)
    r = await api('POST', '/api/auth/email_code/send', { body: { purpose: 'resetPassword', email: 'any@test.local' } })
    check('AUTH-26d 未启用 SMTP 时找回密码验证码发送 400', r.status === 400 && r.text.includes('未启用'), `${r.status} ${r.text.slice(0, 140)}`)

    // ---------- 启用 SMTP：找回密码可用，注册验证码仍关闭 ----------
    await smtpUp()
    info = await serverinfo()
    check('AUTH-26e 启用 SMTP 后找回密码可用', info.passwordResetEnabled === true, JSON.stringify(info))
    check('AUTH-26f 注册验证码开关独立于 SMTP', info.registerEmailCodeRequired === false, JSON.stringify(info))

    r = await api('POST', '/api/auth/email_code/send', { body: { purpose: 'resetPassword', email: 'nobody@test.local' } })
    check('AUTH-26g 找回密码发码对未注册邮箱 404', r.status === 404 && r.text.includes('未注册'), `${r.status} ${r.text.slice(0, 140)}`)

    // 关闭状态下注册无需验证码（存量链路回归）
    const plainName = uname('nc')
    r = await api('POST', '/api/auth/register', { body: { userName: plainName, email: `${plainName}@test.local`, nickName: plainName, phone: `15${Date.now().toString().slice(-9)}`.slice(0, 11), password: rsa('Test1234') } })
    check('AUTH-26h 未开启注册验证码时直接注册成功', r.status === 200, `${r.status} ${r.text.slice(0, 140)}`)
    const resetUserName = plainName

    // ---------- 设置保存校验（SET-S56 开关 + SET-S69 绑定模板 id） ----------
    r = await api('PUT', '/api/settings', { token: rootToken, body: { key: 'REGISTER_EMAIL_CODE_ENABLED', value: 'yes' } })
    check('SET-56a 注册验证码开关非法值 400', r.status === 400 && r.text.includes('true 或 false'), `${r.status} ${r.text.slice(0, 140)}`)
    r = await api('PUT', '/api/settings', { token: rootToken, body: { key: 'SMTP_REGISTER_CODE_TEMPLATE_ID', value: 'abc' } })
    check('SET-69a 注册验证码绑定模板 id 非法 400', r.status === 400 && r.text.includes('模板 id 无效'), `${r.status} ${r.text.slice(0, 140)}`)
    r = await api('PUT', '/api/settings', { token: rootToken, body: { key: 'SMTP_LOGIN_CODE_TEMPLATE_ID', value: '-1' } })
    check('SET-69b 登录验证码绑定模板 id 负数 400', r.status === 400, `${r.status} ${r.text.slice(0, 140)}`)
    await saveSetting('SMTP_RESET_CODE_TEMPLATE_ID', '  15  ')
    const trimmed = await api('GET', '/api/settings', { token: rootToken })
    const templateIdSaved = trimmed.json?.items?.find((s) => s.key === 'SMTP_RESET_CODE_TEMPLATE_ID')?.value
    check('SET-69c 绑定模板 id 保存去首尾空白', templateIdSaved === '15', String(templateIdSaved))
    await saveSetting('SMTP_RESET_CODE_TEMPLATE_ID', '')

    // ---------- 开启注册验证码 ----------
    await saveSetting('REGISTER_EMAIL_CODE_ENABLED', 'true')
    info = await serverinfo()
    check('AUTH-26i 开关开启后 serverinfo 注册验证码_required', info.registerEmailCodeRequired === true, JSON.stringify(info))

    const regName = uname('rc')
    const regEmail = `${regName}@test.local`
    r = await api('POST', '/api/auth/register', { body: { userName: regName, email: regEmail, nickName: regName, phone: `15${Date.now().toString().slice(-9)}1`.slice(0, 11), password: rsa('Test1234') } })
    check('AUTH-26j 缺少验证码注册 400', r.status === 400 && r.text.includes('验证码'), `${r.status} ${r.text.slice(0, 140)}`)

    r = await api('POST', '/api/auth/email_code/send', { body: { purpose: 'register', email: regEmail } })
    check('AUTH-26k 注册验证码发送成功', r.status === 200, `${r.status} ${r.text.slice(0, 140)}`)
    check('AUTH-26l SMTP 桩收到注册验证码邮件', stub.state.messages.length === 1 && stub.state.messages[0].rcpts.join().includes(regEmail), JSON.stringify(stub.state.messages.map((m) => m.rcpts)))

    const regMail = stub.state.messages[0]
    const regBody = decodeTextBody(regMail.data)
    const regCode = extractCode(regBody)
    // 未绑定模板回退内置模板：HTML 正文（验证码独立样式块导致 {expireMinutes} 与「分钟内有效」间有标签，按词分别断言）
    check('AUTH-26m 内置模板 HTML 插值 {code}/{expireMinutes}', regCode.length === 6 && regBody.includes('分钟内有效') && regBody.includes('>10<'), regBody.slice(0, 160))
    check('AUTH-26m2 未绑定模板回退内置 HTML 邮件', isHtmlMail(regMail.data), regMail.data.slice(0, 200))

    r = await api('POST', '/api/auth/email_code/send', { body: { purpose: 'register', email: regEmail } })
    check('AUTH-26n 60 秒冷却内重发 429', r.status === 429, `${r.status} ${r.text.slice(0, 140)}`)

    r = await api('POST', '/api/auth/register', { body: { userName: regName, email: regEmail, nickName: regName, phone: `15${Date.now().toString().slice(-9)}1`.slice(0, 11), password: rsa('Test1234'), emailCode: '000000' } })
    check('AUTH-26o 错误验证码注册 400', r.status === 400, `${r.status} ${r.text.slice(0, 140)}`)

    r = await api('POST', '/api/auth/register', { body: { userName: regName, email: regEmail, nickName: regName, phone: `15${Date.now().toString().slice(-9)}1`.slice(0, 11), password: rsa('Test1234'), emailCode: regCode } })
    check('AUTH-26p 正确验证码注册成功', r.status === 200, `${r.status} ${r.text.slice(0, 140)}`)
    const regLogin = await login(regName, 'Test1234')
    check('AUTH-26q 注册用户可登录', regLogin.status === 200, `${regLogin.status}`)

    // 验证码一次性：注册消费后再次使用无效
    const regName2 = uname('r2')
    r = await api('POST', '/api/auth/register', { body: { userName: regName2, email: `${regName2}@test.local`, nickName: regName2, phone: `15${Date.now().toString().slice(-9)}2`.slice(0, 11), password: rsa('Test1234'), emailCode: regCode } })
    check('AUTH-26r 验证码消费后复用 400', r.status === 400, `${r.status} ${r.text.slice(0, 140)}`)

    // ---------- 错 5 次作废（找回密码用途，用注册用户邮箱，避免占用后续场景邮箱的发送冷却） ----------
    const attemptsEmail = `${regName}@test.local`
    r = await api('POST', '/api/auth/email_code/send', { body: { purpose: 'resetPassword', email: attemptsEmail } })
    check('AUTH-26s1 找回密码发码成功', r.status === 200, `${r.status} ${r.text.slice(0, 140)}`)
    let sawExhausted = false
    let lastStatus = 0
    for (let i = 0; i < 5; i++) {
      const rr = await api('POST', '/api/auth/reset_password', { body: { email: attemptsEmail, code: '111111', newPassword: rsa('NewPass123') } })
      lastStatus = rr.status
      if (rr.status === 400 && rr.text.includes('次数过多')) sawExhausted = true
    }
    check('AUTH-26s 连续错 5 次后验证码作废', lastStatus === 400 && sawExhausted, `last=${lastStatus} exhausted=${sawExhausted}`)

    // ---------- 绑定通知中心自定义模板（注册+找回密码用途） ----------
    const customTemplateName = `auth-email-code-e2e-tpl-${Date.now().toString().slice(-8)}`
    r = await api('POST', '/api/notification/template/create', {
      token: rootToken,
      body: {
        name: customTemplateName,
        title: '{{siteName}} 找回 TEST-{{code}}',
        content: 'CODE={{code}};SITE={{siteName}};EXP={{expireMinutes}}；未知{{unknownVar}}保留',
        contentType: 'text',
        variables: JSON.stringify([
          { name: 'code', description: '验证码', example: '123456' },
          { name: 'siteName', description: '网站名称', example: 'MoAI' },
          { name: 'expireMinutes', description: '有效分钟数', example: '10' },
        ]),
        description: 'auth-email-code-e2e 自定义验证码模板',
      },
    })
    check('AUTH-26t1 创建自定义验证码模板成功', r.status === 200 && Number(r.json?.id) > 0, `${r.status} ${r.text.slice(0, 140)}`)
    customTemplateId = String(r.json?.id ?? '')
    await saveSetting('SMTP_REGISTER_CODE_TEMPLATE_ID', customTemplateId)
    await saveSetting('SMTP_RESET_CODE_TEMPLATE_ID', customTemplateId)

    // 注册用途（新邮箱，独立冷却）：自定义模板插值
    const bindName = uname('bd')
    r = await api('POST', '/api/auth/email_code/send', { body: { purpose: 'register', email: `${bindName}@test.local` } })
    check('AUTH-26t 绑定自定义模板发码成功', r.status === 200, `${r.status} ${r.text.slice(0, 140)}`)
    const customMail = stub.state.messages.at(-1)
    const customSubject = decodeSubject(customMail.data)
    const customBody = decodeTextBody(customMail.data)
    const customCode = extractCode(customBody)
    const siteName = (await serverinfo()).name
    check('AUTH-26u 主题模板插值 {code}', customSubject.includes(customCode) && customSubject.includes('TEST') && customSubject.includes(siteName), customSubject)
    check('AUTH-26v 正文模板插值且未知变量字面保留', customBody.includes(`CODE=${customCode}`) && customBody.includes(`SITE=${siteName}`) && customBody.includes('EXP=10') && customBody.includes('{{unknownVar}}'), customBody.slice(0, 200))
    check('AUTH-26v2 自定义纯文本模板按纯文本发送', !isHtmlMail(customMail.data), customMail.data.slice(0, 200))

    // 解绑注册用途（找回密码用途保持绑定）：回退内置模板（HTML），全新邮箱避开冷却
    await saveSetting('SMTP_REGISTER_CODE_TEMPLATE_ID', '')
    const fbName = uname('fb')
    r = await api('POST', '/api/auth/email_code/send', { body: { purpose: 'register', email: `${fbName}@test.local` } })
    check('AUTH-26w1 解绑后回退内置模板（HTML）', r.status === 200 && isHtmlMail(stub.state.messages.at(-1).data), `${r.status} ${stub.state.messages.at(-1).data.slice(0, 160)}`)

    // ---------- 找回密码全链路（找回密码用途保持绑定自定义模板） ----------
    const oldPassword = 'Test1234'
    const newPassword = 'NewPass123'
    r = await api('POST', '/api/auth/email_code/send', { body: { purpose: 'resetPassword', email: `${resetUserName}@test.local` } })
    check('AUTH-26t2 找回密码按绑定模板发码成功', r.status === 200, `${r.status} ${r.text.slice(0, 140)}`)
    const resetBindCode = extractCode(decodeTextBody(stub.state.messages.at(-1).data))

    r = await api('POST', '/api/auth/reset_password', { body: { email: `${resetUserName}@test.local`, code: '999999', newPassword: rsa(newPassword) } })
    check('AUTH-26w 错误验证码重置 400', r.status === 400, `${r.status} ${r.text.slice(0, 140)}`)

    r = await api('POST', '/api/auth/reset_password', { body: { email: `${resetUserName}@test.local`, code: resetBindCode, newPassword: rsa(newPassword) } })
    check('AUTH-26x 正确验证码重置成功', r.status === 200, `${r.status} ${r.text.slice(0, 140)}`)

    const oldLogin = await login(resetUserName, oldPassword)
    check('AUTH-26y 旧密码登录 401', oldLogin.status === 401, `${oldLogin.status}`)
    const newLogin = await login(resetUserName, newPassword)
    check('AUTH-26z 新密码登录成功', newLogin.status === 200, `${newLogin.status}`)

    // ---------- 关闭 SMTP：全链路拒绝 ----------
    await smtpDown()
    info = await serverinfo()
    check('AUTH-26aa 关闭 SMTP 后 serverinfo 开关复位', info.passwordResetEnabled === false && info.registerEmailCodeRequired === false, JSON.stringify(info))
    r = await api('POST', '/api/auth/email_code/send', { body: { purpose: 'resetPassword', email: `${resetUserName}@test.local` } })
    check('AUTH-26ab 关闭 SMTP 后找回发码 400', r.status === 400, `${r.status} ${r.text.slice(0, 140)}`)
    r = await api('POST', '/api/auth/reset_password', { body: { email: `${resetUserName}@test.local`, code: '123456', newPassword: rsa('Whatever123') } })
    check('AUTH-26ac 关闭 SMTP 后重置密码 400', r.status === 400, `${r.status} ${r.text.slice(0, 140)}`)
  } finally {
    // ---------- 恢复快照设置 + 清理所建渠道 ----------
    for (const key of [...SMTP_KEYS, ...CODE_KEYS]) {
      if (snapshot[key] !== undefined) {
        const rr = await api('PUT', '/api/settings', { token: rootToken, body: { key, value: snapshot[key] } })
        if (rr.status !== 200) console.log(`WARN | 恢复设置 ${key} 失败: ${rr.status}`)
      }
    }
    if (smtpChannelId != null) {
      const rr = await api('DELETE', `/api/notification/channel/${smtpChannelId}`, { token: rootToken })
      if (rr.status !== 200) console.log(`WARN | 删除 SMTP 渠道失败: ${rr.status}`)
    }
    if (customTemplateId) {
      const rr = await api('DELETE', `/api/notification/template/${customTemplateId}`, { token: rootToken })
      if (rr.status !== 200) console.log(`WARN | 删除自定义模板失败: ${rr.status}`)
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
