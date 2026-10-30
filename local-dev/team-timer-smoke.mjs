// 团队定时器冒烟（后端 127.0.0.1:5310，独立 Redis db15 + 桩模型，零真实依赖）
// 覆盖：建团队+Agent 应用+发布 → 定时器 CRUD（409 重名/400 非法 cron/400 未发布应用）
//       → 门禁（Member 写 403 / 读 200）→ Hangfire 下次调度时间回显 → 立即执行（成功落历史）
//       → 失败重试（桩 500 → retrying → failed，attempt=2）→ 删除定时器。
import crypto from 'node:crypto'
import http from 'node:http'

const BASE = process.argv[2] ?? 'http://127.0.0.1:5310'
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
const SUCCESS_MARK = `定时器冒烟OK-${TS}`
const FAIL_MARK = `定时器冒烟FAIL-${TS}`

/** 桩模型：输入含 FAIL_MARK 返回 500，否则回 SUCCESS_MARK + 用户消息尾部 */
function startStubServer() {
  const stub = http.createServer((req, res) => {
    let body = ''
    req.on('data', (chunk) => { body += chunk })
    req.on('end', () => {
      let payload = {}
      try { payload = JSON.parse(body || '{}') } catch { /* 空载荷 */ }
      const messages = payload.messages ?? []
      const lastUser = [...messages].reverse().find((m) => m.role === 'user')
      const userText = typeof lastUser?.content === 'string' ? lastUser.content : ''
      if (userText.includes(FAIL_MARK)) {
        res.writeHead(500, { 'Content-Type': 'application/json' })
        res.end(JSON.stringify({ error: { message: 'stub forced failure' } }))
        return
      }
      const reply = `${SUCCESS_MARK}:${userText.slice(-20)}`
      if (payload.stream) {
        res.writeHead(200, { 'Content-Type': 'text/event-stream' })
        const base = { id: 'chatcmpl-stub', object: 'chat.completion.chunk', created: Math.floor(Date.now() / 1000), model: payload.model ?? 'stub' }
        res.write('data: ' + JSON.stringify({ ...base, choices: [{ index: 0, delta: { role: 'assistant', content: reply }, finish_reason: null }] }) + '\n\n')
        res.write('data: ' + JSON.stringify({ ...base, choices: [{ index: 0, delta: {}, finish_reason: 'stop' }] }) + '\n\n')
        res.write('data: [DONE]\n\n')
        res.end()
        return
      }
      res.writeHead(200, { 'Content-Type': 'application/json' })
      res.end(JSON.stringify({
        id: 'chatcmpl-stub', object: 'chat.completion', created: Math.floor(Date.now() / 1000), model: payload.model ?? 'stub',
        choices: [{ index: 0, message: { role: 'assistant', content: reply }, finish_reason: 'stop' }],
        usage: { prompt_tokens: 10, completion_tokens: 10, total_tokens: 20 },
      }))
    })
  })
  return new Promise((resolve) => stub.listen(0, '127.0.0.1', () => resolve(stub)))
}

