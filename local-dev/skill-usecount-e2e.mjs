// 技能使用计数 E2E（后端 127.0.0.1:5210）
// 场景：应用绑定技能并发布 → 桩模型对话中 call_tool(skill_<key>) 装载技能 → skill.use_count 自增
//       → skill options 接口回显 useCount；默认技能目录（userconfig）同样回显。
import crypto from 'node:crypto'
import http from 'node:http'

const BASE = process.argv[2] ?? 'http://127.0.0.1:5210'
let PASS = 0, FAIL = 0
const check = (name, cond, detail = '') => {
  if (cond) { PASS++; console.log(`PASS | ${name}`) }
  else { FAIL++; console.log(`FAIL | ${name} ${detail ? '— ' + detail : ''}`) }
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

const TS = Date.now().toString().slice(-8)

// ===== 桩模型：指令含 skill_ 时发起装载调用，收到工具结果后回总结 =====
function startStub() {
  const stub = http.createServer((req, res) => {
    let body = ''
    req.on('data', (c) => { body += c })
    req.on('end', () => {
      let payload = {}
      try { payload = JSON.parse(body || '{}') } catch { }
      if (String(req.url).includes('/embeddings')) {
        res.writeHead(200, { 'Content-Type': 'application/json' })
        res.end(JSON.stringify({ object: 'list', model: 'e', data: [{ object: 'embedding', index: 0, embedding: [0.1] }], usage: { prompt_tokens: 1, total_tokens: 1 } }))
        return
      }
      const messages = payload.messages ?? []
      const allText = messages.map((m) => `${m.role}:${typeof m.content === 'string' ? m.content : JSON.stringify(m.content ?? '')}`).join('\n')
      const reply = (text) => {
        if (payload.stream) {
          res.writeHead(200, { 'Content-Type': 'text/event-stream' })
          const base = { id: 's', object: 'chat.completion.chunk', created: 1, model: 'stub' }
          res.write('data: ' + JSON.stringify({ ...base, choices: [{ index: 0, delta: { role: 'assistant', content: text }, finish_reason: null }] }) + '\n\n')
          res.write('data: ' + JSON.stringify({ ...base, choices: [{ index: 0, delta: {}, finish_reason: 'stop' }] }) + '\n\n')
          res.write('data: [DONE]\n\n')
          res.end()
          return
        }
        res.writeHead(200, { 'Content-Type': 'application/json' })
        res.end(JSON.stringify({ id: 's', object: 'chat.completion', created: 1, model: 'stub', choices: [{ index: 0, message: { role: 'assistant', content: text }, finish_reason: 'stop' }], usage: { prompt_tokens: 1, completion_tokens: 1, total_tokens: 2 } }))
      }
      const toolCall = (name, argsJson) => {
        if (payload.stream) {
          res.writeHead(200, { 'Content-Type': 'text/event-stream' })
          const base = { id: 's', object: 'chat.completion.chunk', created: 1, model: 'stub' }
          res.write('data: ' + JSON.stringify({ ...base, choices: [{ index: 0, delta: { role: 'assistant', tool_calls: [{ index: 0, id: 'call_' + Date.now(), type: 'function', function: { name, arguments: '' } }] }, finish_reason: null }] }) + '\n\n')
          res.write('data: ' + JSON.stringify({ ...base, choices: [{ index: 0, delta: { tool_calls: [{ index: 0, function: { arguments: argsJson } }] }, finish_reason: null }] }) + '\n\n')
          res.write('data: ' + JSON.stringify({ ...base, choices: [{ index: 0, delta: {}, finish_reason: 'tool_calls' }] }) + '\n\n')
          res.write('data: [DONE]\n\n')
          res.end()
          return
        }
        res.writeHead(200, { 'Content-Type': 'application/json' })
        res.end(JSON.stringify({ id: 's', object: 'chat.completion', created: 1, model: 'stub', choices: [{ index: 0, message: { role: 'assistant', tool_calls: [{ id: 'call_' + Date.now(), type: 'function', function: { name, arguments: argsJson } }] }, finish_reason: 'tool_calls' }], usage: { prompt_tokens: 1, completion_tokens: 1, total_tokens: 2 } }))
      }

      // 工具结果已回传 → 总结
      if (messages.some((m) => m.role === 'tool')) {
        return reply('技能已装载完成。')
      }
      // 指令披露了 skill_ 工具 → 调用 call_tool(skill_<key>)（取披露列表里的第一个 skill_ 前缀工具名）
      if (allText.includes('skill_')) {
        const m = allText.match(/skill_[A-Za-z0-9_]+/)
        return toolCall('call_tool', JSON.stringify({ toolName: m ? m[0] : 'skill_x', argumentsJson: '{}' }))
      }
      return reply('好的。')
    })
  })
  return new Promise((r) => stub.listen(0, '127.0.0.1', () => r(stub)))
}

async function adminLogin() {
  const l = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
  return l.status === 200 ? l.json.accessToken : null
}

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic
  const adminToken = await adminLogin()
  if (!adminToken) { console.log('FAIL | admin 登录失败'); process.exit(1) }

  const stub = await startStub()
  const stubPort = stub.address().port

  const chName = `skc-渠道-${TS}`
  await api('POST', '/api/ai/channel', { token: adminToken, body: { providerKey: 'openai', name: chName, protocolFamily: 'openaiChatCompletions', baseUrl: `http://127.0.0.1:${stubPort}/v1`, apiKey: 'k', enabled: true, description: 'skill-usecount E2E 桩渠道' } })
  const channels = await api('GET', '/api/ai/channel', { token: adminToken })
  const CH = String((channels.json?.items ?? []).find((c) => c.name === chName)?.id ?? '')
  await api('POST', '/api/ai/model', { token: adminToken, body: { channelId: CH, meta: { modelId: `skc-chat-${TS}`, name: `skc-chat-${TS}`, modelKind: 'conversation', description: 'stub' }, enabled: true, isPublic: true } })
  const models = await api('GET', `/api/ai/model?channelId=${CH}`, { token: adminToken })
  const CHAT_MODEL = String((models.json?.items ?? []).find((m) => m.name === `skc-chat-${TS}`)?.id ?? '')

  try {
    // 团队先行：应用默认技能不含个人技能，创建团队技能供绑定
    const TID = Number((await api('POST', '/api/team', { token: adminToken, body: { name: 'skc-team-' + TS } })).json.value)

    // 技能：无文件（不依赖沙箱，未启用沙箱时仅返回说明，同样计一次使用）
    const SKILL_KEY = `usec_${TS}`
    const cs = await api('POST', '/api/skill', { token: adminToken, body: { key: SKILL_KEY, name: '计数技能' + TS, description: '计数验证', instructions: '# 用法', files: [], teamId: TID } })
    check('SU-01 创建团队技能 200', cs.status === 200, `${cs.status} ${cs.text.slice(0, 120)}`)
    const skills = await api('GET', `/api/skill/options?teamId=${TID}`, { token: adminToken })
    const SKILL_ID = String((skills.json?.items ?? []).find((x) => x.key === SKILL_KEY)?.id ?? '')
    check('SU-02 技能选项回显 useCount=0', (skills.json?.items ?? []).find((x) => x.key === SKILL_KEY)?.useCount === 0)

    // 应用绑定技能 + 发布
    const APP = String((await api('POST', '/api/app', { token: adminToken, body: { teamId: TID, name: '计数应用-' + TS, appType: 'agent' } })).json.value)
    const cfg = await api('PUT', `/api/app/${APP}/agent-config`, {
      token: adminToken,
      body: { prompt: '请先装载技能再回答', wikiIds: [], plugins: [], modelId: CHAT_MODEL, skills: [SKILL_ID], executionSettings: { sandbox: { enabled: false } } },
    })
    check('SU-03 应用绑定技能 200', cfg.status === 200, `${cfg.status} ${cfg.text.slice(0, 120)}`)
    check('SU-04 发布应用 200', (await api('POST', `/api/app/${APP}/publish`, { token: adminToken })).status === 200)

    // userconfig 默认技能目录回显 useCount
    const uc = await api('GET', `/api/app/${APP}/userconfig`, { token: adminToken })
    check('SU-05 默认技能目录回显 useCount=0', (uc.json?.defaultSkills ?? []).find((x) => String(x.id) === SKILL_ID)?.useCount === 0)

    // 对话触发装载
    const sess = await api('POST', `/api/app/${APP}/session`, { token: adminToken, body: { title: 'skc' } })
    const SID = String(sess.json?.value ?? '')
    const res = await fetch(`${BASE}/api/agent/${APP}/chat`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${adminToken}` },
      body: JSON.stringify({ threadId: SID, runId: crypto.randomUUID(), state: {}, messages: [{ id: crypto.randomUUID(), role: 'user', content: '请装载技能' }], tools: [], context: [], forwardedProps: {} }),
    })
    const raw = await res.text()
    const reply = raw.split('\n').filter((l) => l.startsWith('data:')).map((l) => { try { return JSON.parse(l.slice(5).trim()) } catch { return null } }).filter(Boolean)
      .filter((e) => e.type === 'TEXT_MESSAGE_CONTENT').map((e) => e.delta).join('')
    check('SU-06 对话装载技能完成', res.status === 200 && reply.includes('技能已装载完成'), `reply=${reply.slice(0, 60)}`)

    // useCount 自增 → options 与 userconfig 均为 1
    const after = await api('GET', `/api/skill/options?teamId=${TID}`, { token: adminToken })
    check('SU-07 装载后 options useCount=1', (after.json?.items ?? []).find((x) => x.key === SKILL_KEY)?.useCount === 1,
      JSON.stringify((after.json?.items ?? []).find((x) => x.key === SKILL_KEY)))
    const uc2 = await api('GET', `/api/app/${APP}/userconfig`, { token: adminToken })
    check('SU-08 默认技能目录 useCount=1', (uc2.json?.defaultSkills ?? []).find((x) => String(x.id) === SKILL_ID)?.useCount === 1)
  } finally {
    await api('DELETE', `/api/ai/channel/${CH}`, { token: adminToken })
    stub.close()
  }

  console.log(`\n===== skill-usecount E2E 完成：PASS=${PASS} FAIL=${FAIL} =====`)
  process.exit(FAIL > 0 ? 1 : 0)
}

main().catch((e) => { console.error('E2E 异常终止:', e); process.exit(1) })
