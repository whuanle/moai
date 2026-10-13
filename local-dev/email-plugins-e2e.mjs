// 邮件插件「桩服务」端到端（场景 @DYN-S85~S93）
// 做法：本地起 SMTP + IMAP 双协议桩 → 用独立输出目录 DLL 启动独立后端 → 创建
//       smtp_email_send / imap_email_query 实例并运行 → 断言「动态插件实例 → 注册表模板 →
//       MailKit 客户端 → 协议桩 → 参数/响应解析」整条链路，无需真实邮箱与授权码。
// 前置：dotnet build src/MoAI/MoAI.csproj -o .builds/email（绕开运行中后端的 DLL 锁）；
//       postgres / redis / rabbitmq 已就绪。
// 用法：node local-dev/email-plugins-e2e.mjs
// 环境变量：EM_BACKEND_PORT（默认 5211）｜EM_SMTP_PORT（默认 25254）｜EM_IMAP_PORT（默认 25143）｜EM_KEEP_BACKEND=1 保留后端供排查
import net from 'node:net'
import { spawn } from 'node:child_process'
import crypto from 'node:crypto'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const REPO_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const BACKEND_PORT = Number(process.env.EM_BACKEND_PORT ?? 5212)
const SMTP_PORT = Number(process.env.EM_SMTP_PORT ?? 25254)
const IMAP_PORT = Number(process.env.EM_IMAP_PORT ?? 25143)
const BASE = `http://127.0.0.1:${BACKEND_PORT}`
const USER = 'moai-bot@stub.local'
const PASS = 'authcode-123'

let PASS_COUNT = 0, FAIL_COUNT = 0
const check = (name, cond, detail = '') => {
  if (cond) { PASS_COUNT++; console.log(`PASS | ${name}`) }
  else { FAIL_COUNT++; console.log(`FAIL | ${name} ${detail ? '— ' + detail : ''}`) }
}
const sleep = (ms) => new Promise((r) => setTimeout(r, ms))

// 端口预检：被陈旧进程占用时直接失败（陈旧后端会让探活命中旧代码）
async function ensurePortFree(port, label) {
  try {
    const server = net.createServer()
    await new Promise((resolve, reject) => {
      server.once('error', reject)
      server.listen(port, '127.0.0.1', () => resolve())
    })
    server.close()
  } catch {
    throw new Error(`端口 ${port}（${label}）已被占用，请先释放（netstat -ano | findstr ${port}）`)
  }
}

// ================= SMTP 桩：最小 SMTP 会话（EHLO/AUTH/MAIL/RCPT/DATA/QUIT） =================
const smtpSent = [] // { from, rcpts:[], raw }
let smtpAuthFails = 0

function startSmtpStub() {
  const server = net.createServer((socket) => {
    // 状态机：cmd=命令态 / data=DATA 态 / login-user、login-pass=AUTH LOGIN 两轮问答
    let state = 'cmd'
    let buffer = ''
    let mailFrom = null
    let rcpts = []
    let dataLines = []
    const write = (line) => socket.write(line + '\r\n')

    write('220 stub.local ESMTP ready')

    socket.on('data', (chunk) => {
      buffer += chunk.toString('utf8')
      let idx
      while ((idx = buffer.indexOf('\r\n')) >= 0) {
        const line = buffer.slice(0, idx)
        buffer = buffer.slice(idx + 2)
        handle(line)
      }
    })

    function handle(line) {
      if (state === 'data') {
        if (line === '.') {
          smtpSent.push({ from: mailFrom, rcpts: [...rcpts], raw: dataLines.join('\r\n') + '\r\n' })
          state = 'cmd'
          mailFrom = null
          rcpts = []
          dataLines = []
          write('250 2.0.0 OK: queued as E2E' + smtpSent.length)
        } else {
          dataLines.push(line)
        }
        return
      }

      if (state === 'login-user') {
        const user = Buffer.from(line, 'base64').toString('utf8')
        if (user === USER) { state = 'login-pass'; write('334 ' + Buffer.from('Password:').toString('base64')) }
        else { smtpAuthFails++; state = 'cmd'; write('535 5.7.8 Authentication credentials invalid') }
        return
      }
      if (state === 'login-pass') {
        const pass = Buffer.from(line, 'base64').toString('utf8')
        state = 'cmd'
        if (pass === PASS) write('235 2.7.0 Authentication successful')
        else { smtpAuthFails++; write('535 5.7.8 Authentication credentials invalid') }
        return
      }

      const upper = line.toUpperCase()
      if (upper.startsWith('EHLO') || upper.startsWith('HELO')) {
        socket.write('250-stub.local\r\n250-8BITMIME\r\n250-AUTH PLAIN LOGIN\r\n250 SIZE 26214400\r\n')
      } else if (upper.startsWith('AUTH PLAIN')) {
        const b64 = line.slice('AUTH PLAIN'.length).trim()
        let ok = false
        try {
          const parts = Buffer.from(b64, 'base64').toString('utf8').split('\0')
          ok = parts[parts.length - 2] === USER && parts[parts.length - 1] === PASS
        } catch { /* 非 base64 */ }
        if (ok) write('235 2.7.0 Authentication successful')
        else { smtpAuthFails++; write('535 5.7.8 Authentication credentials invalid') }
      } else if (upper === 'AUTH LOGIN') {
        state = 'login-user'
        write('334 ' + Buffer.from('Username:').toString('base64'))
      } else if (upper.startsWith('MAIL FROM:')) {
        mailFrom = line.slice(line.indexOf(':') + 1).trim()
        write('250 2.1.0 OK')
      } else if (upper.startsWith('RCPT TO:')) {
        rcpts.push(line.slice(line.indexOf(':') + 1).trim())
        write('250 2.1.5 OK')
      } else if (upper === 'DATA') {
        state = 'data'
        dataLines = []
        write('354 End data with <CR><LF>.<CR><LF>')
      } else if (upper === 'RSET' || upper === 'NOOP') {
        write('250 2.0.0 OK')
      } else if (upper === 'QUIT') {
        write('221 2.0.0 Bye')
        socket.end()
      } else {
        write('500 5.5.2 unrecognized')
      }
    }
  })
  return new Promise((resolve) => server.listen(SMTP_PORT, '127.0.0.1', () => resolve(server)))
}

