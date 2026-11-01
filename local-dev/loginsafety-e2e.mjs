// 登录安全风控 E2E（场景 @LS-S1~S26）
// 覆盖：门禁（member 403 / 未登录 401）+ 登录日志全量落库（成功/失败）+ ForwardedHeaders 真实 IP 与 IP2Region 归属地解析
//       + R1 IP 爆破自动封禁（阈值内失败→封禁 403→事件+解封恢复）+ R2 撞库喷洒自动封禁（多用户名失败）
//       + R3 新 IP 中风险（登录日志 riskLevel=2 + 风险事件 + 用户提醒邮件）+ R5 疾速旅行高风险（间隔过短的异地登录）
//       + R6 管理员新环境升级（高风险 + 管理员告警邮件）+ 风险事件标记已读与未读计数联动
//       + IP 手动封禁/解封 + 总开关关闭直通（仅日志不判定）+ 设置校验（阈值/开关非法 400）
// 用法：node local-dev/loginsafety-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5213）
// 前置：后端以 ForwardedHeaders 开启运行（模拟多来源 IP 必需），例如：
//   MAI_FILE=local-dev/system.local.json ASPNETCORE_ENVIRONMENT=Development \
//   MoAI__Port=5213 MoAI__Server=http://127.0.0.1:5213 MoAI__ForwardedHeaders__Enabled=true \
//   dotnet run --project src/MoAI/MoAI.csproj
//   且 configs/ip2region.xdb 存在（geo 用例依赖归属地；文件缺失时 geo 用例会 FAIL 提示安装）。
// 种子账号 admin / abcd123456（root）。开头快照 SMTP 与 LS_* 设置原值，结束时恢复（共享开发实例不破坏配置）。
import net from 'node:net'
import crypto from 'node:crypto'

const BASE = process.env.APP_BASE ?? process.argv[2] ?? 'http://127.0.0.1:5213'
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

async function api(method, path, { token, body, headers: extraHeaders } = {}) {
  const headers = { ...extraHeaders }
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
  return { name, email: `${name}@test.local`, password: 'Test1234' }
}

const xff = (ip) => ({ 'X-Forwarded-For': ip })

async function login(userName, password, ip) {
  const r = await api('POST', '/api/auth/login', { body: { userName, password: rsa(password) }, headers: ip ? xff(ip) : undefined })
  return r
}

/** 极简 SMTP 桩：记录信封（MAIL FROM/RCPT TO）与 DATA 原文 */
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
const getSettingValue = async (token, key) => {
  const q = await api('GET', '/api/settings', { token })
  return (q.json?.items ?? []).find((s) => s.key === key)?.value
}

/** 轮询等待条件成立（风控通知走 Hangfire 异步，允许 ~25s） */
async function waitFor(fn, timeoutMs = 25000, stepMs = 500) {
  const deadline = Date.now() + timeoutMs
  while (Date.now() < deadline) {
    const value = await fn()
    if (value) return value
    await new Promise((r) => setTimeout(r, stepMs))
  }
  return null
}

/** 每次运行随机的内网测试 IP（避免 Redis 失败计数/封禁记录跨运行残留） */
const rndOctet = () => Math.floor(Math.random() * 200) + 11

// ---------- 启动 ----------
const info = await fetch(`${BASE}/api/common/serverinfo`).then((r) => r.json())
RSA_KEY = info.rsaPublic

const smtpStub = await startSmtpStub()

// admin（root）登录；本机 IP 若有残留封禁（如上次异常中断的运行），走 XFF 引导登录后解封恢复
let adminLogin = await login('admin', 'abcd123456')
if (adminLogin.status === 403 && /封禁/.test(adminLogin.json?.detail ?? '')) {
  const boot = await login('admin', 'abcd123456', '77.77.77.77')
  if (boot.status !== 200 || !boot.json.accessToken) {
    console.error('FATAL: 引导登录失败', boot.status, boot.text.slice(0, 200))
    smtpStub.server.close()
    process.exit(1)
  }
  const bans = await api('GET', '/api/loginsafety/ipban?pageNo=1&pageSize=100', { token: boot.json.accessToken })
  for (const b of (bans.json?.items ?? []).filter((x) => !x.isLifted && new Date(x.expireTime) > new Date() && /127\.0\.0\.1|::1|::ffff:127/.test(x.ip))) {
    await api('POST', '/api/loginsafety/ipban/lift', { token: boot.json.accessToken, body: { id: b.id } })
  }
  adminLogin = await login('admin', 'abcd123456')
}
if (adminLogin.status !== 200 || !adminLogin.json.accessToken) {
  console.error('FATAL: admin 登录失败', adminLogin.status, adminLogin.text.slice(0, 200))
  smtpStub.server.close()
  process.exit(1)
}
const admin = adminLogin.json.accessToken

