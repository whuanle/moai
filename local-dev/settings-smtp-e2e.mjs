// SMTP 邮件服务设置 E2E（场景 @SET-S48+）
// 覆盖：root 保存 SMTP_ENABLED/HOST/PORT/SECURITY/USERNAME/PASSWORD/FROM_ADDRESS/FROM_NAME 八项并回读生效（含大小写归一）；
//       保存格式校验（端口范围、加密方式、开关布尔、发件人邮箱格式非法 400）；门禁（member 403、未登录 401）；
//       未启用/未配置时发送测试邮件 400；发送测试邮件全链路（本地 SMTP 桩捕获信封 MAIL FROM/RCPT TO 与 DATA 头）；SMTP 认证（AUTH PLAIN）链路。
// 用法：node local-dev/settings-smtp-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5000）
// 前置：后端运行中；种子账号 admin / abcd123456（root）。开头快照八项原值，结束时恢复（共享开发实例不破坏已配置的 SMTP）。
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
function startSmtpStub({ requireAuth = false, expectUser = '', expectPass = '' } = {}) {
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
const sendTest = (token, to) => api('POST', '/api/settings/email/test', { token, body: { to } })
const SMTP_KEYS = ['SMTP_ENABLED', 'SMTP_HOST', 'SMTP_PORT', 'SMTP_SECURITY', 'SMTP_USERNAME', 'SMTP_PASSWORD', 'SMTP_FROM_ADDRESS', 'SMTP_FROM_NAME']
const getSettingValue = async (token, key) => {
  const q = await api('GET', '/api/settings', { token })
  return (q.json?.items ?? []).find((s) => s.key === key)?.value
}

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const l = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
  const root = l.json?.accessToken ?? null
  check('SET-00 root 登录', Boolean(root))

  // ============ 快照原值（结束时恢复） ============
  const ORIG = {}
  for (const key of SMTP_KEYS) ORIG[key] = await getSettingValue(root, key)

  const member = await mkuser('ss')

  // ============ 保存与回读 ============
  // SET-48 root 保存 SMTP 全量配置并回读生效
  const stub1 = await startSmtpStub()
  const cfg = {
    SMTP_ENABLED: 'true',
    SMTP_HOST: '127.0.0.1',
    SMTP_PORT: String(stub1.port),
    SMTP_SECURITY: 'none',
    SMTP_USERNAME: '',
    SMTP_PASSWORD: '',
    SMTP_FROM_ADDRESS: 'moai@test.local',
    SMTP_FROM_NAME: 'MoAI E2E',
  }
  for (const [key, value] of Object.entries(cfg)) {
    const r = await putSetting(root, key, value)
    check(`SET-48a root 保存 ${key} 200`, r.status === 200, `${r.status} ${r.text.slice(0, 120)}`)
  }
  {
    const readback = {}
    for (const key of SMTP_KEYS) readback[key] = await getSettingValue(root, key)
    const ok = SMTP_KEYS.every((k) => readback[k] === cfg[k])
    check('SET-48b 八项回读与提交值一致', ok, JSON.stringify(readback))
  }

  // SET-48c 大小写归一：开关/加密方式落库小写
  await putSetting(root, 'SMTP_ENABLED', 'TRUE')
  await putSetting(root, 'SMTP_SECURITY', 'SSL')
  check('SET-48c ENABLED=TRUE 归一为 true', (await getSettingValue(root, 'SMTP_ENABLED')) === 'true')
  check('SET-48d SECURITY=SSL 归一为 ssl', (await getSettingValue(root, 'SMTP_SECURITY')) === 'ssl')

  // ============ 保存格式校验 ============
  // SET-49 端口/加密/开关/发件人非法 400
  check('SET-49a 端口 0 400', (await putSetting(root, 'SMTP_PORT', '0')).status === 400)
  check('SET-49b 端口 70000 400', (await putSetting(root, 'SMTP_PORT', '70000')).status === 400)
  check('SET-49c 端口 abc 400', (await putSetting(root, 'SMTP_PORT', 'abc')).status === 400)
  check('SET-49d 加密 tls 400', (await putSetting(root, 'SMTP_SECURITY', 'tls')).status === 400)
  check('SET-49e 开关 yes 400', (await putSetting(root, 'SMTP_ENABLED', 'yes')).status === 400)
  check('SET-49f 发件人 not-an-email 400', (await putSetting(root, 'SMTP_FROM_ADDRESS', 'not-an-email')).status === 400)

  // ============ 门禁 ============
  // SET-50 非 root 修改/发送 403、未登录 401
  check('SET-50a member 保存 SMTP 403', (await putSetting(member.token, 'SMTP_ENABLED', 'true')).status === 403)
  check('SET-50b 未登录保存 SMTP 401', (await putSetting(undefined, 'SMTP_ENABLED', 'true')).status === 401)
  check('SET-50c member 发送测试邮件 403', (await sendTest(member.token, 'x@test.local')).status === 403)
  check('SET-50d 未登录发送测试邮件 401', (await sendTest(undefined, 'x@test.local')).status === 401)

  // ============ 未启用/未配置防护 ============
  // SET-51 未启用发送测试邮件 400；启用但未配置服务器 400
  await putSetting(root, 'SMTP_ENABLED', 'false')
  {
    const r = await sendTest(root, 'x@test.local')
    check('SET-51a 未启用时发送测试邮件 400', r.status === 400 && (r.text.includes('未启用') || r.json?.detail?.includes('未启用')), `${r.status} ${r.text.slice(0, 140)}`)
  }
  await putSetting(root, 'SMTP_ENABLED', 'true')
  await putSetting(root, 'SMTP_HOST', '')
  {
    const r = await sendTest(root, 'x@test.local')
    check('SET-51b 未配置服务器时发送测试邮件 400', r.status === 400, `${r.status} ${r.text.slice(0, 140)}`)
  }

  // ============ 发送测试邮件全链路（免认证中继） ============
  // SET-52 配置指向本地 SMTP 桩（none 免认证），发送测试邮件后桩捕获信封与 DATA
  await putSetting(root, 'SMTP_HOST', '127.0.0.1')
  await putSetting(root, 'SMTP_PORT', String(stub1.port))
  await putSetting(root, 'SMTP_SECURITY', 'none')
  {
    const r = await sendTest(root, 'inbox@test.local')
    check('SET-52a 发送测试邮件 200', r.status === 200, `${r.status} ${r.text.slice(0, 160)}`)
    await new Promise((r) => setTimeout(r, 300))
    const msg = stub1.state.messages[0]
    check('SET-52b 桩收到 1 封邮件', stub1.state.messages.length === 1, `实际 ${stub1.state.messages.length}`)
    check('SET-52c 信封 MAIL FROM=配置发件人', Boolean(msg?.from.includes('moai@test.local')), msg?.from)
    check('SET-52d 信封 RCPT TO=目标收件人', Boolean(msg?.rcpts.some((x) => x.includes('inbox@test.local'))), JSON.stringify(msg?.rcpts))
    check('SET-52e DATA 含 Subject 与 Message-Id 头', Boolean(msg?.data.includes('Subject:') && /Message-Id:/i.test(msg.data)), (msg?.data ?? '').slice(0, 200))
  }

  // ============ SMTP 认证链路 ============
  // SET-53 配置账号密码后发送，桩要求认证并核对 AUTH PLAIN 凭据
  const stub2 = await startSmtpStub({ requireAuth: true, expectUser: 'notify@test.local', expectPass: 'authcode-123' })
  await putSetting(root, 'SMTP_PORT', String(stub2.port))
  await putSetting(root, 'SMTP_USERNAME', 'notify@test.local')
  await putSetting(root, 'SMTP_PASSWORD', 'authcode-123')
  {
    const r = await sendTest(root, 'auth@test.local')
    check('SET-53a 带认证发送测试邮件 200', r.status === 200, `${r.status} ${r.text.slice(0, 160)}`)
    await new Promise((r) => setTimeout(r, 300))
    const auth = stub2.state.authLines.join(' | ')
    check('SET-53b 桩记录 AUTH 认证', stub2.state.authOk, auth)
    check('SET-53c AUTH 凭据与配置一致', auth.includes('notify@test.local') && auth.includes('authcode-123'), auth.slice(0, 160))
    check('SET-53d 认证后桩收到邮件', stub2.state.messages.length === 1, `实际 ${stub2.state.messages.length}`)
  }

  // ============ 恢复执行前原值 ============
  for (const key of SMTP_KEYS) {
    if (ORIG[key] !== undefined) await putSetting(root, key, ORIG[key])
  }
  {
    const readback = {}
    for (const key of SMTP_KEYS) readback[key] = await getSettingValue(root, key)
    const ok = SMTP_KEYS.every((k) => (ORIG[k] ?? '') === (readback[k] ?? ''))
    check('SET-90 结束恢复执行前原值（八项）', ok, JSON.stringify({ ORIG, readback }))
  }

  stub1.server.close()
  stub2.server.close()
  console.log(`\n总计: PASS ${PASS} / FAIL ${FAIL}`)
  if (FAIL > 0) process.exit(1)
}

main().catch((e) => { console.error('E2E 执行异常:', e); process.exit(1) })