// ================= IMAP 桩：最小 IMAP4rev1（LOGIN/LIST/SELECT/SEARCH/FETCH/STORE） =================
const reportText = 'Q1 revenue is growing.'
const reportB64 = Buffer.from(reportText, 'utf8').toString('base64')
const cjkB64 = Buffer.from('1 月发票共 42 张，合计 12.8 万元。', 'utf8').toString('base64')
const cjkSubjectB64 = Buffer.from('季度发票汇总', 'utf8').toString('base64')

const inBox = [
  {
    uid: 1, seq: 1, seen: false,
    date: 'Mon, 5 Jan 2026 10:00:00 +0800', internaldate: '05-Jan-2026 10:00:00 +0800', sinceDay: '2026-01-05',
    fromName: 'Alice', fromAddr: 'alice@stub.local', toAddr: 'inbox@stub.local',
    subject: 'Welcome to stub', messageId: '<stub-msg-1@stub.local>',
    raw: [
      'From: Alice <alice@stub.local>',
      'To: <inbox@stub.local>',
      'Subject: Welcome to stub',
      'Date: Mon, 5 Jan 2026 10:00:00 +0800',
      'Message-ID: <stub-msg-1@stub.local>',
      'Content-Type: text/plain; charset=utf-8',
      'Content-Transfer-Encoding: 7bit',
      '',
      'Welcome to the stub mailbox. This is the first message.',
      '',
    ].join('\r\n') + '\r\n',
  },
  {
    uid: 2, seq: 2, seen: true,
    date: 'Tue, 6 Jan 2026 09:30:00 +0800', internaldate: '06-Jan-2026 09:30:00 +0800', sinceDay: '2026-01-06',
    fromName: 'Bob', fromAddr: 'bob@stub.local', toAddr: 'inbox@stub.local',
    subject: 'Quarterly report Q1', messageId: '<stub-msg-2@stub.local>',
    raw: [
      'From: Bob <bob@stub.local>',
      'To: <inbox@stub.local>',
      'Subject: Quarterly report Q1',
      'Date: Tue, 6 Jan 2026 09:30:00 +0800',
      'Message-ID: <stub-msg-2@stub.local>',
      'MIME-Version: 1.0',
      'Content-Type: multipart/mixed; boundary="stub-b2"',
      '',
      '--stub-b2',
      'Content-Type: text/plain; charset=utf-8',
      'Content-Transfer-Encoding: 7bit',
      '',
      'Please find the report attached.',
      '',
      '--stub-b2',
      'Content-Type: text/plain; charset=utf-8',
      'Content-Transfer-Encoding: base64',
      'Content-Disposition: attachment; filename="report.txt"',
      '',
      reportB64,
      '--stub-b2--',
      '',
    ].join('\r\n') + '\r\n',
  },
  {
    uid: 3, seq: 3, seen: false,
    date: 'Wed, 7 Jan 2026 14:20:00 +0800', internaldate: '07-Jan-2026 14:20:00 +0800', sinceDay: '2026-01-07',
    fromName: 'Alice', fromAddr: 'alice@stub.local', toAddr: 'inbox@stub.local',
    subject: '=?utf-8?B?' + cjkSubjectB64 + '?=', messageId: '<stub-msg-3@stub.local>',
    raw: [
      'From: Alice <alice@stub.local>',
      'To: <inbox@stub.local>',
      'Subject: =?utf-8?B?' + cjkSubjectB64 + '?=',
      'Date: Wed, 7 Jan 2026 14:20:00 +0800',
      'Message-ID: <stub-msg-3@stub.local>',
      'Content-Type: text/plain; charset=utf-8',
      'Content-Transfer-Encoding: base64',
      '',
      cjkB64,
      '',
    ].join('\r\n') + '\r\n',
  },
]
const archive = []
const folders = () => [
  { name: 'INBOX', attrs: '\\HasNoChildren', msgs: inBox },
  { name: 'Archive', attrs: '\\HasNoChildren', msgs: archive },
]

const imapLogins = { ok: 0, fail: 0 }
const IMAP_DEBUG = process.env.EM_IMAP_DEBUG === '1'