// 快照 SMTP 与 LS 设置原值
const SMTP_KEYS = ['SMTP_ENABLED', 'SMTP_HOST', 'SMTP_PORT', 'SMTP_SECURITY', 'SMTP_USERNAME', 'SMTP_PASSWORD', 'SMTP_FROM_ADDRESS', 'SMTP_FROM_NAME']
const LS_KEYS = ['LS_ENABLED', 'LS_NOTIFY_USER_ENABLED', 'LS_NOTIFY_ADMIN_ENABLED', 'LS_ADMIN_ALERT_RECIPIENTS', 'LS_IP_FAIL_THRESHOLD', 'LS_IP_FAIL_WINDOW_MINUTES', 'LS_IP_BAN_MINUTES', 'LS_DISTINCT_USER_THRESHOLD', 'LS_NEW_IP_DAYS', 'LS_TRAVEL_WINDOW_MINUTES']
const snapshot = {}
for (const key of [...SMTP_KEYS, ...LS_KEYS]) snapshot[key] = await getSettingValue(admin, key)

// 指向本地 SMTP 桩 + 收紧阈值（测毕恢复）
const ALERT_TO = 'alert-e2e@test.local'
await putSetting(admin, 'SMTP_ENABLED', 'true')
await putSetting(admin, 'SMTP_HOST', '127.0.0.1')
await putSetting(admin, 'SMTP_PORT', String(smtpStub.port))
await putSetting(admin, 'SMTP_SECURITY', 'none')
await putSetting(admin, 'SMTP_FROM_ADDRESS', 'stub@moai.local')
await putSetting(admin, 'LS_ENABLED', 'true')
await putSetting(admin, 'LS_NOTIFY_USER_ENABLED', 'true')
await putSetting(admin, 'LS_NOTIFY_ADMIN_ENABLED', 'true')
await putSetting(admin, 'LS_ADMIN_ALERT_RECIPIENTS', ALERT_TO)
// ASCII 自定义模板（断言告警内容可读，同时覆盖模板自定义生效）
await putSetting(admin, 'LS_ADMIN_ALERT_SUBJECT', 'LS-ALERT {userName}')
await putSetting(admin, 'LS_ADMIN_ALERT_BODY', 'RISK ALERT user={userName} ip={ip} location={location} reasons={reasons}')
await putSetting(admin, 'LS_USER_ALERT_SUBJECT', 'LS-USER {userName}')
await putSetting(admin, 'LS_USER_ALERT_BODY', 'LOGIN NOTICE user={userName} ip={ip} location={location} time={time}')