async function waitForTerminal(token, timerId, timeoutMs = 90000) {
  const start = Date.now()
  let last = null
  while (Date.now() - start < timeoutMs) {
    await new Promise((r) => setTimeout(r, 1500))
    const res = await api('GET', `/api/timer/${timerId}/executions?pageNo=1&pageSize=10`, { token })
    last = res.json?.items?.[0] ?? null
    if (last && ['success', 'failed'].includes(String(last.status))) return last
  }
  return last
}

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic
  const l = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
  const adminToken = l.json.accessToken
  check('TIMER-01 管理员登录', Boolean(adminToken))

  const stub = await startStubServer()
  const stubPort = stub.address().port

  // 团队 + 桩模型 + 已发布 Agent 应用
  const TID = Number((await api('POST', '/api/team', { token: adminToken, body: { name: `timer-team-${TS}` } })).json.value)
  check('TIMER-02 创建团队', TID > 0)

  const chName = `timer-stub渠道-${TS}`
  await api('POST', '/api/ai/channel', {
    token: adminToken,
    body: { providerKey: 'openai', name: chName, protocolFamily: 'openaiChatCompletions', baseUrl: `http://127.0.0.1:${stubPort}/v1`, apiKey: 'stub-key', enabled: true, description: 'timer smoke 桩渠道' },
  })
  const channels = await api('GET', '/api/ai/channel', { token: adminToken })
  const CH_ID = String((channels.json?.items ?? []).find((c) => c.name === chName)?.id ?? '')
  const modelName = `timer-stub-chat-${TS}`
  await api('POST', '/api/ai/model', {
    token: adminToken,
    body: { channelId: CH_ID, meta: { modelId: modelName, name: modelName, modelKind: 'conversation', description: 'timer smoke 桩模型' }, enabled: true, isPublic: false },
  })
  const models = await api('GET', `/api/ai/model?channelId=${CH_ID}`, { token: adminToken })
  const MODEL_ID = String((models.json?.items ?? []).find((m) => m.name === modelName)?.id ?? '')
  const cur = await api('GET', `/api/ai/model/${MODEL_ID}/authorization`, { token: adminToken })
  const teamIds = [...new Set([...(cur.json?.items ?? []).map((i) => Number(i.teamId)), TID])]
  await api('PUT', `/api/ai/model/${MODEL_ID}/authorization`, { token: adminToken, body: { modelId: MODEL_ID, teamIds } })
  check('TIMER-03 桩渠道与模型', Boolean(CH_ID) && Boolean(MODEL_ID))

  const APP = String((await api('POST', '/api/app', { token: adminToken, body: { teamId: TID, name: `定时器应用-${TS}`, appType: 'agent' } })).json.value)
  await api('PUT', `/api/app/${APP}/agent-config`, {
    token: adminToken,
    body: { prompt: '你是定时报告助手', wikiIds: [], plugins: [], modelId: MODEL_ID, executionSettings: { sandbox: { enabled: false }, memory: { enabled: false } } },
  })
  check('TIMER-04 发布应用', (await api('POST', `/api/app/${APP}/publish`, { token: adminToken })).status === 200)

  // ===== 校验类 =====
  const name = `日报定时器-${TS}`
  const bad = await api('POST', '/api/timer', { token: adminToken, body: { teamId: TID, name, appId: APP, cronExpression: 'not a cron', inputContent: 'x', isEnabled: true, maxRetryCount: 0, retryIntervalSeconds: 60 } })
  check('TIMER-05 非法 cron 400', bad.status === 400, `got ${bad.status}`)

  const APP2 = String((await api('POST', '/api/app', { token: adminToken, body: { teamId: TID, name: `未发布应用-${TS}`, appType: 'agent' } })).json.value)
  const unp = await api('POST', '/api/timer', { token: adminToken, body: { teamId: TID, name: `${name}-unpub`, appId: APP2, cronExpression: '0 1 * * *', inputContent: 'x', isEnabled: true, maxRetryCount: 0, retryIntervalSeconds: 60 } })
  check('TIMER-06 绑定未发布应用 400', unp.status === 400, `got ${unp.status}`)
  await api('DELETE', `/api/app/${APP2}`, { token: adminToken })

  // ===== 创建 + 列表 =====
  const created = await api('POST', '/api/timer', { token: adminToken, body: { teamId: TID, name, appId: APP, cronExpression: '0 1 * * *', inputContent: '生成今日经营日报', isEnabled: true, maxRetryCount: 1, retryIntervalSeconds: 10 } })
  const TIMER = String(created.json?.value ?? '')
  check('TIMER-07 创建定时器', created.status === 200 && /^[0-9a-f-]{36}$/.test(TIMER), `got ${created.status}`)

  const dup = await api('POST', '/api/timer', { token: adminToken, body: { teamId: TID, name, appId: APP, cronExpression: '0 1 * * *', inputContent: 'x', isEnabled: true, maxRetryCount: 0, retryIntervalSeconds: 60 } })
  check('TIMER-08 重名 409', dup.status === 409, `got ${dup.status}`)

  const list1 = await api('GET', `/api/timer/list?teamId=${TID}&pageNo=1&pageSize=20`, { token: adminToken })
  const item = (list1.json?.items ?? []).find((x) => String(x.timerId) === TIMER)
  check('TIMER-09 列表回显与下次调度时间', Boolean(item) && item.isEnabled === true && Boolean(item.nextRunTime) && item.appName.includes('定时器应用'), JSON.stringify(item?.nextRunTime))
  check('TIMER-09b 新建未执行时列表无执行状态', Boolean(item) && item.lastExecutionStatus == null, JSON.stringify(item?.lastExecutionStatus))

  // ===== 门禁 =====
  const memberName = `tmem${TS}`
  await api('POST', '/api/auth/register', { body: { userName: memberName, email: `${memberName}@test.local`, nickName: memberName, phone: `15${TS}9`.slice(0, 11), password: rsa('Test1234') } })
  const ml = await api('POST', '/api/auth/login', { body: { userName: memberName, password: rsa('Test1234') } })
  const memberToken = ml.json.accessToken
  const memberUserId = Number(ml.json.userId)
  await api('POST', `/api/team/${TID}/users`, { token: adminToken, body: { userId: memberUserId, role: 0 } })
  const forbidden = await api('POST', '/api/timer', { token: memberToken, body: { teamId: TID, name: `成员-${TS}`, appId: APP, cronExpression: '0 1 * * *', inputContent: 'x', isEnabled: true, maxRetryCount: 0, retryIntervalSeconds: 60 } })
  check('TIMER-10 Member 创建 403', forbidden.status === 403, `got ${forbidden.status}`)
  check('TIMER-11 Member 读列表 200', (await api('GET', `/api/timer/list?teamId=${TID}&pageNo=1&pageSize=20`, { token: memberToken })).status === 200)

  // ===== 立即执行（成功）=====
  const run1 = await api('POST', `/api/timer/${TIMER}/run`, { token: adminToken })
  const EXEC1 = String(run1.json?.value ?? '')
  check('TIMER-12 立即执行入队', run1.status === 200 && /^[0-9a-f-]{36}$/.test(EXEC1), `got ${run1.status} ${run1.text.slice(0, 200)}`)
  const done1 = await waitForTerminal(adminToken, TIMER)
  check('TIMER-13 执行成功且回答落历史', done1?.status === 'success' && String(done1.result ?? '').includes(SUCCESS_MARK) && Number(done1.attemptCount) === 1 && done1.triggerType === 'manual' && done1.triggerUserName === 'admin', JSON.stringify({ status: done1?.status, attempt: done1?.attemptCount, result: String(done1?.result ?? '').slice(0, 50) }))
  const listItem1 = ((await api('GET', `/api/timer/list?teamId=${TID}&pageNo=1&pageSize=20`, { token: adminToken })).json?.items ?? []).find((x) => String(x.timerId) === TIMER)
  check('TIMER-13b 列表回显最近执行状态 success', listItem1?.lastExecutionStatus === 'success', JSON.stringify(listItem1?.lastExecutionStatus))

  // ===== 定时执行落会话：管理员日志页可见，本人会话列表排除 =====
  const execDetail = (await api('GET', `/api/timer/${TIMER}/executions?pageNo=1&pageSize=5`, { token: adminToken })).json?.items?.[0]
  const logs = await api('GET', `/api/app/${APP}/logs?pageNo=1&pageSize=20&userType=timer`, { token: adminToken })
  const timerSession = (logs.json?.items ?? []).find((x) => String(x.sessionId) === String(execDetail?.sessionId ?? ''))
  check('TIMER-13c 执行落会话且管理员日志页可见（userType=timer）', Boolean(execDetail?.sessionId) && Boolean(timerSession), JSON.stringify({ execSession: execDetail?.sessionId, total: logs.json?.totalCount }))
  const sessionMessages = await api('GET', `/api/app/${APP}/logs/${execDetail.sessionId}/messages`, { token: adminToken })
  const msgList = sessionMessages.json?.items ?? sessionMessages.json ?? []
  const hasReply = (Array.isArray(msgList) ? msgList : []).some((m) => String(m.content ?? '').includes(SUCCESS_MARK))
  check('TIMER-13d 会话消息详情含完整回答', hasReply, `messages=${Array.isArray(msgList) ? msgList.length : -1}`)
  const mySessions = await api('GET', `/api/app/${APP}/session/list`, { token: adminToken })
  const myList = mySessions.json?.items ?? (Array.isArray(mySessions.json) ? mySessions.json : [])
  const leaked = myList.some((x) => String(x.id ?? x.sessionId) === String(execDetail.sessionId))
  check('TIMER-13e 本人会话列表不出现定时任务会话', !leaked, `sessions=${myList.length}`)
  check('TIMER-14 手动触发刷新最近触发时间', Boolean(done1) && new Date(item.lastRunTime ?? 0).getTime() <= new Date(done1.createTime ?? Date.now()).getTime())

  // ===== 失败重试 =====
  const put1 = await api('PUT', `/api/timer/${TIMER}`, { token: adminToken, body: { name, appId: APP, cronExpression: '0 1 * * *', inputContent: FAIL_MARK, isEnabled: true, maxRetryCount: 1, retryIntervalSeconds: 10 } })
  check('TIMER-15a 更新定时器 200', put1.status === 200, `got ${put1.status} ${put1.text.slice(0, 120)}`)
  await api('POST', `/api/timer/${TIMER}/run`, { token: adminToken })
  const done2 = await waitForTerminal(adminToken, TIMER, 120000)
  check('TIMER-15 失败重试后终态 failed 且尝试 2 次', done2?.status === 'failed' && Number(done2.attemptCount) === 2 && Boolean(done2.errorMessage), JSON.stringify({ status: done2?.status, attempt: done2?.attemptCount, err: String(done2?.errorMessage ?? '').slice(0, 60) }))

  const list2 = await api('GET', `/api/timer/${TIMER}/executions?pageNo=1&pageSize=10`, { token: adminToken })
  check('TIMER-16 执行历史默认 10 条倒序', list2.json?.totalCount === 2 && String(list2.json?.items?.[0]?.executionId) === String(done2.executionId), `total=${list2.json?.totalCount}`)
  const itemAfterFail = ((await api('GET', `/api/timer/list?teamId=${TID}&pageNo=1&pageSize=20`, { token: adminToken })).json?.items ?? []).find((x) => String(x.timerId) === TIMER)
  check('TIMER-17 列表最近触发时间已刷新', Boolean(itemAfterFail?.lastRunTime))
  check('TIMER-17b 重试终态后列表执行状态 failed', itemAfterFail?.lastExecutionStatus === 'failed', JSON.stringify(itemAfterFail?.lastExecutionStatus))

  // ===== 停用 → 移除调度 =====
  await api('PUT', `/api/timer/${TIMER}`, { token: adminToken, body: { name, appId: APP, cronExpression: '0 1 * * *', inputContent: '生成今日经营日报', isEnabled: false, maxRetryCount: 0, retryIntervalSeconds: 60 } })
  const item2 = ((await api('GET', `/api/timer/list?teamId=${TID}&pageNo=1&pageSize=20`, { token: adminToken })).json?.items ?? []).find((x) => String(x.timerId) === TIMER)
  check('TIMER-18 停用后无下次调度时间', item2?.isEnabled === false && !item2?.nextRunTime, JSON.stringify(item2?.nextRunTime))

  // ===== 删除 =====
  check('TIMER-19 删除定时器 200', (await api('DELETE', `/api/timer/${TIMER}`, { token: adminToken })).status === 200)
  const list3 = await api('GET', `/api/timer/list?teamId=${TID}&pageNo=1&pageSize=20`, { token: adminToken })
  check('TIMER-20 删除后列表为空', (list3.json?.items ?? []).length === 0, `items=${(list3.json?.items ?? []).length}`)
  check('TIMER-21 删除后执行历史保留', (await api('GET', `/api/timer/${TIMER}/executions?pageNo=1&pageSize=10`, { token: adminToken })).json?.totalCount === 2)

  // ===== 清理 =====
  await api('DELETE', `/api/app/${APP}`, { token: adminToken })
  await api('DELETE', `/api/ai/channel/${CH_ID}`, { token: adminToken })
  stub.close()

  console.log(`\n===== 结果：PASS ${PASS} / FAIL ${FAIL} =====`)
  process.exit(FAIL > 0 ? 1 : 0)
}

main().catch((e) => {
  console.error('E2E 异常:', e)
  process.exit(1)
})