function startImapStub() {
  const server = net.createServer((socket) => {
    let authed = false
    let current = null
    let buffer = Buffer.alloc(0)
    let carry = '' // 字面量续行时已累积的逻辑行前缀
    let literal = null // { need } 待读取的字面量字节数
    const write = (s) => { if (IMAP_DEBUG && s.includes('FETCH')) console.error('IMAP>>', JSON.stringify(s).slice(0, 400)); socket.write(s) }

    write('* OK [CAPABILITY IMAP4rev1 NAMESPACE] stub IMAP ready\r\n')

    socket.on('data', (chunk) => {
      buffer = Buffer.concat([buffer, chunk])
      pump()
    })

    function pump() {
      while (true) {
        if (literal) {
          if (buffer.length < literal.need) return
          const piece = buffer.subarray(0, literal.need)
          buffer = buffer.subarray(literal.need)
          // 字面量内容还原为带引号参数拼回逻辑行
          carry += piece.toString('utf8') + '"'
          literal = null
          continue
        }

        const idx = buffer.indexOf('\r\n')
        if (idx < 0) return
        const line = buffer.subarray(0, idx).toString('utf8')
        buffer = buffer.subarray(idx + 2)

        const m = line.match(/\{(\d+)\}$/)
        if (m) {
          // 命令含字面量（如 SEARCH SUBJECT 中文）：先回 continuation，读完字面量再拼行
          write('+ Ready for literal data\r\n')
          carry += line.slice(0, m.index) + ' "'
          literal = { need: Number(m[1]) }
          continue
        }

        const full = carry + line
        carry = ''
        handleCommand(full)
      }
    }

    function handleCommand(line) {
      const sp = line.indexOf(' ')
      if (sp < 0) return
      const tag = line.slice(0, sp)
      const rest = line.slice(sp + 1)
      const upper = rest.toUpperCase()
      const done = (text) => write(`${tag} ${text}\r\n`)
      const needAuth = () => {
        if (!authed) { done('NO Please authenticate first'); return true }
        return false
      }

      if (upper.startsWith('CAPABILITY')) {
        write('* CAPABILITY IMAP4rev1 NAMESPACE\r\n')
        return done('OK CAPABILITY completed')
      }
      if (upper.startsWith('NAMESPACE')) {
        write('* NAMESPACE (("" "/")) NIL NIL\r\n')
        return done('OK NAMESPACE completed')
      }
      if (upper.startsWith('LOGOUT')) {
        write('* BYE stub logging out\r\n')
        done('OK LOGOUT completed')
        socket.end()
        return
      }
      if (upper.startsWith('LOGIN')) {
        const args = splitArgs(rest.slice(5).trim())
        const [user, pass] = args
        if (user === USER && pass === PASS) {
          authed = true
          imapLogins.ok++
          return done('OK [CAPABILITY IMAP4rev1 NAMESPACE] Logged in')
        }
        imapLogins.fail++
        return done('NO [AUTHENTICATIONFAILED] Invalid credentials')
      }
      if (upper.startsWith('NOOP') || upper.startsWith('CHECK')) return done('OK NOOP completed')
      if (needAuth()) return

      if (upper.startsWith('LIST')) {
        const args = splitArgs(rest.slice(4).trim())
        const pattern = unquote(args[1] ?? '*')
        if (pattern === '') {
          write('* LIST (\\Noselect) "/" ""\r\n')
          return done('OK LIST completed')
        }
        for (const f of folders()) {
          if (pattern === '*' || pattern.toUpperCase() === f.name.toUpperCase()) {
            write(`* LIST (${f.attrs}) "/" "${f.name}"\r\n`)
          }
        }
        return done('OK LIST completed')
      }

      if (upper.startsWith('SELECT') || upper.startsWith('EXAMINE')) {
        const readOnly = upper.startsWith('EXAMINE')
        const name = unquote(splitArgs(rest.slice(upper.startsWith('SELECT') ? 6 : 7).trim())[0] ?? '')
        const folder = folders().find((f) => f.name.toUpperCase() === name.toUpperCase())
        if (!folder) return done(`NO Folder not found: ${name}`)
        current = { name: folder.name, msgs: folder.msgs, readOnly }
        write('* FLAGS (\\Answered \\Flagged \\Deleted \\Seen \\Draft)\r\n')
        write(`* ${folder.msgs.length} EXISTS\r\n`)
        write('* 0 RECENT\r\n')
        write('* OK [PERMANENTFLAGS (\\Answered \\Flagged \\Deleted \\Seen \\Draft)] Flags permitted\r\n')
        write('* OK [UIDVALIDITY 1] UIDs valid\r\n')
        write(`* OK [UIDNEXT ${folder.msgs.length + 1}] Predicted next UID\r\n`)
        return done(`OK [${readOnly ? 'READ-ONLY' : 'READ-WRITE'}] ${readOnly ? 'EXAMINE' : 'SELECT'} completed`)
      }

      if (upper.startsWith('CLOSE') || upper.startsWith('UNSELECT')) {
        current = null
        return done('OK ' + upper.split(' ')[0] + ' completed')
      }
      if (upper.startsWith('STATUS')) {
        return done('NO STATUS not supported by stub')
      }

      if (upper.startsWith('UID SEARCH') || upper.startsWith('SEARCH')) {
        if (!current) return done('NO No folder selected')
        const prefix = upper.startsWith('UID') ? 'UID SEARCH'.length : 'SEARCH'.length
        const criteria = rest.slice(prefix).trim()
        const matched = searchMessages(current.msgs, criteria)
        if (IMAP_DEBUG) console.error('IMAP SEARCH:', JSON.stringify(criteria), '->', JSON.stringify(matched.map((x) => x.uid)))
        const uids = matched.map((x) => x.uid)
        write((uids.length ? `* SEARCH ${uids.join(' ')}\r\n` : '* SEARCH\r\n'))
        return done('OK SEARCH completed')
      }

      if (upper.startsWith('UID FETCH') || upper.startsWith('FETCH')) {
        if (!current) return done('NO No folder selected')
        const prefix = upper.startsWith('UID') ? 'UID FETCH'.length : 'FETCH'.length
        const restAfter = rest.slice(prefix).trim()
        const sp2 = restAfter.indexOf(' ')
        const setStr = restAfter.slice(0, sp2)
        const items = restAfter.slice(sp2 + 1)
        const wantBody = /BODY(\.PEEK)?\[\]/i.test(items)
        const wantEnvelope = /ENVELOPE/i.test(items)
        const wantInternal = /INTERNALDATE/i.test(items)
        const wantSize = /RFC822\.SIZE/i.test(items)
        for (const msg of matchUidSet(current.msgs, setStr)) {
          const parts = [`UID ${msg.uid}`, `FLAGS (${msg.seen ? '\\Seen' : ''})`]
          if (wantInternal) parts.push(`INTERNALDATE "${msg.internaldate}"`)
          if (wantSize) parts.push(`RFC822.SIZE ${Buffer.byteLength(msg.raw, 'utf8')}`)
          if (wantEnvelope) parts.push(`ENVELOPE ${buildEnvelope(msg)}`)
          if (wantBody) {
            const len = Buffer.byteLength(msg.raw, 'utf8')
            write(`* ${msg.seq} FETCH (${parts.join(' ')} BODY[] {${len}}\r\n`)
            write(msg.raw)
            write(')\r\n')
          } else {
            write(`* ${msg.seq} FETCH (${parts.join(' ')})\r\n`)
          }
        }
        return done('OK FETCH completed')
      }

      if (upper.startsWith('UID STORE') || upper.startsWith('STORE')) {
        if (!current) return done('NO No folder selected')
        const prefix = upper.startsWith('UID') ? 'UID STORE'.length : 'STORE'.length
        const restAfter = rest.slice(prefix).trim()
        const sp2 = restAfter.indexOf(' ')
        const setStr = restAfter.slice(0, sp2)
        const op = restAfter.slice(sp2 + 1).trim().toUpperCase()
        const seenFlag = op.includes('\\SEEN')
        for (const msg of matchUidSet(current.msgs, setStr)) {
          if (!seenFlag) continue
          if (op.startsWith('-')) msg.seen = false
          else msg.seen = true
        }
        return done('OK STORE completed')
      }

      return done(`NO Unknown command: ${rest.split(' ')[0]}`)
    }
  })
  return new Promise((resolve) => server.listen(IMAP_PORT, '127.0.0.1', () => resolve(server)))
}