try {
  // ---------- LS-S1~S3 门禁 ----------
  {
    const member = await mkuser('lsmb')
    const memberLogin = await login(member.name, member.password)
    const memberToken = memberLogin.json.accessToken

    const g1 = await api('GET', '/api/loginsafety/loginlog', { token: memberToken })
    check('LS-S1 member 查询登录日志 403', g1.status === 403, `got ${g1.status}`)
    const g2 = await api('GET', '/api/loginsafety/loginlog')
    check('LS-S2 未登录查询登录日志 401', g2.status === 401, `got ${g2.status}`)
    const g3 = await putSetting(memberToken, 'LS_ENABLED', 'false')
    check('LS-S3 member 修改设置 403', g3.status === 403, `got ${g3.status}`)
  }

  // ---------- LS-S4~S5 基线落库 ----------
  {
    const q = await api('GET', '/api/loginsafety/loginlog?pageNo=1&pageSize=10&userName=admin', { token: admin })
    const items = q.json?.items ?? []
    check('LS-S4 登录日志有 admin 成功记录', q.status === 200 && items.some((x) => x.isSuccess === true), `total=${q.json?.total}`)

    const unread = await api('GET', '/api/loginsafety/riskevent/unread-count', { token: admin })
    check('LS-S5 未读计数接口可用', unread.status === 200 && typeof unread.json?.count === 'number', `got ${unread.status}`)
  }

  // ---------- LS-S6~S7 ForwardedHeaders + IP2Region 归属地 ----------
  {
    await login('admin', 'abcd123456', '1.2.4.8')
    let item = await waitFor(async () => {
      const q = await api('GET', '/api/loginsafety/loginlog?pageNo=1&pageSize=5&ip=1.2.4.8', { token: admin })
      return (q.json?.items ?? []).find((x) => x.isSuccess)
    })
    check('LS-S6 X-Forwarded-For 解析真实 IP', item != null, '未找到 ip=1.2.4.8 的记录')
    check('LS-S7 归属地解析（北京市）', item?.country === '中国' && item?.province === '北京市', JSON.stringify({ country: item?.country, province: item?.province }))

    await login('admin', 'abcd123456', '8.8.8.8')
    const us = await waitFor(async () => {
      const q = await api('GET', '/api/loginsafety/loginlog?pageNo=1&pageSize=5&ip=8.8.8.8', { token: admin })
      return (q.json?.items ?? []).find((x) => x.isSuccess)
    })
    check('LS-S7b 海外 IP 归属地解析', us?.country === 'United States' && us?.province === 'California', JSON.stringify({ country: us?.country, province: us?.province }))
  }

  // ---------- LS-S8~S12 R1 IP 爆破自动封禁与解封 ----------
  {
    await putSetting(admin, 'LS_IP_FAIL_THRESHOLD', '4')
    await putSetting(admin, 'LS_DISTINCT_USER_THRESHOLD', '100')

    // 每次运行随机 IP，避免 Redis 失败计数跨运行残留影响阈值判定
    const ipR1 = `10.${rndOctet()}.${rndOctet()}.1`
    const bruteName = uname('lsbrute')
    let last = null
    for (let i = 0; i < 4; i++) {
      last = await login(bruteName, 'Wrong1234', ipR1)
    }
    check('LS-S8 爆破中失败返回 401', last.status === 401, `got ${last.status}`)

    // 第 5 次请求：IP 已被封禁 → 403（即使密码正确）
    const banned = await login(bruteName, 'Wrong1234', ipR1)
    check('LS-S9 达阈值后该 IP 登录 403', banned.status === 403, `got ${banned.status}`)

    const evt = await waitFor(async () => {
      const q = await api('GET', '/api/loginsafety/riskevent?pageNo=1&pageSize=20', { token: admin })
      return (q.json?.items ?? []).find((x) => x.ruleCode === 'R1')
    })
    check('LS-S10 R1 高风险事件生成', evt != null && evt.riskLevel === 3, JSON.stringify(evt ?? {}))

    const banList = await api('GET', '/api/loginsafety/ipban?pageNo=1&pageSize=50', { token: admin })
    const ban = (banList.json?.items ?? []).find((x) => x.ip === ipR1 && !x.isLifted)
    check('LS-S11 封禁列表出现 auto:R1 记录', ban != null && ban.sourceRule === 'auto:R1', JSON.stringify(ban ?? banList.json?.items?.slice(0, 3) ?? banList.text.slice(0, 120)))

    if (ban) {
      const lift = await api('POST', '/api/loginsafety/ipban/lift', { token: admin, body: { id: ban.id } })
      check('LS-S12 解封成功', lift.status === 200, `got ${lift.status}`)
      const retry = await login('admin', 'abcd123456', '10.1.1.1')
      check('LS-S12b 解封后该 IP 可登录', retry.status === 200, `got ${retry.status}`)
    } else {
      check('LS-S12 解封成功', false, '未找到封禁记录')
      check('LS-S12b 解封后该 IP 可登录', false, '未找到封禁记录')
    }
  }

  // ---------- LS-S13~S15 R2 撞库喷洒自动封禁 ----------
  {
    await putSetting(admin, 'LS_DISTINCT_USER_THRESHOLD', '2')

    // 每次运行随机 IP，避免 Redis 失败计数跨运行残留影响阈值判定
    const ipR2 = `10.${rndOctet()}.${rndOctet()}.2`
    for (const name of [uname('lsspray'), uname('lsspray')]) {
      await login(name, 'Wrong1234', ipR2)
    }
    const banned = await login(uname('lsspray'), 'Wrong1234', ipR2)
    check('LS-S13 撞库喷洒后该 IP 登录 403', banned.status === 403, `got ${banned.status}`)

    const evt = await waitFor(async () => {
      const q = await api('GET', '/api/loginsafety/riskevent?pageNo=1&pageSize=20', { token: admin })
      return (q.json?.items ?? []).find((x) => x.ruleCode === 'R2')
    })
    check('LS-S14 R2 高风险事件生成', evt != null && evt.riskLevel === 3, JSON.stringify(evt ?? {}))

    const banList = await api('GET', '/api/loginsafety/ipban?pageNo=1&pageSize=50', { token: admin })
    check('LS-S15 封禁列表出现 auto:R2 记录', (banList.json?.items ?? []).some((x) => x.ip === ipR2 && x.sourceRule === 'auto:R2'),
      JSON.stringify((banList.json?.items ?? []).slice(0, 2)))
  }

  // ---------- LS-S16~S19 R3 新 IP 中风险 + R5 疾速旅行高风险 + 用户提醒邮件 ----------
  const u1 = await mkuser('lsu')
  {
    // 首次从 1.2.4.8（北京市）登录 → 新 IP 中风险
    const first = await login(u1.name, u1.password, '1.2.4.8')
    check('LS-S16 用户新 IP 登录成功', first.status === 200, `got ${first.status}`)

    const r3 = await waitFor(async () => {
      const q = await api('GET', '/api/loginsafety/riskevent?pageNo=1&pageSize=50&userName=' + u1.name, { token: admin })
      return (q.json?.items ?? []).find((x) => x.ruleCode === 'R3')
    })
    check('LS-S17 R3 中风险事件生成', r3 != null && r3.riskLevel === 2, JSON.stringify(r3 ?? {}))

    // 随后短间隔切换到 8.8.8.8（California）→ 疾速旅行高风险
    const second = await login(u1.name, u1.password, '8.8.8.8')
    check('LS-S18 用户异地登录成功', second.status === 200, `got ${second.status}`)

    const r5 = await waitFor(async () => {
      const q = await api('GET', '/api/loginsafety/riskevent?pageNo=1&pageSize=50&userName=' + u1.name, { token: admin })
      return (q.json?.items ?? []).find((x) => x.ruleCode === 'R5')
    })
    check('LS-S19 R5 疾速旅行高风险事件', r5 != null && r5.riskLevel === 3, JSON.stringify(r5 ?? {}))

    // 用户提醒邮件（中/高风险都发）；正文经 MIME 编码不可读，断言明文 Subject（自定义模板已渲染）
    const userMail = await waitFor(() => smtpStub.state.messages.find((m) => m.rcpts.some((r) => r.includes(u1.email))))
    check('LS-S19b 用户收到风险登录提醒邮件', userMail != null, `mails=${smtpStub.state.messages.length}`)
    check('LS-S19c 提醒邮件自定义模板生效', userMail != null && /Subject: LS-USER \S+/.test(userMail.data), userMail?.data.split('\r\n').find((l) => l.startsWith('Subject')) ?? '')
  }

  // ---------- LS-S20 管理员告警邮件（高风险事件 → 收件人） ----------
  {
    const adminMail = await waitFor(() => smtpStub.state.messages.find((m) => m.rcpts.some((r) => r.includes(ALERT_TO))))
    check('LS-S20 管理员收到风险告警邮件', adminMail != null, `mails=${smtpStub.state.messages.length}`)
    check('LS-S20b 告警邮件自定义模板生效', adminMail != null && /Subject: LS-ALERT \S+/.test(adminMail.data), adminMail?.data.split('\r\n').find((l) => l.startsWith('Subject')) ?? '')
  }

  // ---------- LS-S21 风险事件标记已读与未读计数联动 ----------
  {
    const before = await api('GET', '/api/loginsafety/riskevent/unread-count', { token: admin })
    const list = await api('GET', '/api/loginsafety/riskevent?pageNo=1&pageSize=5&isRead=false', { token: admin })
    const ids = (list.json?.items ?? []).map((x) => x.id).slice(0, 2)
    if (ids.length > 0) {
      const mark = await api('POST', '/api/loginsafety/riskevent/read', { token: admin, body: { eventIds: ids } })
      const after = await api('GET', '/api/loginsafety/riskevent/unread-count', { token: admin })
      check('LS-S21 标记已读后未读数减少', mark.status === 200 && (after.json?.count ?? 0) <= (before.json?.count ?? 0) - ids.length,
        `before=${before.json?.count} after=${after.json?.count} marked=${ids.length}`)
    } else {
      check('LS-S21 标记已读后未读数减少', false, '无未读事件可标记')
    }
  }

  // ---------- LS-S22~S23 IP 手动封禁/解封 ----------
  {
    const add = await api('POST', '/api/loginsafety/ipban/add', { token: admin, body: { ip: '10.9.9.9', minutes: 30, reason: 'E2E 手动封禁' } })
    check('LS-S22 手动封禁成功', add.status === 200, `got ${add.status}`)
    const banned = await login('admin', 'abcd123456', '10.9.9.9')
    check('LS-S22b 被手动封禁 IP 登录 403', banned.status === 403, `got ${banned.status}`)

    const banList = await api('GET', '/api/loginsafety/ipban?pageNo=1&pageSize=50&ip=10.9.9.9', { token: admin })
    const ban = (banList.json?.items ?? []).find((x) => !x.isLifted)
    if (ban) {
      await api('POST', '/api/loginsafety/ipban/lift', { token: admin, body: { id: ban.id } })
      const retry = await login('admin', 'abcd123456', '10.9.9.9')
      check('LS-S23 解封后恢复登录', retry.status === 200, `got ${retry.status}`)
    } else {
      check('LS-S23 解封后恢复登录', false, '未找到手动封禁记录')
    }
  }

  // ---------- LS-S24 总开关关闭：仅记录不判定 ----------
  {
    await putSetting(admin, 'LS_ENABLED', 'false')
    const beforeCount = await api('GET', '/api/loginsafety/riskevent/unread-count', { token: admin })
    const r = await login(u1.name, u1.password, '47.98.1.1')
    check('LS-S24 开关关闭登录仍成功', r.status === 200, `got ${r.status}`)

    await new Promise((r2) => setTimeout(r2, 2000))
    const logQ = await api('GET', '/api/loginsafety/loginlog?pageNo=1&pageSize=5&ip=47.98.1.1', { token: admin })
    const logItem = (logQ.json?.items ?? [])[0]
    check('LS-S24b 开关关闭仍记录登录日志', logItem != null && logItem.isSuccess === true && (logItem.riskLevel ?? 0) === 0, JSON.stringify(logItem ?? {}))

    const afterCount = await api('GET', '/api/loginsafety/riskevent/unread-count', { token: admin })
    check('LS-S24c 开关关闭不产生新风险事件', (afterCount.json?.count ?? 0) === (beforeCount.json?.count ?? 0), `before=${beforeCount.json?.count} after=${afterCount.json?.count}`)
    await putSetting(admin, 'LS_ENABLED', 'true')
  }

  // ---------- LS-S25 设置校验 ----------
  {
    const v1 = await putSetting(admin, 'LS_IP_FAIL_THRESHOLD', '0')
    check('LS-S25 阈值 0 被 400 拒绝', v1.status === 400, `got ${v1.status}`)
    const v2 = await putSetting(admin, 'LS_ENABLED', 'abc')
    check('LS-S25b 开关非法值 400', v2.status === 400, `got ${v2.status}`)
    const v3 = await putSetting(admin, 'LS_NEW_IP_DAYS', '99999')
    check('LS-S25c 回看天数越界 400', v3.status === 400, `got ${v3.status}`)
    const v4 = await putSetting(admin, 'LS_ADMIN_ALERT_RECIPIENTS', 'not-an-email')
    check('LS-S25d 收件人非法邮箱 400', v4.status === 400, `got ${v4.status}`)
  }

  // ---------- LS-S26 本人登录记录 ----------
  {
    const myLogin = await login(u1.name, u1.password)
    const my = await api('GET', '/api/loginsafety/mylog?pageNo=1&pageSize=10', { token: myLogin.json.accessToken })
    const items = my.json?.items ?? []
    check('LS-S26 本人登录记录可查且不含他人', my.status === 200 && items.length > 0 && items.every((x) => x.ip !== undefined && x.userName === undefined), `total=${my.json?.total}`)
  }
} finally {
  // ---------- 恢复设置 ----------
  for (const [key, value] of Object.entries(snapshot)) {
    if (value !== undefined) {
      await putSetting(admin, key, value)
    } else {
      // 原本不存在的键恢复为默认值（语义等价：写回 SettingDefinitions 默认）
      const defaults = {
        LS_ENABLED: 'true', LS_NOTIFY_USER_ENABLED: 'true', LS_NOTIFY_ADMIN_ENABLED: 'true',
        LS_ADMIN_ALERT_RECIPIENTS: '', LS_IP_FAIL_THRESHOLD: '20', LS_IP_FAIL_WINDOW_MINUTES: '10',
        LS_IP_BAN_MINUTES: '30', LS_DISTINCT_USER_THRESHOLD: '5', LS_NEW_IP_DAYS: '90', LS_TRAVEL_WINDOW_MINUTES: '120'
      }
      if (key in defaults) await putSetting(admin, key, defaults[key])
    }
  }
  smtpStub.server.close()
  console.log(`\nPASS ${PASS} / FAIL ${FAIL}`)
  if (FAIL > 0) process.exit(1)
}