// SEARCH 条件解析：ALL / UNSEEN / FROM "x" / SUBJECT "x" / SINCE d-MMM-yyyy（AND 语义）
function searchMessages(msgs, criteria) {
  const tokens = splitArgs(criteria)
  let result = [...msgs]
  for (let i = 0; i < tokens.length;) {
    const t = tokens[i].toUpperCase()
    if (t === 'ALL') { i++; continue }
    if (t === 'UNSEEN') { result = result.filter((x) => !x.seen); i++; continue }
    if (t === 'SEEN') { result = result.filter((x) => x.seen); i++; continue }
    if ((t === 'FROM' || t === 'SUBJECT') && i + 1 < tokens.length) {
      const needle = unquote(tokens[i + 1]).toLowerCase()
      result = result.filter((x) => plainSubject(x).toLowerCase().includes(needle) || (t === 'FROM' && x.fromAddr.toLowerCase().includes(needle)))
      i += 2
      continue
    }
    if (t === 'SINCE' && i + 1 < tokens.length) {
      const day = parseImapDate(unquote(tokens[i + 1]))
      result = result.filter((x) => x.sinceDay >= day)
      i += 2
      continue
    }
    i++
  }
  return result
}

// fixture uid3 的主题头是 MIME encoded-word；比对时还原为原文
function plainSubject(msg) {
  return msg.subject.includes('=?utf-8?') ? '季度发票汇总' : msg.subject
}

function parseImapDate(text) {
  const m = text.match(/^(\d{1,2})-([A-Za-z]{3})-(\d{4})$/)
  if (!m) return text
  const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']
  const mm = String(months.indexOf(m[2]) + 1).padStart(2, '0')
  return `${m[3]}-${mm}-${m[1].padStart(2, '0')}`
}

// ENVELOPE：RFC3501 括号结构；主题带 encoded-word（与真实服务器一致的原始头形态）
function buildEnvelope(msg) {
  const subject = `"${msg.subject}"`
  const [local, host] = msg.fromAddr.split('@')
  const [toLocal, toHost] = msg.toAddr.split('@')
  const fromPair = `((NIL NIL "${local}" "${host}"))`
  const toPair = `((NIL NIL "${toLocal}" "${toHost}"))`
  return `("${msg.date}" ${subject} ${fromPair} ${fromPair} ${fromPair} ${toPair} NIL NIL NIL "${msg.messageId}")`
}

// —— 参数解析小工具 ——
function splitArgs(input) {
  const args = []
  let cur = ''
  let inQuote = false
  for (let i = 0; i < input.length; i++) {
    const ch = input[i]
    if (inQuote) {
      if (ch === '\\') { cur += input[++i] ?? ''; continue }
      if (ch === '"') { inQuote = false; continue }
      cur += ch
      continue
    }
    if (ch === '"') { inQuote = true; continue }
    if (ch === ' ') { if (cur) args.push(cur); cur = ''; continue }
    cur += ch
  }
  if (cur) args.push(cur)
  return args
}

function unquote(v) {
  return (v ?? '').trim()
}

function matchUidSet(msgs, setStr) {
  const out = []
  for (const part of setStr.split(',')) {
    const range = part.match(/^(\d+):(\d+)$/)
    if (range) {
      const lo = Math.min(Number(range[1]), Number(range[2]))
      const hi = Math.max(Number(range[1]), Number(range[2]))
      out.push(...msgs.filter((x) => x.uid >= lo && x.uid <= hi))
    } else {
      const n = Number(part)
      out.push(...msgs.filter((x) => x.uid === n))
    }
  }
  return out.sort((a, b) => a.uid - b.uid)
}

// ================= 后端：独立输出目录 DLL（绕开运行中后端的 DLL 锁） =================
function startBackend() {
  const dll = path.join(REPO_ROOT, '.builds', 'email', 'MoAI.dll')
  if (!fs.existsSync(dll)) {
    throw new Error(`未找到 ${dll}，请先执行：dotnet build src/MoAI/MoAI.csproj -o .builds/email`)
  }
  const child = spawn('dotnet', [dll], {
    cwd: path.join(REPO_ROOT, 'src', 'MoAI'),
    env: {
      ...process.env,
      ASPNETCORE_ENVIRONMENT: 'Development',
      MoAI__Port: String(BACKEND_PORT),
    },
    stdio: ['ignore', 'pipe', 'pipe'],
  })
  let log = ''
  child.stdout.on('data', (d) => { log += d })
  child.stderr.on('data', (d) => { log += d })
  child.getLog = () => log
  return child
}

function killBackend(child) {
  return new Promise((resolve) => {
    if (!child || child.exitCode !== null) return resolve()
    spawn('taskkill', ['/pid', String(child.pid), '/T', '/F'], { stdio: 'ignore' })
      .on('close', () => resolve())
  })
}

async function waitReady(child) {
  for (let i = 0; i < 90; i++) {
    if (child.exitCode !== null) throw new Error(`后端进程提前退出（code=${child.exitCode}）：\n${child.getLog().slice(-2000)}`)
    try {
      const r = await fetch(`${BASE}/api/common/serverinfo`, { signal: AbortSignal.timeout(3000) })
      if (r.ok) return true
    } catch { /* 未就绪 */ }
    await sleep(2000)
  }
  throw new Error(`后端 ${BASE} 未在 180s 内就绪：\n${child.getLog().slice(-2000)}`)
}

async function api(method, p, { token, body } = {}) {
  const headers = {}
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  if (token) headers['Authorization'] = `Bearer ${token}`
  const res = await fetch(BASE + p, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) })
  const text = await res.text()
  let json = null
  try { json = JSON.parse(text) } catch { /* 非 JSON */ }
  return { status: res.status, json, text }
}

const save = (token, body) => api('POST', '/api/ai/plugin/dynamic/save', { token, body })
const del = (token, pluginKey) => api('DELETE', '/api/ai/plugin/dynamic', { token, body: { pluginKey } })
const run = async (token, key, requestJson) => {
  const r = await api('POST', '/api/ai/plugin/run', { token, body: { key, requestJson } })
  let data = null
  try { data = JSON.parse(r.json?.dataJson ?? 'null') } catch { /* 非 JSON */ }
  return { ...r, data }
}

async function main() {
  await ensurePortFree(BACKEND_PORT, '后端')
  await ensurePortFree(SMTP_PORT, 'SMTP 桩')
  await ensurePortFree(IMAP_PORT, 'IMAP 桩')
  const smtp = await startSmtpStub()
  const imap = await startImapStub()
  console.log(`协议桩已就绪：SMTP 127.0.0.1:${SMTP_PORT} ｜ IMAP 127.0.0.1:${IMAP_PORT}`)
  const backend = startBackend()
  console.log(`后端启动中：${BASE}（.builds/email 独立输出）`)

  try {
    await waitReady(backend)
    console.log('后端已就绪')

    const si = await api('GET', '/api/common/serverinfo')
    const rsaPub = si.json.rsaPublic
    const rsa = (plain) => {
      const key = crypto.createPublicKey({ key: Buffer.from(rsaPub, 'base64'), format: 'der', type: 'spki' })
      return crypto.publicEncrypt({ key, padding: crypto.constants.RSA_PKCS1_PADDING }, Buffer.from(plain, 'utf8')).toString('base64')
    }
    const login = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
    if (login.status !== 200 || !login.json?.accessToken) throw new Error(`admin 登录失败: ${login.status} ${login.text.slice(0, 200)}`)
    const admin = login.json.accessToken

    const TS = Date.now().toString().slice(-8)
    const K_SMTP = `email_smtp_${TS}`
    const K_SMTP_BAD = `email_smtp_bad_${TS}`
    const K_SMTP_CFG = `email_smtp_cfg_${TS}`
    const K_IMAP = `email_imap_${TS}`
    const K_IMAP_BAD = `email_imap_bad_${TS}`

    const smtpConfig = { Host: '127.0.0.1', Port: SMTP_PORT, Security: 'none', Username: USER, Password: PASS, FromName: 'MoAI Bot' }
    const imapConfig = { Host: '127.0.0.1', Port: IMAP_PORT, Security: 'none', Username: USER, Password: PASS }

    // ---------- 注册表：两模板自动发现 ----------
    const registry = await api('GET', '/api/ai/plugin', { token: admin })
    const templates = Array.isArray(registry.json) ? registry.json : (registry.json?.items ?? registry.json?.plugins ?? [])
    const smtpTpl = templates.find((x) => (x.key ?? '') === 'smtp_email_send')
    const imapTpl = templates.find((x) => (x.key ?? '') === 'imap_email_query')
    check('@DYN-S85 smtp_email_send 模板出现在注册表（含配置/参数示例）', !!smtpTpl && smtpTpl.isDynamic === true && !!smtpTpl.configExample && !!smtpTpl.paramsExample, JSON.stringify(smtpTpl)?.slice(0, 200))
    check('@DYN-S92 imap_email_query 模板出现在注册表（含配置/参数示例）', !!imapTpl && imapTpl.isDynamic === true && !!imapTpl.configExample && !!imapTpl.paramsExample, JSON.stringify(imapTpl)?.slice(0, 200))

    // ---------- SMTP：纯文本发送 ----------
    check('创建 smtp_email_send 实例', (await save(admin, { pluginKey: K_SMTP, templeteKey: 'smtp_email_send', title: '桩 SMTP', description: 'e2e', classifyId: 0, config: JSON.stringify(smtpConfig) })).status === 200)
    const before = smtpSent.length
    const plain = await run(admin, K_SMTP, JSON.stringify({
      To: 'dest1@stub.local, dest2@stub.local',
      Subject: 'MoAI Daily: email plugin E2E',
      Body: 'Hello from MoAI E2E. This is a plain text body.',
    }))
    const plainMid = String(plain.data?.MessageId ?? '').replace(/^<|>$/g, '')
    check('@DYN-S86a 纯文本发送成功并回显 Message-ID', plain.json?.success === true && /^[^@\s]+@stub\.local$/.test(plainMid), `${plain.status} mid=${plain.data?.MessageId} ${(plain.json?.error ?? '').slice(0, 140)}`)
    const plainMail = smtpSent[before]
    check('@DYN-S86b SMTP 桩收到 2 个 RCPT 与正文', plainMail?.rcpts?.length === 2 && plainMail?.raw?.includes('Hello from MoAI E2E'), JSON.stringify(plainMail?.rcpts ?? []))
    check('@DYN-S86c Message-ID 头与回显一致、发件人地址正确', !!plainMail && plainMail.raw.toUpperCase().replace(/\r/g, '').includes(`MESSAGE-ID: <${plainMid.toUpperCase()}>`) && plainMail.raw.includes(`From: MoAI Bot <${USER}>`), plainMail?.raw?.split('\r\n').slice(0, 3).join(' / '))

    // ---------- SMTP：HTML + 抄送密送 + 回复地址 ----------
    const before2 = smtpSent.length
    const html = await run(admin, K_SMTP, JSON.stringify({
      To: 'a@stub.local;b@stub.local',
      Cc: 'c@stub.local',
      Bcc: 'd@stub.local',
      ReplyTo: 'reply@stub.local',
      Subject: 'HTML report',
      Body: '<h1>KPI</h1><p>+9.2%</p>',
      IsHtml: true,
    }))
    const htmlMail = smtpSent[before2]
    check('@DYN-S87a HTML 邮件发送成功（4 个 RCPT 含密送）', html.json?.success === true && htmlMail?.rcpts?.length === 4, `${html.status} ${JSON.stringify(htmlMail?.rcpts ?? [])}`)
    check('@DYN-S87b 头部含 Cc/Reply-To 与 text/html 正文', htmlMail?.raw?.includes('Cc: c@stub.local') && htmlMail.raw.includes('Reply-To: reply@stub.local') && htmlMail.raw.includes('Content-Type: text/html'), htmlMail?.raw?.split('\r\n').slice(0, 12).join(' / '))

    // ---------- SMTP：附件 ----------
    const before3 = smtpSent.length
    const att = await run(admin, K_SMTP, JSON.stringify({
      To: 'arch@stub.local',
      Subject: 'report with attachment',
      Body: 'see attached',
      Attachments: [{ FileName: 'report-2026.txt', ContentBase64: Buffer.from(reportText, 'utf8').toString('base64') }],
    }))
    const attMail = smtpSent[before3]
    check('@DYN-S88a 附件发送成功（数量与总字节数回显）', att.json?.success === true && att.data?.AttachmentCount === 1 && att.data?.AttachmentTotalBytes === Buffer.byteLength(reportText), `${att.status} ${JSON.stringify(att.data ?? {})}`)
    check('@DYN-S88b 桩收到 multipart 附件（文件名与内容）', attMail?.raw?.includes('Content-Disposition: attachment') && attMail.raw.includes('filename=report-2026.txt') && attMail.raw.includes('Q1 revenue is growing.'), attMail?.raw?.slice(0, 700))

    // ---------- SMTP：参数校验（先于连接，SMTP 桩零命中） ----------
    const hitsBefore = smtpSent.length
    const badTo = await run(admin, K_SMTP, JSON.stringify({ To: ' , ;', Subject: 's', Body: 'b' }))
    check('@DYN-S89a 收件人为空被拒', badTo.json?.success === false && /To 不能为空/.test(badTo.json?.error ?? ''), (badTo.json?.error ?? '').slice(0, 160))
    const badAddr = await run(admin, K_SMTP, JSON.stringify({ To: 'a@stub.local, not-an-email', Subject: 's', Body: 'b' }))
    check('@DYN-S89b 非法邮箱被拒并带地址', badAddr.json?.success === false && (badAddr.json?.error ?? '').includes('not-an-email'), (badAddr.json?.error ?? '').slice(0, 160))
    const noSubject = await run(admin, K_SMTP, JSON.stringify({ To: 'a@stub.local', Subject: '', Body: 'b' }))
    check('@DYN-S89c 主题为空被拒', noSubject.json?.success === false && /Subject 不能为空/.test(noSubject.json?.error ?? ''), (noSubject.json?.error ?? '').slice(0, 160))
    check('@DYN-S89d 参数校验未触达 SMTP 桩', smtpSent.length === hitsBefore, `sent=${smtpSent.length}`)

    // ---------- SMTP：认证失败与配置缺失 ----------
    await save(admin, { pluginKey: K_SMTP_BAD, templeteKey: 'smtp_email_send', title: '桩 SMTP 坏密码', description: 'e2e', classifyId: 0, config: JSON.stringify({ ...smtpConfig, Password: 'wrong-pass' }) })
    const badAuth = await run(admin, K_SMTP_BAD, JSON.stringify({ To: 'a@stub.local', Subject: 's', Body: 'b' }))
    check('@DYN-S90 SMTP 认证失败归一为可读错误（535）', badAuth.json?.success === false && /SMTP 认证失败/.test(badAuth.json?.error ?? ''), (badAuth.json?.error ?? '').slice(0, 200))
    check('@DYN-S90b 桩侧确认收到坏凭证（AUTH 535）', smtpAuthFails >= 1, `authFails=${smtpAuthFails}`)

    await save(admin, { pluginKey: K_SMTP_CFG, templeteKey: 'smtp_email_send', title: '桩 SMTP 缺配置', description: 'e2e', classifyId: 0, config: JSON.stringify({ ...smtpConfig, Host: '' }) })
    const badCfg = await run(admin, K_SMTP_CFG, JSON.stringify({ To: 'a@stub.local', Subject: 's', Body: 'b' }))
    check('@DYN-S91 配置缺 Host 在运行态校验拒绝', badCfg.json?.success === false && /Host 不能为空/.test(badCfg.json?.error ?? ''), (badCfg.json?.error ?? '').slice(0, 160))

    // ---------- IMAP：recent ----------
    check('创建 imap_email_query 实例', (await save(admin, { pluginKey: K_IMAP, templeteKey: 'imap_email_query', title: '桩 IMAP', description: 'e2e', classifyId: 0, config: JSON.stringify(imapConfig) })).status === 200)
    const recent = await run(admin, K_IMAP, JSON.stringify({ Mode: 'recent', Count: 2 }))
    check('@DYN-S93a recent 列表成功（取最新 2 封，新→旧）', recent.json?.success === true && recent.data?.Messages?.length === 2 && recent.data.Messages[0].Uid === 3 && recent.data.Messages[1].Uid === 2, `${recent.status} ${JSON.stringify(recent.data ?? recent.json?.error ?? '').slice(0, 240)}`)
    check('@DYN-S93b 摘要字段（发件人/主题/已读/时间/总数/截断）', recent.data?.Messages?.[0]?.FromAddress === 'alice@stub.local' && recent.data.Messages[1].Seen === true && recent.data.TotalCount === 3 && recent.data.Truncated === true && /^2026-01-07T14:20:00\+08/.test(recent.data.Messages[0].DateIso ?? ''), JSON.stringify(recent.data?.Messages?.[0] ?? recent.json?.error ?? {}))

    // ---------- IMAP：search（中文主题走 IMAP 字面量） ----------
    const bySubject = await run(admin, K_IMAP, JSON.stringify({ Mode: 'search', SubjectContains: '发票' }))
    check('@DYN-S94a 按中文主题搜索命中 1 封（字面量链路）', bySubject.json?.success === true && bySubject.data?.TotalCount === 1 && bySubject.data.Messages[0]?.Uid === 3, `${bySubject.status} ${JSON.stringify(bySubject.data ?? bySubject.json?.error ?? '').slice(0, 200)}`)

    const byFromSince = await run(admin, K_IMAP, JSON.stringify({ Mode: 'search', FromContains: 'alice', SinceDate: '2026-01-06' }))
    check('@DYN-S95 发件人 + 起始日期组合（AND 语义）', byFromSince.json?.success === true && byFromSince.data?.TotalCount === 1 && byFromSince.data.Messages[0]?.Uid === 3, JSON.stringify(byFromSince.data ?? byFromSince.json?.error ?? '').slice(0, 200))

    const unread = await run(admin, K_IMAP, JSON.stringify({ Mode: 'search', UnreadOnly: true }))
    check('@DYN-S96a 仅未读命中 uid1/uid3', unread.json?.success === true && unread.data?.TotalCount === 2 && unread.data.Messages.map((m) => m.Uid).sort().join(',') === '1,3', JSON.stringify(unread.data?.Messages?.map((m) => m.Uid) ?? []))

    // ---------- IMAP：mark_read 与未读联动 ----------
    const mark = await run(admin, K_IMAP, JSON.stringify({ Mode: 'mark_read', Uid: 3 }))
    check('@DYN-S98a 标记已读成功（回显 UID 与状态）', mark.json?.success === true && mark.data?.UpdatedUid === 3 && mark.data?.Unread === false, JSON.stringify(mark.data ?? mark.json?.error ?? ''))
    const unreadAfter = await run(admin, K_IMAP, JSON.stringify({ Mode: 'search', UnreadOnly: true }))
    check('@DYN-S98b 标记后未读集合联动变化', unreadAfter.data?.TotalCount === 1 && unreadAfter.data.Messages[0]?.Uid === 1, JSON.stringify(unreadAfter.data?.Messages?.map((m) => m.Uid) ?? []))
    const markBack = await run(admin, K_IMAP, JSON.stringify({ Mode: 'mark_read', Uid: 3, Unread: true }))
    const unreadBack = await run(admin, K_IMAP, JSON.stringify({ Mode: 'search', UnreadOnly: true }))
    check('@DYN-S98c 标回未读成功', markBack.json?.success === true && unreadBack.data?.TotalCount === 2, JSON.stringify(unreadBack.data?.Messages?.map((m) => m.Uid) ?? []))

    // ---------- IMAP：detail ----------
    const detail = await run(admin, K_IMAP, JSON.stringify({ Mode: 'detail', Uid: 2 }))
    check('@DYN-S97a detail 取回完整内容（主题/发件人/Message-ID/已读）', detail.json?.success === true && detail.data?.Message?.Subject === 'Quarterly report Q1' && detail.data.Message.FromAddress === 'bob@stub.local' && detail.data.Message.MessageId === 'stub-msg-2@stub.local' && detail.data.Message.Seen === true, JSON.stringify(detail.data?.Message ?? detail.json?.error ?? '').slice(0, 240))
    check('@DYN-S97b detail 正文与附件元信息', detail.data?.Message?.BodyText?.includes('Please find the report attached.') && detail.data.Message.Attachments?.length === 1 && detail.data.Message.Attachments[0].FileName === 'report.txt' && detail.data.Message.Attachments[0].SizeBytes === Buffer.byteLength(reportText), JSON.stringify(detail.data?.Message?.Attachments ?? []))
    const detailCjk = await run(admin, K_IMAP, JSON.stringify({ Mode: 'detail', Uid: 3 }))
    check('@DYN-S97c 中文 MIME 主题与 Base64 正文解码', detailCjk.json?.success === true && ['季度发票汇总', `=?utf-8?B?${cjkSubjectB64}?=`].includes(detailCjk.data?.Message?.Subject ?? '') && String(detailCjk.data?.Message?.BodyText ?? '').includes('12.8 万元'), JSON.stringify({ s: detailCjk.data?.Message?.Subject, b: detailCjk.data?.Message?.BodyText?.slice(0, 40) }))

    // ---------- IMAP：folders ----------
    const foldersRun = await run(admin, K_IMAP, JSON.stringify({ Mode: 'folders' }))
    check('@DYN-S99 folders 列出 INBOX 与 Archive', foldersRun.json?.success === true && foldersRun.data?.Folders?.some((f) => f.FullName === 'INBOX') && foldersRun.data.Folders.some((f) => f.FullName === 'Archive'), JSON.stringify(foldersRun.data?.Folders ?? foldersRun.json?.error ?? ''))

    // ---------- IMAP：参数校验（先于连接） ----------
    const badMode = await run(admin, K_IMAP, JSON.stringify({ Mode: 'delete_all' }))
    check('@DYN-S100a 未知模式被拒', badMode.json?.success === false && (badMode.json?.error ?? '').includes('delete_all'), (badMode.json?.error ?? '').slice(0, 160))
    const noUid = await run(admin, K_IMAP, JSON.stringify({ Mode: 'detail' }))
    check('@DYN-S100b detail 缺 Uid 被拒', noUid.json?.success === false && (noUid.json?.error ?? '').includes('Uid'), (noUid.json?.error ?? '').slice(0, 160))
    const noCriteria = await run(admin, K_IMAP, JSON.stringify({ Mode: 'search' }))
    check('@DYN-S100c search 无过滤条件被拒', noCriteria.json?.success === false && /至少需要一个过滤条件/.test(noCriteria.json?.error ?? ''), (noCriteria.json?.error ?? '').slice(0, 160))
    const badDate = await run(admin, K_IMAP, JSON.stringify({ Mode: 'search', SubjectContains: 'x', SinceDate: '2026/01/06' }))
    check('@DYN-S100d 非法日期被拒', badDate.json?.success === false && /yyyy-MM-dd/.test(badDate.json?.error ?? ''), (badDate.json?.error ?? '').slice(0, 160))

    // ---------- IMAP：认证失败与文件夹不存在 ----------
    await save(admin, { pluginKey: K_IMAP_BAD, templeteKey: 'imap_email_query', title: '桩 IMAP 坏密码', description: 'e2e', classifyId: 0, config: JSON.stringify({ ...imapConfig, Password: 'wrong-pass' }) })
    const imapBad = await run(admin, K_IMAP_BAD, JSON.stringify({ Mode: 'recent' }))
    check('@DYN-S101 IMAP 认证失败归一为可读错误', imapBad.json?.success === false && /IMAP 认证失败/.test(imapBad.json?.error ?? ''), (imapBad.json?.error ?? '').slice(0, 200))
    check('@DYN-S101b 桩侧确认 LOGIN 被拒', imapLogins.fail >= 1, JSON.stringify(imapLogins))

    const noFolder = await run(admin, K_IMAP, JSON.stringify({ Mode: 'recent', Folder: 'NoSuch' }))
    check('@DYN-S102 文件夹不存在给可读提示', noFolder.json?.success === false && /文件夹不存在/.test(noFolder.json?.error ?? ''), (noFolder.json?.error ?? '').slice(0, 160))

    // ---------- 清理 ----------
    for (const key of [K_SMTP, K_SMTP_BAD, K_SMTP_CFG, K_IMAP, K_IMAP_BAD]) {
      await del(admin, key)
    }

    console.log(`\n=== 邮件插件桩 E2E: PASS ${PASS_COUNT} / FAIL ${FAIL_COUNT} ===`)
    if (FAIL_COUNT > 0) process.exitCode = 1
  } finally {
    if (process.env.EM_KEEP_BACKEND === '1') console.log(`EM_KEEP_BACKEND=1，保留后端 pid=${backend.pid}`)
    else await killBackend(backend)
    smtp.close()
    imap.close()
  }
}

main().catch((e) => { console.error('E2E 异常:', e.message); process.exitCode = 1 })
