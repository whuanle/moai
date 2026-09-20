// 应用管理 E2E（场景 @AP-Sn）
// 用法：node local-dev/app-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5000，可用 APP_BASE 覆盖）
// 前置：后端运行中，且已执行 asserts/app.sql 建表。
import crypto from 'node:crypto'
import http from 'node:http'

const BASE = process.env.APP_BASE ?? process.argv[2] ?? 'http://127.0.0.1:5000'
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
let seq = 0
const uname = (p) => `${p}${TS}${String(seq++).padStart(2, '0')}`
const phone = () => `15${Date.now().toString().slice(-8)}${String(seq++).padStart(2, '0')}`.slice(0, 11)

async function mkuser(p) {
  const name = uname(p)
  const r = await api('POST', '/api/auth/register', { body: { userName: name, email: `${name}@test.local`, nickName: name, phone: phone(), password: rsa('Test1234') } })
  if (r.status !== 200) throw new Error(`注册 ${name} 失败: ${r.status} ${r.text.slice(0, 120)}`)
  const l = await api('POST', '/api/auth/login', { body: { userName: name, password: rsa('Test1234') } })
  return { name, userId: Number(l.json.userId), token: l.json.accessToken }
}

const isGuid = (v) => typeof v === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(v)

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const owner = await mkuser('ao')
  const member = await mkuser('am')
  const outsider = await mkuser('ax')

  const TID = Number((await api('POST', '/api/team', { token: owner.token, body: { name: 'app-team-' + TS } })).json.value)
  // 0=Member 1=Admin 2=Owner（TeamRole 枚举）
  await api('POST', `/api/team/${TID}/users`, { token: owner.token, body: { userId: member.userId, role: 0 } })

  // AP-01 未登录
  check('AP-01 未登录查应用列表 401', (await api('GET', `/api/app/list?teamId=${TID}`)).status === 401)

  // AP-02 校验
  check('AP-02a 空名称 400', (await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '', appType: 'agent' } })).status === 400)
  check('AP-02b 名称超 20 字 400', (await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: 'x'.repeat(21), appType: 'agent' } })).status === 400)
  check('AP-02c 非法应用类型 400', (await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: 'bad-type', appType: 'unknown' } })).status === 400)
  check('AP-02d 非法 teamId 400', (await api('POST', '/api/app', { token: owner.token, body: { teamId: 0, name: 'bad-team', appType: 'agent' } })).status === 400)

  // AP-03 权限：应用是团队下的产物，仅团队管理员可创建
  check('AP-03a Member 创建 403', (await api('POST', '/api/app', { token: member.token, body: { teamId: TID, name: 'member-app', appType: 'agent' } })).status === 403)
  check('AP-03b 非成员创建 404', (await api('POST', '/api/app', { token: outsider.token, body: { teamId: TID, name: 'outsider-app', appType: 'agent' } })).status === 404)
  check('AP-03c 非成员查列表 404', (await api('GET', `/api/app/list?teamId=${TID}`, { token: outsider.token })).status === 404)

  // AP-04 创建 Agent 应用
  const c1 = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '客服助手', description: '售前售后问答', appType: 'agent' } })
  check('AP-04 Agent 应用创建 200 且返回 Guid', c1.status === 200 && isGuid(c1.json?.value), `${c1.status} ${c1.text.slice(0, 140)}`)
  const AGENT_ID = String(c1.json?.value ?? '')

  // AP-05 创建流程应用
  const c2 = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '审批流', appType: 'workflow' } })
  check('AP-05 流程应用创建 200', c2.status === 200 && isGuid(c2.json?.value), `${c2.status} ${c2.text.slice(0, 140)}`)
  const WORKFLOW_ID = String(c2.json?.value ?? '')

  // AP-06 团队内名称唯一
  check('AP-06 同团队重名 409', (await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '客服助手', appType: 'agent' } })).status === 409)

  // AP-40 调试会话：未发布也能创建、不落库、不计用量
  {
    const dbgOutsider = await api('POST', `/api/app/${AGENT_ID}/debug/session`, { token: outsider.token })
    check('AP-40a 非成员创建调试会话 404', dbgOutsider.status === 404, `${dbgOutsider.status}`)

    const dbgMember = await api('POST', `/api/app/${AGENT_ID}/debug/session`, { token: member.token })
    check('AP-40b Member 创建调试会话 403', dbgMember.status === 403, `${dbgMember.status}`)

    const dbgFlow = await api('POST', `/api/app/${WORKFLOW_ID}/debug/session`, { token: owner.token })
    check('AP-40c 流程应用创建调试会话 400', dbgFlow.status === 400, `${dbgFlow.status}`)

    const dbg = await api('POST', `/api/app/${AGENT_ID}/debug/session`, { token: owner.token })
    check('AP-40d Owner 创建调试会话 200 且返回 Guid', dbg.status === 200 && isGuid(dbg.json?.value), `${dbg.status} ${dbg.text.slice(0, 140)}`)

    const debugSessionId = String(dbg.json?.value ?? '')
    const after = await api('GET', `/api/app/${AGENT_ID}/session/list`, { token: owner.token })
    check('AP-40e 调试会话不进入正式会话列表', (after.json?.items ?? []).every((s) => String(s.sessionId) !== debugSessionId),
      `after=${(after.json?.items ?? []).length}`)

    const dbgNotFound = await api('POST', '/api/app/01924f5e-0000-7000-8000-00000000ffff/debug/session', { token: owner.token })
    check('AP-40f 不存在应用调试会话 404', dbgNotFound.status === 404, `${dbgNotFound.status}`)
  }

  // AP-42 对话日志：分页列表 + 过滤 + 权限 + 消息详情
  {
    const sess = await api('POST', `/api/app/${AGENT_ID}/session`, { token: owner.token, body: { title: 'log-e2e' } })
    check('AP-42a 创建正式会话 200 且返回 Guid', sess.status === 200 && isGuid(sess.json?.value), `${sess.status} ${sess.text.slice(0, 140)}`)
    const LOG_SESSION_ID = String(sess.json?.value ?? '')

    const list = await api('GET', `/api/app/${AGENT_ID}/logs?pageNo=1&pageSize=20`, { token: owner.token })
    const items = list.json?.items ?? []
    check('AP-42b 管理员查日志 200 且含该会话', list.status === 200 && items.some((x) => String(x.sessionId) === LOG_SESSION_ID), `${list.status} ${list.text.slice(0, 160)}`)
    check('AP-42c 日志返回分页字段', typeof list.json?.total === 'number' && list.json?.pageNo === 1 && list.json?.pageSize === 20, JSON.stringify({ total: list.json?.total, pageNo: list.json?.pageNo, pageSize: list.json?.pageSize }))

    const kw = await api('GET', `/api/app/${AGENT_ID}/logs?keyword=${encodeURIComponent('log-e2e')}`, { token: owner.token })
    check('AP-42d 标题关键字命中', (kw.json?.items ?? []).some((x) => String(x.sessionId) === LOG_SESSION_ID), kw.text.slice(0, 160))
    const kwMiss = await api('GET', `/api/app/${AGENT_ID}/logs?keyword=${encodeURIComponent('no-such-title-zzz')}`, { token: owner.token })
    check('AP-42e 标题关键字不命中返回空', (kwMiss.json?.items ?? []).length === 0, kwMiss.text.slice(0, 160))

    const normal = await api('GET', `/api/app/${AGENT_ID}/logs?userType=normal`, { token: owner.token })
    check('AP-42f 按内部用户过滤命中', (normal.json?.items ?? []).some((x) => String(x.sessionId) === LOG_SESSION_ID), normal.text.slice(0, 160))
    const external = await api('GET', `/api/app/${AGENT_ID}/logs?userType=external`, { token: owner.token })
    check('AP-42g 按外部用户过滤不含内部会话', !(external.json?.items ?? []).some((x) => String(x.sessionId) === LOG_SESSION_ID), external.text.slice(0, 160))

    const msgs = await api('GET', `/api/app/${AGENT_ID}/logs/${LOG_SESSION_ID}/messages`, { token: owner.token })
    check('AP-42h 会话消息详情 200', msgs.status === 200 && Array.isArray(msgs.json?.items), `${msgs.status} ${msgs.text.slice(0, 160)}`)
    check('AP-42i 跨应用取消息 404', (await api('GET', `/api/app/${WORKFLOW_ID}/logs/${LOG_SESSION_ID}/messages`, { token: owner.token })).status === 404)
    check('AP-42j 不存在会话消息 404', (await api('GET', `/api/app/${AGENT_ID}/logs/01924f5e-0000-7000-8000-00000000ffff/messages`, { token: owner.token })).status === 404)

    check('AP-42k Member 查日志 403', (await api('GET', `/api/app/${AGENT_ID}/logs`, { token: member.token })).status === 403)
    check('AP-42l 非成员查日志 404', (await api('GET', `/api/app/${AGENT_ID}/logs`, { token: outsider.token })).status === 404)
    check('AP-42m 非成员查日志消息 404', (await api('GET', `/api/app/${AGENT_ID}/logs/${LOG_SESSION_ID}/messages`, { token: outsider.token })).status === 404)
    check('AP-42n 不存在应用日志 404', (await api('GET', '/api/app/01924f5e-0000-7000-8000-00000000ffff/logs', { token: owner.token })).status === 404)
  }

  // AP-43 应用监控：用量汇总 + 按模型分布 + 权限
  {
    const usage = await api('GET', `/api/app/${AGENT_ID}/usage`, { token: owner.token })
    check('AP-43a 管理员查用量 200 且含 summary/byModel', usage.status === 200 && usage.json?.summary != null && Array.isArray(usage.json?.byModel), `${usage.status} ${usage.text.slice(0, 160)}`)
    check('AP-43b 汇总含调用次数与合计 token 字段', usage.json?.summary?.callCount !== undefined && usage.json?.summary?.totalTokens !== undefined, JSON.stringify(usage.json?.summary))
    check('AP-43c Member 查用量 403', (await api('GET', `/api/app/${AGENT_ID}/usage`, { token: member.token })).status === 403)
    check('AP-43d 非成员查用量 404', (await api('GET', `/api/app/${AGENT_ID}/usage`, { token: outsider.token })).status === 404)
    check('AP-43e 不存在应用用量 404', (await api('GET', '/api/app/01924f5e-0000-7000-8000-00000000ffff/usage', { token: owner.token })).status === 404)
  }

  // AP-07 列表（Member 可见 + myRole）
  {
    const r = await api('GET', `/api/app/list?teamId=${TID}`, { token: member.token })
    const items = r.json?.items ?? []
    const agent = items.find(i => i.name === '客服助手')
    const flow = items.find(i => i.name === '审批流')
    check('AP-07a Member 查列表 200 且 myRole=0', r.status === 200 && r.json?.myRole === 0, `${r.status} ${r.text.slice(0, 120)}`)
    check('AP-07b 返回 2 条且类型正确', items.length === 2 && agent?.appType === 'agent' && flow?.appType === 'workflow', JSON.stringify(items))
    check('AP-07c 列表按创建时间倒序（流程应用在前）', items[0]?.name === '审批流', JSON.stringify(items.map(i => i.name)))
  }

  // AP-08 详情
  {
    const r = await api('GET', `/api/app/${AGENT_ID}`, { token: member.token })
    const d = r.json ?? {}
    check('AP-08a 详情 200 且字段齐全', r.status === 200 && d.name === '客服助手' && d.appType === 'agent' && Number(d.teamId) === TID && !!d.createTime && typeof d.isPublic === 'boolean' && d.isExternal === false,
      `${r.status} name=${d.name} appType=${d.appType} teamId=${d.teamId}(${typeof d.teamId}) TID=${TID} createTime=${d.createTime} isPublic=${d.isPublic}(${typeof d.isPublic}) isExternal=${d.isExternal}`)
    check('AP-08b 非成员查详情 404', (await api('GET', `/api/app/${AGENT_ID}`, { token: outsider.token })).status === 404)
    check('AP-08c 不存在的应用详情 404', (await api('GET', '/api/app/01924f5e-0000-7000-8000-00000000ffff', { token: owner.token })).status === 404)
  }

  // AP-09 更新基础信息
  check('AP-09a Member 更新 403', (await api('PUT', `/api/app/${AGENT_ID}`, { token: member.token, body: { name: 'member-edit' } })).status === 403)
  check('AP-09b Admin 更新 200', (await api('PUT', `/api/app/${AGENT_ID}`, { token: owner.token, body: { name: '智能客服', description: '改名后' } })).status === 200)
  {
    const r = await api('GET', `/api/app/${AGENT_ID}`, { token: owner.token })
    check('AP-09c 更新后名称/描述生效', r.json?.name === '智能客服' && r.json?.description === '改名后', r.text.slice(0, 140))
    check('AP-09d 应用类型不可修改（服务端忽略 appType）', r.json?.appType === 'agent', String(r.json?.appType))
  }
  check('AP-09e 更新撞团队内已有名称 409', (await api('PUT', `/api/app/${AGENT_ID}`, { token: owner.token, body: { name: '审批流' } })).status === 409)

  // AP-10 头像（存储全链路）
  {
    const payload = Buffer.from('app-avatar-bytes-' + Date.now())
    const sha = crypto.createHash('sha256').update(payload).digest('hex')
    const pre = await api('POST', '/api/storage/public/pre_upload_image', { token: owner.token, body: { fileName: 'appavatar.png', contentType: 'image/png', fileSize: payload.length, shA256: sha } })
    check('AP-10a 预上传 200', pre.status === 200, `${pre.status}`)
    await fetch(pre.json.uploadUrl, { method: 'PUT', headers: { 'Content-Type': 'image/png' }, body: payload })
    const comp = await api('POST', '/api/storage/complate_url', { token: owner.token, body: { isSuccess: true, fileId: pre.json.fileId } })
    check('AP-10b 伪造 objectKey 404', (await api('POST', `/api/app/${AGENT_ID}/avatar`, { token: owner.token, body: { objectKey: 'public/fake-app-key.png' } })).status === 404)
    check('AP-10c Member 设头像 403', (await api('POST', `/api/app/${AGENT_ID}/avatar`, { token: member.token, body: { objectKey: comp.json.objectKey } })).status === 403)
    check('AP-10d 设置头像 200', (await api('POST', `/api/app/${AGENT_ID}/avatar`, { token: owner.token, body: { objectKey: comp.json.objectKey } })).status === 200)
    const d = await api('GET', `/api/app/${AGENT_ID}`, { token: owner.token })
    check('AP-10e 详情回填头像 objectKey', d.json?.avatarPath === comp.json.objectKey, `${d.json?.avatarPath} vs ${comp.json.objectKey}`)
  }

  // AP-11 不存在的团队
  check('AP-11 不存在的团队查列表 404', (await api('GET', '/api/app/list?teamId=99999999', { token: owner.token })).status === 404)

  // AP-12 创建时可设置头像 + AP-13 允许外部使用开关
  {
    const payload = Buffer.from('app-create-avatar-' + Date.now())
    const sha = crypto.createHash('sha256').update(payload).digest('hex')
    const pre = await api('POST', '/api/storage/public/pre_upload_image', { token: owner.token, body: { fileName: 'create-avatar.png', contentType: 'image/png', fileSize: payload.length, shA256: sha } })
    await fetch(pre.json.uploadUrl, { method: 'PUT', headers: { 'Content-Type': 'image/png' }, body: payload })
    const comp = await api('POST', '/api/storage/complate_url', { token: owner.token, body: { isSuccess: true, fileId: pre.json.fileId } })

    const c = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '带头像应用', appType: 'agent', avatar: comp.json.objectKey } })
    check('AP-12a 创建时带头像 200', c.status === 200 && isGuid(c.json?.value), `${c.status} ${c.text.slice(0, 140)}`)
    const NEW_ID = String(c.json?.value ?? '')

    const d = await api('GET', `/api/app/${NEW_ID}`, { token: owner.token })
    check('AP-12b 详情回填创建时提交的头像', d.json?.avatarPath === comp.json.objectKey, `${d.json?.avatarPath} vs ${comp.json.objectKey}`)

    // is_public 只能通过上架审核（/api/publication）由管理员审批设置，创建/更新接口不再提供该字段
    check('AP-13a 创建后详情 isPublic=false（无直接公开通道）', d.json?.isPublic === false, String(d.json?.isPublic))
    const list = await api('GET', `/api/app/list?teamId=${TID}`, { token: owner.token })
    const created = (list.json?.items ?? []).find(i => i.name === '带头像应用')
    check('AP-13b 列表返回 isPublic', created?.isPublic === false, JSON.stringify(created))

    check('AP-13c 更新基础信息 200（更新接口不再接受 isPublic）', (await api('PUT', `/api/app/${NEW_ID}`, { token: owner.token, body: { name: '带头像应用' } })).status === 200)
    const d2 = await api('GET', `/api/app/${NEW_ID}`, { token: owner.token })
    check('AP-13d 更新后 isPublic 仍为 false', d2.json?.isPublic === false, String(d2.json?.isPublic))

    const fake = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '伪造头像应用', appType: 'agent', avatar: 'public/fake-create-avatar.png' } })
    check('AP-14 创建时伪造 objectKey 404', fake.status === 404, `${fake.status} ${fake.text.slice(0, 120)}`)
  }

  // AP-13e ~ AP-13l 外部应用（is_external / is_auth）
  {
    const ext = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '对外助手', appType: 'agent', isExternal: true, isAuth: true } })
    check('AP-13e 管理员创建外部应用 200', ext.status === 200 && isGuid(ext.json?.value), `${ext.status} ${ext.text.slice(0, 140)}`)
    const EXT_ID = String(ext.json?.value ?? '')

    const ed = await api('GET', `/api/app/${EXT_ID}`, { token: owner.token })
    check('AP-13f 外部应用详情 isExternal/isAuth 正确', ed.json?.isExternal === true && ed.json?.isAuth === true, JSON.stringify(ed.json))

    const internalList = await api('GET', `/api/app/list?teamId=${TID}`, { token: owner.token })
    check('AP-13g 内部应用列表不包含外部应用', !(internalList.json?.items ?? []).some(i => i.appId === EXT_ID), JSON.stringify((internalList.json?.items ?? []).map(i => i.name)))

    const extList = await api('GET', `/api/app/external/list?teamId=${TID}`, { token: owner.token })
    check('AP-13h 外部应用列表包含新建外部应用', extList.status === 200 && (extList.json?.items ?? []).some(i => i.appId === EXT_ID), `${extList.status} ${extList.text.slice(0, 140)}`)
    check('AP-13i 普通成员查外部列表 403', (await api('GET', `/api/app/external/list?teamId=${TID}`, { token: member.token })).status === 403)
    check('AP-13j 非成员查外部列表 404', (await api('GET', `/api/app/external/list?teamId=${TID}`, { token: outsider.token })).status === 404)
    check('AP-13k 内部应用设置 isAuth 被拒 400', (await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '非法内部应用', appType: 'agent', isAuth: true } })).status === 400)

    // AP-13s ~ AP-13y 应用接入（access_app）
    const createAcc = await api('POST', '/api/access-app', { token: owner.token, body: { teamId: TID, name: 'ERP接入', description: '接入测试' } })
    check('AP-13s 创建应用接入 200 且返回 key', createAcc.status === 200 && isGuid(createAcc.json?.accessAppId) && typeof createAcc.json?.key === 'string' && createAcc.json.key.startsWith('moai-ac-'), `${createAcc.status} ${createAcc.text.slice(0, 140)}`)
    const ACC_ID = String(createAcc.json?.accessAppId ?? '')

    const accList = await api('GET', `/api/access-app/list?teamId=${TID}`, { token: owner.token })
    const accItem = (accList.json?.items ?? []).find(i => i.accessAppId === ACC_ID)
    check('AP-13t 接入列表回显完整 key（可再次查看）', accList.status === 200 && !!accItem && accItem.key === createAcc.json?.key, JSON.stringify(accItem))
    check('AP-13u Member 查接入 403', (await api('GET', `/api/access-app/list?teamId=${TID}`, { token: member.token })).status === 403)
    check('AP-13v 非成员查接入 404', (await api('GET', `/api/access-app/list?teamId=${TID}`, { token: outsider.token })).status === 404)
    check('AP-13x 更新接入 200', (await api('PUT', `/api/access-app/${ACC_ID}`, { token: owner.token, body: { name: 'ERP接入2' } })).status === 200)
    check('AP-13y 删除接入 200', (await api('DELETE', `/api/access-app/${ACC_ID}`, { token: owner.token })).status === 200)
  }

  // AP-13m ~ AP-13r 平台公开应用（is_public 只能通过上架审核由管理员审批设置）
  {
    const pubAdminLogin = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
    const pubAdminToken = pubAdminLogin.json?.accessToken

    const c = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '公开助手', appType: 'agent' } })
    check('AP-13m 创建内部应用 200', c.status === 200 && isGuid(c.json?.value), `${c.status} ${c.text.slice(0, 140)}`)
    const PUB_ID = String(c.json?.value ?? '')
    check('AP-13n 非成员详情未发布 404', (await api('GET', `/api/app/${PUB_ID}`, { token: outsider.token })).status === 404)

    check('AP-13o 发布应用 200', (await api('POST', `/api/app/${PUB_ID}/publish`, { token: owner.token })).status === 200)

    if (pubAdminToken) {
      const apply = await api('POST', '/api/publication/apply', { token: owner.token, body: { resourceType: 'app', resourceId: PUB_ID, applyReason: 'e2e 申请上架' } })
      check('AP-13o2 团队申请上架 200', apply.status === 200, `${apply.status} ${apply.text.slice(0, 140)}`)
      const review = await api('POST', '/api/publication/review', { token: pubAdminToken, body: { publicationId: apply.json?.value, isApprove: true } })
      check('AP-13o3 管理员审批通过 200', review.status === 200, `${review.status} ${review.text.slice(0, 140)}`)
    } else {
      console.log('SKIP | AP-13o2/o3 无 admin 账号（admin/abcd123456），无法走上架审核')
    }

    const od = await api('GET', `/api/app/${PUB_ID}`, { token: outsider.token })
    check('AP-13p 非成员可看已发布公开应用详情', od.status === 200 && od.json?.isPublic === true && od.json?.myRole === -1, `${od.status} ${od.text.slice(0, 140)}`)

    const pub = await api('GET', '/api/app/public/list', { token: outsider.token })
    check('AP-13q 公开应用列表包含该应用', pub.status === 200 && (pub.json?.items ?? []).some(i => i.appId === PUB_ID), `${pub.status} ${pub.text.slice(0, 140)}`)

    const sess = await api('POST', `/api/app/${PUB_ID}/session`, { token: outsider.token, body: {} })
    check('AP-13r 非成员可对公开已发布应用建会话', sess.status === 200 && isGuid(sess.json?.value), `${sess.status} ${sess.text.slice(0, 140)}`)
  }

  // AP-15 ~ AP-19 Agent 应用配置（允许使用的插件/知识库 + 提示词）
  {
    const cfgUrl = `/api/app/${AGENT_ID}/agent-config`

    check('AP-15a 未登录查配置 401', (await api('GET', cfgUrl)).status === 401)
    check('AP-15b 非成员查配置 404', (await api('GET', cfgUrl, { token: outsider.token })).status === 404)
    {
      const r = await api('GET', cfgUrl, { token: member.token })
      check('AP-15c Member 可读配置 200 且为新应用空配置',
        r.status === 200 && r.json?.prompt === '' && (r.json?.wikiIds ?? []).length === 0 && (r.json?.plugins ?? []).length === 0 && r.json?.myRole === 0,
        `${r.status} ${r.text.slice(0, 160)}`)
    }

    // 绑定范围：本团队知识库 + 团队可访问插件
    const wk = await api('POST', '/api/wiki', { token: owner.token, body: { teamId: TID, name: '配置用知识库' } })
    const WID = Number(wk.json?.value ?? 0)
    check('AP-16a 创建团队知识库 200', wk.status === 200 && WID > 0, `${wk.status} ${wk.text.slice(0, 120)}`)

    const pl = await api('GET', `/api/team/${TID}/plugin/list`, { token: owner.token })
    const bindable = (pl.json?.items ?? []).find(i => isGuid(i.pluginId) && i.pluginId !== '00000000-0000-0000-0000-000000000000')

    check('AP-16b Member 保存配置 403', (await api('PUT', cfgUrl, { token: member.token, body: { prompt: 'x', wikiIds: [], plugins: [] } })).status === 403)

    const plugins = bindable ? [String(bindable.pluginId)] : []
    const save = await api('PUT', cfgUrl, { token: owner.token, body: { prompt: '你是售前客服助手', wikiIds: [WID], plugins } })
    check('AP-16c Owner 保存配置 200', save.status === 200, `${save.status} ${save.text.slice(0, 140)}`)

    const back = await api('GET', cfgUrl, { token: owner.token })
    check('AP-16d 保存后回读一致（提示词 + 知识库）',
      back.json?.prompt === '你是售前客服助手' && (back.json?.wikiIds ?? []).map(Number).includes(WID),
      back.text.slice(0, 200))
    if (bindable) {
      check('AP-16e 可绑定团队可访问插件', (back.json?.plugins ?? []).map(String).includes(String(bindable.pluginId)), JSON.stringify(back.json?.plugins))
    }

    // 越权/非法绑定一律 400
    check('AP-17a 绑定非本团队知识库 400', (await api('PUT', cfgUrl, { token: owner.token, body: { prompt: '', wikiIds: [999999999], plugins: [] } })).status === 400)
    check('AP-17b 绑定无权限插件 400', (await api('PUT', cfgUrl, { token: owner.token, body: { prompt: '', wikiIds: [], plugins: ['01924f5e-0000-7000-8000-00000000aaaa'] } })).status === 400)
    check('AP-17c 提示词超 4000 字 400', (await api('PUT', cfgUrl, { token: owner.token, body: { prompt: 'x'.repeat(4001), wikiIds: [], plugins: [] } })).status === 400)

    const after = await api('GET', cfgUrl, { token: owner.token })
    check('AP-17d 校验失败不写入配置', after.json?.prompt === '你是售前客服助手', String(after.json?.prompt))

    // 流程应用仅经此入口维护对话开场白（模型/知识库/插件不适用不落库，见 workflow-e2e WF-15）
    check('AP-18 流程应用保存配置 200（只写开场白字段）', (await api('PUT', `/api/app/${WORKFLOW_ID}/agent-config`, { token: owner.token, body: { prompt: '', wikiIds: [], plugins: [] } })).status === 200)
    check('AP-19a 不存在的应用查配置 404', (await api('GET', '/api/app/01924f5e-0000-7000-8000-00000000ffff/agent-config', { token: owner.token })).status === 404)
    check('AP-19b 不存在的应用存配置 404', (await api('PUT', '/api/app/01924f5e-0000-7000-8000-00000000ffff/agent-config', { token: owner.token, body: { prompt: '', wikiIds: [], plugins: [] } })).status === 404)

    // AP-20 对话模型：取值必须落在该团队可用的模型列表内（= 公开模型 + 已授权给该团队的私有模型）
    // 本地开发库不一定有公开模型，这里用 root 管理员把一个私有模型授权给本次 E2E 团队，制造可用取值；
    // 拿不到管理员账号时跳过「绑定可用模型」用例，仍校验「无权模型一律 400」这条不变量。
    const adminLogin = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
    const adminToken = adminLogin.json?.accessToken
    let usableModelId = ''
    if (adminToken) {
      const all = await api('GET', '/api/ai/model', { token: adminToken })
      const priv = (all.json?.items ?? []).find((m) => !m.isPublic)
      if (priv) {
        const cur = await api('GET', `/api/ai/model/${priv.id}/authorization`, { token: adminToken })
        const teamIds = [...new Set([...(cur.json?.items ?? []).map((i) => Number(i.teamId)), TID])]
        const grant = await api('PUT', `/api/ai/model/${priv.id}/authorization`, { token: adminToken, body: { teamIds } })
        if (grant.status === 200) usableModelId = String(priv.id)
      }
    } else {
      console.log('SKIP | AP-20 无 admin 账号（admin/abcd123456），仅校验无权模型被拒')
    }

    const gm = await api('GET', `/api/team/${TID}/gateway/models`, { token: owner.token })
    check('AP-20a 团队可用模型列表与授权一致',
      gm.status === 200 && (usableModelId ? (gm.json?.items ?? []).map((i) => String(i.aiModelId)).includes(usableModelId) : (gm.json?.items ?? []).length === 0),
      `${gm.status} ${gm.text.slice(0, 160)}`)

    if (usableModelId) {
      const saveModel = await api('PUT', cfgUrl, { token: owner.token, body: { modelId: usableModelId, prompt: '你是售前客服助手', wikiIds: [WID], plugins } })
      check('AP-20b Owner 绑定团队可用模型 200', saveModel.status === 200, `${saveModel.status} ${saveModel.text.slice(0, 140)}`)

      const withModel = await api('GET', cfgUrl, { token: owner.token })
      check('AP-20c 回读 modelId 一致', String(withModel.json?.modelId) === usableModelId, String(withModel.json?.modelId))
    }

    check('AP-20d 绑定团队无权模型 400', (await api('PUT', cfgUrl, { token: owner.token, body: { modelId: '01924f5e-0000-7000-8000-00000000bbbb', prompt: 'x', wikiIds: [], plugins: [] } })).status === 400)
    const afterModel = await api('GET', cfgUrl, { token: owner.token })
    check('AP-20e 模型校验失败不写入',
      String(afterModel.json?.modelId) === (usableModelId || '00000000-0000-0000-0000-000000000000'),
      String(afterModel.json?.modelId))
  }

  // AP-44 会话专家提示词：创建/绑定/清除/权限
  {
    check('AP-44a 未登录设置会话提示词 401', (await api('PUT', `/api/app/session/01924f5e-0000-7000-8000-00000000ffff/prompt`, { body: { promptId: 0 } })).status === 401)

    const pPersonal = await api('POST', '/api/prompt', { token: owner.token, body: { teamId: 0, name: 'ap44-个人-' + TS, description: 'e2e', content: '你是严谨的审校专家' } })
    const pTeam = await api('POST', '/api/prompt', { token: owner.token, body: { teamId: TID, name: 'ap44-团队-' + TS, description: 'e2e', content: '你是售前客服专家' } })
    const outsiderPrompt = await api('POST', '/api/prompt', { token: outsider.token, body: { teamId: 0, name: 'ap44-外部-' + TS, description: 'e2e', content: 'x' } })
    check('AP-44b 前置：个人/团队/外部提示词创建 200', pPersonal.status === 200 && pTeam.status === 200 && outsiderPrompt.status === 200, `${pPersonal.status}/${pTeam.status}/${outsiderPrompt.status}`)
    const PERSONAL_ID = Number(pPersonal.json?.value)
    const TEAM_ID = Number(pTeam.json?.value)
    const OUTSIDER_ID = Number(outsiderPrompt.json?.value)

    // 创建会话时直接绑定团队提示词
    const withPrompt = await api('POST', `/api/app/${AGENT_ID}/session`, { token: owner.token, body: { title: 'ap44-with-prompt', promptId: TEAM_ID } })
    check('AP-44c 创建会话绑定团队提示词 200', withPrompt.status === 200 && isGuid(withPrompt.json?.value), `${withPrompt.status} ${withPrompt.text.slice(0, 120)}`)
    const EP_SESSION = String(withPrompt.json?.value ?? '')
    const epList = await api('GET', `/api/app/${AGENT_ID}/session/list`, { token: owner.token })
    check('AP-44d 会话列表回读 promptId', (epList.json?.items ?? []).some((s) => String(s.sessionId) === EP_SESSION && Number(s.promptId) === TEAM_ID), epList.text.slice(0, 160))

    // 绑定他人个人提示词 404；不存在提示词 404；负数 400
    check('AP-44e 绑定他人个人提示词 404', (await api('PUT', `/api/app/session/${EP_SESSION}/prompt`, { token: owner.token, body: { promptId: OUTSIDER_ID } })).status === 404)
    check('AP-44f 绑定不存在提示词 404', (await api('PUT', `/api/app/session/${EP_SESSION}/prompt`, { token: owner.token, body: { promptId: 99999999 } })).status === 404)
    check('AP-44g 负数 promptId 400', (await api('PUT', `/api/app/session/${EP_SESSION}/prompt`, { token: owner.token, body: { promptId: -1 } })).status === 400)

    // 非归属用户改他人会话 404
    check('AP-44h 非归属用户改他人会话 404', (await api('PUT', `/api/app/session/${EP_SESSION}/prompt`, { token: member.token, body: { promptId: TEAM_ID } })).status === 404)

    // 已有会话改绑个人提示词后清除
    const rebound = await api('PUT', `/api/app/session/${EP_SESSION}/prompt`, { token: owner.token, body: { promptId: PERSONAL_ID } })
    const reboundList = await api('GET', `/api/app/${AGENT_ID}/session/list`, { token: owner.token })
    check('AP-44i 改绑个人提示词 200 且回读一致',
      rebound.status === 200 && (reboundList.json?.items ?? []).some((s) => String(s.sessionId) === EP_SESSION && Number(s.promptId) === PERSONAL_ID),
      `${rebound.status}`)

    // 绑定不可用的提示词时创建即失败，不产生会话行
    const beforeCnt = ((await api('GET', `/api/app/${AGENT_ID}/session/list`, { token: owner.token })).json?.items ?? []).length
    const badCreate = await api('POST', `/api/app/${AGENT_ID}/session`, { token: owner.token, body: { title: 'ap44-bad', promptId: 99999999 } })
    const afterCnt = ((await api('GET', `/api/app/${AGENT_ID}/session/list`, { token: owner.token })).json?.items ?? []).length
    check('AP-44j 创建会话绑定不可用提示词 404 且不落库',
      badCreate.status === 404 && beforeCnt === afterCnt,
      `status=${badCreate.status} before=${beforeCnt} after=${afterCnt}`)

    const cleared = await api('PUT', `/api/app/session/${EP_SESSION}/prompt`, { token: owner.token, body: { promptId: 0 } })
    const clearedList = await api('GET', `/api/app/${AGENT_ID}/session/list`, { token: owner.token })
    check('AP-44k 清除绑定 200 且 promptId=0',
      cleared.status === 200 && (clearedList.json?.items ?? []).some((s) => String(s.sessionId) === EP_SESSION && Number(s.promptId) === 0),
      `${cleared.status}`)

    // 删除提示词后会话仍保留 promptId，但对话装配时静默降级（不阻塞对话）；此处仅验证软删不回读出可用性错误
    const del = await api('DELETE', `/api/prompt/${PERSONAL_ID}`, { token: owner.token })
    check('AP-44l 清理：删除个人提示词 200', del.status === 200, `${del.status}`)
  }

  // AP-45 对话开场白（Agent 应用配置：启用开关 + 内容，随应用详情下发到聊天页）
  {
    const cfgUrl = `/api/app/${AGENT_ID}/agent-config`
    const OS_TEXT = '你好，我是售前助手，很高兴为你服务！'

    check('AP-45a Member 保存开场白 403', (await api('PUT', cfgUrl, { token: member.token, body: { prompt: '', wikiIds: [], plugins: [], openingStatement: 'x', openingStatementEnabled: true } })).status === 403)

    const saveOs = await api('PUT', cfgUrl, { token: owner.token, body: { prompt: '', wikiIds: [], plugins: [], openingStatement: OS_TEXT, openingStatementEnabled: true } })
    check('AP-45b Owner 保存开场白（启用+内容）200', saveOs.status === 200, `${saveOs.status} ${saveOs.text.slice(0, 140)}`)

    const backOs = await api('GET', cfgUrl, { token: owner.token })
    check('AP-45c 配置回读开场白一致', backOs.status === 200 && backOs.json?.openingStatement === OS_TEXT && backOs.json?.openingStatementEnabled === true,
      backOs.text.slice(0, 200))

    // 聊天页挂载点：应用详情（Member 可读）随详情下发开场白
    const detailMember = await api('GET', `/api/app/${AGENT_ID}`, { token: member.token })
    check('AP-45d Member 查应用详情下发开场白', detailMember.status === 200 && detailMember.json?.openingStatement === OS_TEXT && detailMember.json?.openingStatementEnabled === true,
      detailMember.text.slice(0, 200))

    // 关闭开关：内容保留但不再启用
    const disableOs = await api('PUT', cfgUrl, { token: owner.token, body: { prompt: '', wikiIds: [], plugins: [], openingStatement: OS_TEXT, openingStatementEnabled: false } })
    const detailOff = await api('GET', `/api/app/${AGENT_ID}`, { token: member.token })
    check('AP-45e 关闭开关后详情 enabled=false 且内容保留',
      disableOs.status === 200 && detailOff.json?.openingStatementEnabled === false && detailOff.json?.openingStatement === OS_TEXT,
      detailOff.text.slice(0, 200))

    // 校验：超 4000 字 400，且不写入
    check('AP-45f 开场白超 4000 字 400', (await api('PUT', cfgUrl, { token: owner.token, body: { prompt: '', wikiIds: [], plugins: [], openingStatement: 'x'.repeat(4001), openingStatementEnabled: true } })).status === 400)
    const afterBad = await api('GET', cfgUrl, { token: owner.token })
    check('AP-45g 校验失败不写入开场白', afterBad.json?.openingStatement === OS_TEXT && afterBad.json?.openingStatementEnabled === false,
      afterBad.text.slice(0, 200))

    // 流程应用开场白默认为空（设计器系统设置里配置过才有值，见 workflow-e2e WF-15）
    const wfDetail = await api('GET', `/api/app/${WORKFLOW_ID}`, { token: owner.token })
    check('AP-45h 流程应用详情开场白为空', wfDetail.status === 200 && wfDetail.json?.openingStatement === '' && wfDetail.json?.openingStatementEnabled === false,
      wfDetail.text.slice(0, 200))
  }

  // AP-57 快捷输入（Agent 应用配置：管理员自定义多条，聊天页欢迎态点击即发送）
  {
    const cfgUrl = `/api/app/${AGENT_ID}/agent-config`
    const QI = ['帮我总结一份文档的核心要点', '根据知识库回答一个业务问题', '写一段简洁的产品介绍']

    check('AP-57a Member 保存快捷输入 403', (await api('PUT', cfgUrl, { token: member.token, body: { prompt: '', wikiIds: [], plugins: [], quickInputs: QI } })).status === 403)

    const saveQi = await api('PUT', cfgUrl, { token: owner.token, body: { prompt: '', wikiIds: [], plugins: [], quickInputs: QI } })
    check('AP-57b Owner 保存快捷输入（3 条）200', saveQi.status === 200, `${saveQi.status} ${saveQi.text.slice(0, 140)}`)

    const backQi = await api('GET', cfgUrl, { token: owner.token })
    check('AP-57c 配置回读快捷输入一致', backQi.status === 200 && JSON.stringify(backQi.json?.quickInputs) === JSON.stringify(QI),
      backQi.text.slice(0, 200))

    // 聊天页挂载点：应用详情（Member 可读）随详情下发快捷输入
    const detailQi = await api('GET', `/api/app/${AGENT_ID}`, { token: member.token })
    check('AP-57d Member 查应用详情下发快捷输入', detailQi.status === 200 && JSON.stringify(detailQi.json?.quickInputs) === JSON.stringify(QI),
      detailQi.text.slice(0, 200))

    // 规范化：去首尾空白、丢弃空串、去重
    const norm = await api('PUT', cfgUrl, { token: owner.token, body: { prompt: '', wikiIds: [], plugins: [], quickInputs: ['  第一条  ', '', '第一条', '第二条'] } })
    const backNorm = await api('GET', cfgUrl, { token: owner.token })
    check('AP-57e 空白与重复项被清理', norm.status === 200 && JSON.stringify(backNorm.json?.quickInputs) === JSON.stringify(['第一条', '第二条']),
      backNorm.text.slice(0, 200))

    check('AP-57f 超过 10 条 400', (await api('PUT', cfgUrl, { token: owner.token, body: { prompt: '', wikiIds: [], plugins: [], quickInputs: Array.from({ length: 11 }, (_, i) => `q${i}`) } })).status === 400)
    check('AP-57g 单条超 200 字 400', (await api('PUT', cfgUrl, { token: owner.token, body: { prompt: '', wikiIds: [], plugins: [], quickInputs: ['x'.repeat(201)] } })).status === 400)
    const afterBad = await api('GET', cfgUrl, { token: owner.token })
    check('AP-57h 校验失败不写入快捷输入', JSON.stringify(afterBad.json?.quickInputs) === JSON.stringify(['第一条', '第二条']),
      afterBad.text.slice(0, 200))

    // 旧前端兼容：请求不携带 quickInputs 字段保存其他字段时，已保存的快捷输入保持不变
    await api('PUT', cfgUrl, { token: owner.token, body: { prompt: 'P', wikiIds: [], plugins: [] } })
    const keep = await api('GET', cfgUrl, { token: owner.token })
    check('AP-57i 不携带字段保存时保持原值', JSON.stringify(keep.json?.quickInputs) === JSON.stringify(['第一条', '第二条']),
      keep.text.slice(0, 200))

    const clear = await api('PUT', cfgUrl, { token: owner.token, body: { prompt: 'P', wikiIds: [], plugins: [], quickInputs: [] } })
    const backClear = await api('GET', cfgUrl, { token: owner.token })
    check('AP-57j 空数组清空快捷输入', clear.status === 200 && JSON.stringify(backClear.json?.quickInputs) === '[]',
      backClear.text.slice(0, 200))

    // 发布快照：发布后改草稿，线上详情仍按发布快照下发快捷输入
    const c = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '快捷输入快照' + TS, appType: 'agent' } })
    const SNAP_ID = String(c.json?.value ?? '')
    await api('PUT', `/api/app/${SNAP_ID}/agent-config`, { token: owner.token, body: { prompt: '', wikiIds: [], plugins: [], quickInputs: ['快照版问题'] } })
    await api('POST', `/api/app/${SNAP_ID}/publish`, { token: owner.token })
    await api('PUT', `/api/app/${SNAP_ID}/agent-config`, { token: owner.token, body: { prompt: '', wikiIds: [], plugins: [], quickInputs: ['草稿版问题'] } })
    const snapDetail = await api('GET', `/api/app/${SNAP_ID}`, { token: member.token })
    check('AP-57k 已发布应用详情按快照下发快捷输入', snapDetail.status === 200 && JSON.stringify(snapDetail.json?.quickInputs) === JSON.stringify(['快照版问题']),
      snapDetail.text.slice(0, 200))
  }

  // AP-54 发布配置快照双轨：发布后管理员改配置只落草稿，线上按发布快照执行，重新发布后生效
  {
    const c = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '快照双轨' + TS, appType: 'agent' } })
    const SNAP_ID = String(c.json?.value ?? '')
    const snapUrl = `/api/app/${SNAP_ID}/agent-config`
    const saveA = await api('PUT', snapUrl, { token: owner.token, body: { prompt: 'PROMPT_A', wikiIds: [], plugins: [], openingStatement: 'OS_A', openingStatementEnabled: true } })
    check('AP-54a 发布前保存配置 200', c.status === 200 && saveA.status === 200, `${saveA.status} ${saveA.text.slice(0, 120)}`)
    check('AP-54b 发布应用 200', (await api('POST', `/api/app/${SNAP_ID}/publish`, { token: owner.token })).status === 200)
    const cfg1 = await api('GET', snapUrl, { token: owner.token })
    check('AP-54c 发布后配置状态=草稿与发布一致', cfg1.status === 200 && cfg1.json?.status === 1, cfg1.text.slice(0, 160))

    // 管理员继续改配置（草稿）：配置回读为草稿值，线上详情仍下发发布快照开场白
    const saveB = await api('PUT', snapUrl, { token: owner.token, body: { prompt: 'PROMPT_B', wikiIds: [], plugins: [], openingStatement: 'OS_B', openingStatementEnabled: true } })
    const cfg2 = await api('GET', snapUrl, { token: owner.token })
    const detail1 = await api('GET', `/api/app/${SNAP_ID}`, { token: member.token })
    check('AP-54d 草稿保存后回读草稿值且状态=未发布变更', saveB.status === 200 && cfg2.json?.prompt === 'PROMPT_B' && cfg2.json?.status === 0,
      cfg2.text.slice(0, 160))
    check('AP-54e 线上详情仍下发已发布开场白 OS_A', detail1.status === 200 && detail1.json?.openingStatement === 'OS_A' && detail1.json?.openingStatementEnabled === true,
      detail1.text.slice(0, 160))

    // 重新发布后草稿进入发布快照
    check('AP-54f 重新发布 200', (await api('POST', `/api/app/${SNAP_ID}/publish`, { token: owner.token })).status === 200)
    const cfg3 = await api('GET', snapUrl, { token: owner.token })
    const detail2 = await api('GET', `/api/app/${SNAP_ID}`, { token: member.token })
    check('AP-54g 重新发布后开场白 OS_B 生效且状态回 1', cfg3.json?.status === 1 && detail2.json?.openingStatement === 'OS_B',
      `${cfg3.text.slice(0, 120)} ${detail2.text.slice(0, 120)}`)

    // 未发布应用不进入双轨：详情按实时草稿下发
    const c2 = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '未发布双轨' + TS, appType: 'agent' } })
    const UNP_ID = String(c2.json?.value ?? '')
    await api('PUT', `/api/app/${UNP_ID}/agent-config`, { token: owner.token, body: { prompt: '', wikiIds: [], plugins: [], openingStatement: 'OS_U', openingStatementEnabled: true } })
    const detailU = await api('GET', `/api/app/${UNP_ID}`, { token: owner.token })
    check('AP-54h 未发布应用详情按实时开场白', detailU.status === 200 && detailU.json?.openingStatement === 'OS_U' && detailU.json?.openingStatementEnabled === true,
      detailU.text.slice(0, 160))
    const cfgU = await api('GET', `/api/app/${UNP_ID}/agent-config`, { token: owner.token })
    check('AP-54i 未发布应用配置状态=0', cfgU.status === 200 && cfgU.json?.status === 0, cfgU.text.slice(0, 120))
  }

  // AP-58 流程应用绑定为工具（Agent 应用配置：workflowApps 仅允许本团队已发布流程应用，null 保持原值）
  {
    const cfgUrl = `/api/app/${AGENT_ID}/agent-config`

    // 前置：造一个已发布流程应用（start(question) → end(reply=interpolation)，确定性输出不依赖模型）
    const wfNodes = [
      { key: 'start', name: '开始', type: 'start', inputs: {}, outputs: [{ name: 'question', fieldType: 'string', isRequired: true, description: '用户问题' }] },
      { key: 'end', name: '结束', type: 'end', inputs: { reply: { expressionType: 'interpolation', value: '工具结果:{start.question}', required: false } }, outputs: [{ name: 'reply', fieldType: 'string' }] },
    ]
    const wfDef = { id: '', name: 'wf-tool-e2e', version: 0, status: 'draft', nodes: wfNodes, connections: [{ id: 'c1', source: 'start', target: 'end' }], variables: [], ui: { nodePositions: { start: { x: 80, y: 200 }, end: { x: 480, y: 200 } } } }
    const wfEditor = {
      nodes: wfNodes.map((n) => ({
        id: n.key, type: n.type,
        meta: { position: wfDef.ui.nodePositions[n.key], defaultExpanded: true },
        data: { title: n.name, content: '', inputs: n.inputs, outputs: n.outputs, settings: n.config ?? {} },
        blocks: [],
        edges: wfDef.connections.filter((c) => c.source === n.key).map((c) => ({ sourceNodeID: c.source, targetNodeID: c.target })),
      })),
      edges: [],
    }

    const cPub = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '工具流程' + TS, appType: 'workflow' } })
    const WF_PUB = String(cPub.json?.value ?? '')
    const draft = await api('POST', '/api/app/workflow/draft', { token: owner.token, body: { appId: WF_PUB, teamId: TID, definition: JSON.stringify(wfDef), editorData: JSON.stringify(wfEditor) } })
    check('AP-58a 前置：流程草稿保存 200', draft.status === 200, `${draft.status} ${draft.text.slice(0, 140)}`)
    const pub = await api('POST', '/api/app/workflow/publish', { token: owner.token, body: { appId: WF_PUB, teamId: TID } })
    check('AP-58b 前置：流程发布 200', pub.status === 200, `${pub.status} ${pub.text.slice(0, 140)}`)

    // 未发布流程应用（对照）
    const cDraft = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '草稿工具流程' + TS, appType: 'workflow' } })
    const WF_DRAFT = String(cDraft.json?.value ?? '')

    check('AP-58c Member 绑定流程应用 403', (await api('PUT', cfgUrl, { token: member.token, body: { prompt: '', wikiIds: [], plugins: [], workflowApps: [WF_PUB] } })).status === 403)
    check('AP-58d 绑定 Agent 应用 400', (await api('PUT', cfgUrl, { token: owner.token, body: { prompt: '', wikiIds: [], plugins: [], workflowApps: [AGENT_ID] } })).status === 400)
    check('AP-58e 绑定未发布流程应用 400', (await api('PUT', cfgUrl, { token: owner.token, body: { prompt: '', wikiIds: [], plugins: [], workflowApps: [WF_DRAFT] } })).status === 400)
    check('AP-58f 绑定不存在的流程应用 400', (await api('PUT', cfgUrl, { token: owner.token, body: { prompt: '', wikiIds: [], plugins: [], workflowApps: ['01924f5e-0000-7000-8000-00000000cccc'] } })).status === 400)

    const saveWf = await api('PUT', cfgUrl, { token: owner.token, body: { prompt: 'P', wikiIds: [], plugins: [], workflowApps: [WF_PUB] } })
    check('AP-58g Owner 绑定已发布流程应用 200', saveWf.status === 200, `${saveWf.status} ${saveWf.text.slice(0, 140)}`)
    const backWf = await api('GET', cfgUrl, { token: owner.token })
    check('AP-58h 回读绑定一致', (backWf.json?.workflowApps ?? []).map(String).includes(WF_PUB), backWf.text.slice(0, 200))

    // 旧前端兼容：不携带 workflowApps 字段保存其他字段时，绑定保持不变
    await api('PUT', cfgUrl, { token: owner.token, body: { prompt: 'P2', wikiIds: [], plugins: [] } })
    const keep = await api('GET', cfgUrl, { token: owner.token })
    check('AP-58i 不携带字段保存时保持绑定', (keep.json?.workflowApps ?? []).map(String).includes(WF_PUB) && keep.json?.prompt === 'P2', keep.text.slice(0, 200))

    // 发布快照含流程应用绑定：发布后改草稿，状态进入未发布变更；重新发布后回 1
    await api('POST', `/api/app/${AGENT_ID}/publish`, { token: owner.token })
    const st1 = await api('GET', cfgUrl, { token: owner.token })
    check('AP-58j 发布后配置状态=1', st1.json?.status === 1, st1.text.slice(0, 120))
    await api('PUT', cfgUrl, { token: owner.token, body: { prompt: 'P3', wikiIds: [], plugins: [], workflowApps: [] } })
    const st2 = await api('GET', cfgUrl, { token: owner.token })
    check('AP-58k 草稿清空绑定后状态=0 且草稿回读为空', st2.json?.status === 0 && (st2.json?.workflowApps ?? []).length === 0, st2.text.slice(0, 160))
    check('AP-58l 重新发布 200', (await api('POST', `/api/app/${AGENT_ID}/publish`, { token: owner.token })).status === 200)
  }

  // AP-59 对话链路：绑定的已发布流程应用被装配为工具，Agent 对话中 call_tool 驱动流程并回传结果
  // 模型为本地 OpenAI 兼容桩（流式/非流式均支持）：首轮直接 call_tool(workflow__*)，收到工具结果后回显.
  {
    const adminLogin = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
    const adminToken = adminLogin.json?.accessToken
    if (!adminToken) {
      console.log('SKIP | AP-59 无 admin 账号（admin/abcd123456），跳过对话链路验证')
    } else {
      // 已发布流程应用（沿用 AP-58 形状：start(question) → end(reply=插值)，无模型/沙箱依赖）
      const wfName = 'flowtool' + TS
      const wfNodes = [
        { key: 'start', name: '开始', type: 'start', inputs: {}, outputs: [{ name: 'question', fieldType: 'string', isRequired: true, description: '用户问题' }] },
        { key: 'end', name: '结束', type: 'end', inputs: { reply: { expressionType: 'interpolation', value: '工具结果:{start.question}', required: false } }, outputs: [{ name: 'reply', fieldType: 'string' }] },
      ]
      const wfDef = { id: '', name: 'wf-tool-chat', version: 0, status: 'draft', nodes: wfNodes, connections: [{ id: 'c1', source: 'start', target: 'end' }], variables: [], ui: { nodePositions: { start: { x: 80, y: 200 }, end: { x: 480, y: 200 } } } }
      const wfEditor = {
        nodes: wfNodes.map((n) => ({
          id: n.key, type: n.type,
          meta: { position: wfDef.ui.nodePositions[n.key], defaultExpanded: true },
          data: { title: n.name, content: '', inputs: n.inputs, outputs: n.outputs, settings: n.config ?? {} },
          blocks: [],
          edges: wfDef.connections.filter((c) => c.source === n.key).map((c) => ({ sourceNodeID: c.source, targetNodeID: c.target })),
        })),
        edges: [],
      }
      const cwf = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: wfName, appType: 'workflow' } })
      const WF_CHAT = String(cwf.json?.value ?? '')
      await api('POST', '/api/app/workflow/draft', { token: owner.token, body: { appId: WF_CHAT, teamId: TID, definition: JSON.stringify(wfDef), editorData: JSON.stringify(wfEditor) } })
      const wfPub = await api('POST', '/api/app/workflow/publish', { token: owner.token, body: { appId: WF_CHAT, teamId: TID } })
      check('AP-59a 前置：工具流程发布 200', wfPub.status === 200, `${wfPub.status} ${wfPub.text.slice(0, 140)}`)

      // OpenAI 兼容对话桩：无工具结果时发 call_tool(workflow__<name>)，有工具结果时回显其内容
      const WF_TOOL_NAME = 'workflow__' + wfName
      const stub = http.createServer((req, res) => {
        console.log(`STUB-HIT | ${req.method} ${req.url}`)
        let body = ''
        req.on('data', (chunk) => { body += chunk })
        req.on('end', () => {
          let payload = {}
          try { payload = JSON.parse(body || '{}') } catch { /* 按无工具结果处理 */ }
          const toolMsg = (payload.messages ?? []).find((m) => m.role === 'tool')
          // 工具结果为 JSON（{success,reply,instanceId}），回显其中的 reply；解析失败按原文回显
          let toolEcho = String(toolMsg?.content ?? '')
          try {
            const parsed = JSON.parse(toolEcho)
            if (parsed && typeof parsed.reply === 'string') toolEcho = parsed.reply
          } catch { /* 保持原文 */ }
          const base = { id: 'chatcmpl-stub', object: 'chat.completion.chunk', created: Math.floor(Date.now() / 1000), model: payload.model ?? 'stub' }
          if (payload.stream) {
            res.writeHead(200, { 'Content-Type': 'text/event-stream' })
            const send = (obj) => res.write('data: ' + JSON.stringify(obj) + '\n\n')
            if (toolMsg) {
              send({ ...base, choices: [{ index: 0, delta: { role: 'assistant', content: '流程返回：' + toolEcho.slice(0, 400) }, finish_reason: null }] })
              send({ ...base, choices: [{ index: 0, delta: {}, finish_reason: 'stop' }] })
            } else {
              const fnArgs = JSON.stringify({ toolName: WF_TOOL_NAME, argumentsJson: JSON.stringify({ query: '工单123' }) })
              send({ ...base, choices: [{ index: 0, delta: { role: 'assistant', content: null, tool_calls: [{ index: 0, id: 'call_stub_1', type: 'function', function: { name: 'call_tool', arguments: fnArgs } }] }, finish_reason: null }] })
              send({ ...base, choices: [{ index: 0, delta: {}, finish_reason: 'tool_calls' }] })
            }
            res.write('data: [DONE]\n\n')
            res.end()
          } else {
            res.writeHead(200, { 'Content-Type': 'application/json' })
            const message = toolMsg
              ? { role: 'assistant', content: '流程返回：' + toolEcho.slice(0, 400) }
              : { role: 'assistant', content: null, tool_calls: [{ id: 'call_stub_1', type: 'function', function: { name: 'call_tool', arguments: JSON.stringify({ toolName: WF_TOOL_NAME, argumentsJson: JSON.stringify({ query: '工单123' }) }) } }] }
            res.end(JSON.stringify({ ...base, object: 'chat.completion', choices: [{ index: 0, message, finish_reason: toolMsg ? 'stop' : 'tool_calls' }] }))
          }
        })
      })
      await new Promise((resolve) => stub.listen(0, '127.0.0.1', resolve))
      stub.unref()
      const stubPort = stub.address().port

      // 桩渠道 + 对话模型，授权给本次团队（结束时清理）
      const chName = `ap59-mock渠道-${TS}`
      const modelName = `ap59-chat-mock-${TS}`
      const crc = await api('POST', '/api/ai/channel', { token: adminToken, body: { providerKey: 'openai', name: chName, protocolFamily: 'openaiChatCompletions', baseUrl: `http://127.0.0.1:${stubPort}/v1`, apiKey: 'ap59-mock-key', enabled: true, description: 'app-e2e AP-59 桩渠道' } })
      check('AP-59b 创建桩渠道 200', crc.status === 200, `${crc.status} ${crc.text.slice(0, 140)}`)
      const channels = await api('GET', '/api/ai/channel', { token: adminToken })
      const CH_ID = (channels.json?.items ?? []).find((c) => c.name === chName)?.id ?? ''
      const mrc = await api('POST', '/api/ai/model', { token: adminToken, body: { channelId: CH_ID, meta: { modelId: modelName, name: modelName, modelKind: 'conversation', description: 'app-e2e AP-59 桩模型' }, enabled: true, isPublic: false } })
      check('AP-59c 创建桩对话模型 200', mrc.status === 200, `${mrc.status} ${mrc.text.slice(0, 140)}`)
      const models = await api('GET', `/api/ai/model?channelId=${CH_ID}`, { token: adminToken })
      const MODEL_ID = String((models.json?.items ?? []).find((m) => m.name === modelName)?.id ?? '')
      const cur = await api('GET', `/api/ai/model/${MODEL_ID}/authorization`, { token: adminToken })
      const teamIds = [...new Set([...(cur.json?.items ?? []).map((i) => Number(i.teamId)), TID])]
      check('AP-59d 桩模型授权团队 200', (await api('PUT', `/api/ai/model/${MODEL_ID}/authorization`, { token: adminToken, body: { teamIds } })).status === 200)

      // Agent 应用：绑模型 + 流程应用 → 发布（正式会话按发布快照装配工具）
      const ca = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '流程工具宿主' + TS, appType: 'agent' } })
      const HOST_ID = String(ca.json?.value ?? '')
      const cfgSave = await api('PUT', `/api/app/${HOST_ID}/agent-config`, { token: owner.token, body: { prompt: '你会调用流程工具', wikiIds: [], plugins: [], modelId: MODEL_ID, workflowApps: [WF_CHAT] } })
      check('AP-59e 宿主应用绑定模型与流程应用 200', cfgSave.status === 200, `${cfgSave.status} ${cfgSave.text.slice(0, 140)}`)
      check('AP-59f 宿主应用发布 200', (await api('POST', `/api/app/${HOST_ID}/publish`, { token: owner.token })).status === 200)

      const chatSse = async (appId, token, sessionId, text) => {
        const res = await fetch(`${BASE}/api/agent/${appId}/chat`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
          body: JSON.stringify({
            threadId: sessionId,
            runId: crypto.randomUUID(),
            state: {},
            messages: [{ id: crypto.randomUUID(), role: 'user', content: text }],
            tools: [],
            context: [],
            forwardedProps: {},
          }),
        })
        const raw = await res.text()
        const events = raw.split('\n')
          .filter((l) => l.startsWith('data:'))
          .map((l) => { try { return JSON.parse(l.slice(5).trim()) } catch { return null } })
          .filter(Boolean)
        const reply = events
          .filter((e) => e.type === 'TEXT_MESSAGE_CONTENT' && typeof e.delta === 'string')
          .map((e) => e.delta)
          .join('')
        const runError = events.find((e) => e.type === 'RUN_ERROR')?.message ?? null
        return { status: res.status, reply, runError }
      }

      const sess = await api('POST', `/api/app/${HOST_ID}/session`, { token: owner.token, body: { title: 'ap59' } })
      const SID = String(sess.json?.value ?? '')
      check('AP-59g 创建正式会话 200', sess.status === 200 && isGuid(SID), `${sess.status} ${sess.text.slice(0, 140)}`)

      const chat1 = await chatSse(HOST_ID, owner.token, SID, '请处理工单123')
      check('AP-59h 对话中 call_tool 驱动流程并回传结果', chat1.status === 200 && chat1.reply.includes('工具结果:工单123') && !chat1.runError,
        `${chat1.status} reply=${chat1.reply.slice(0, 160)} err=${chat1.runError}`)

      // 解绑后重新发布：工具下线，同名调用返回「工具不存在」错误文本
      await api('PUT', `/api/app/${HOST_ID}/agent-config`, { token: owner.token, body: { prompt: '你会调用流程工具', wikiIds: [], plugins: [], modelId: MODEL_ID, workflowApps: [] } })
      await api('POST', `/api/app/${HOST_ID}/publish`, { token: owner.token })
      const sess2 = await api('POST', `/api/app/${HOST_ID}/session`, { token: owner.token, body: { title: 'ap59-unbound' } })
      const chat2 = await chatSse(HOST_ID, owner.token, String(sess2.json?.value ?? ''), '再处理一次')
      check('AP-59i 解绑发布后工具下线（无任何工具，call_tool 不存在）', chat2.status === 200 && chat2.reply.includes('not found'),
        `${chat2.status} reply=${chat2.reply.slice(0, 160)} err=${chat2.runError}`)

      // 清理桩模型/渠道（应用与流程留给台账）
      await api('POST', '/api/ai/model/batch-delete', { token: adminToken, body: { modelIds: [MODEL_ID] } })
      await api('DELETE', `/api/ai/channel/${CH_ID}`, { token: adminToken })
      stub.close()
    }
  }

  // AP-60 审批策略：审批模式下白名单插件与沙箱自动放行，其余工具仍挂起等待人工决策；
  // 模型为本地 OpenAI 兼容桩（用户消息 CALL:<toolName> 指定要调用的工具，收到工具结果后回显其内容）.
  {
    const adminLogin = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
    const adminToken = adminLogin.json?.accessToken
    if (!adminToken) {
      console.log('SKIP | AP-60 无 admin 账号（admin/abcd123456），跳过审批策略验证')
    } else {
      const team = await api('POST', '/api/team', { token: adminToken, body: { name: `ap60-审批策略-${TS}` } })
      const TID = Number(team.json?.value ?? 0)

      // 团队可访问插件：优先静态（工具名=插件名，无外部依赖），否则取第一个非空 Guid 插件（工具名经 userconfig 下发）
      const pl = await api('GET', `/api/team/${TID}/plugin/list`, { token: adminToken })
      const items = (pl.json?.items ?? []).filter((i) => isGuid(i.pluginId) && i.pluginId !== '00000000-0000-0000-0000-000000000000')
      const bindable = items.find((i) => i.kind === 'static') ?? items[0] ?? null
      if (!bindable) {
        console.log('SKIP | AP-60 团队无可绑定插件，跳过审批策略验证')
      } else {
        // 桩模型：用户消息 CALL:<toolName> → call_tool(toolName)；有工具结果时原样回显
        const stub = http.createServer((req, res) => {
          let body = ''
          req.on('data', (chunk) => { body += chunk })
          req.on('end', () => {
            let payload = {}
            try { payload = JSON.parse(body || '{}') } catch { /* 按无指令处理 */ }
            const toolMsg = (payload.messages ?? []).find((m) => m.role === 'tool')
            const lastUser = [...(payload.messages ?? [])].reverse().find((m) => m.role === 'user')
            const wantTool = String(lastUser?.content ?? '').match(/CALL:([\w-]+)/)?.[1] ?? null
            const base = { id: 'chatcmpl-stub60', object: 'chat.completion.chunk', created: Math.floor(Date.now() / 1000), model: payload.model ?? 'stub60' }
            const send = (obj) => res.write('data: ' + JSON.stringify(obj) + '\n\n')
            if (payload.stream) {
              res.writeHead(200, { 'Content-Type': 'text/event-stream' })
              if (toolMsg) {
                send({ ...base, choices: [{ index: 0, delta: { role: 'assistant', content: '工具回显:' + String(toolMsg.content ?? '').slice(0, 600) }, finish_reason: null }] })
                send({ ...base, choices: [{ index: 0, delta: {}, finish_reason: 'stop' }] })
              } else {
                const fnArgs = JSON.stringify({ toolName: wantTool, argumentsJson: '{}' })
                send({ ...base, choices: [{ index: 0, delta: { role: 'assistant', content: null, tool_calls: [{ index: 0, id: 'call_stub60_1', type: 'function', function: { name: 'call_tool', arguments: fnArgs } }] }, finish_reason: null }] })
                send({ ...base, choices: [{ index: 0, delta: {}, finish_reason: 'tool_calls' }] })
              }
              res.write('data: [DONE]\n\n')
              res.end()
            } else {
              res.writeHead(200, { 'Content-Type': 'application/json' })
              const message = toolMsg
                ? { role: 'assistant', content: '工具回显:' + String(toolMsg.content ?? '').slice(0, 600) }
                : { role: 'assistant', content: null, tool_calls: [{ id: 'call_stub60_1', type: 'function', function: { name: 'call_tool', arguments: JSON.stringify({ toolName: wantTool, argumentsJson: '{}' }) } }] }
              res.end(JSON.stringify({ ...base, object: 'chat.completion', choices: [{ index: 0, message, finish_reason: toolMsg ? 'stop' : 'tool_calls' }] }))
            }
          })
        })
        await new Promise((resolve) => stub.listen(0, '127.0.0.1', resolve))
        stub.unref()
        const stubPort = stub.address().port

        const chName = `ap60-mock渠道-${TS}`
        const modelName = `ap60-chat-mock-${TS}`
        await api('POST', '/api/ai/channel', { token: adminToken, body: { providerKey: 'openai', name: chName, protocolFamily: 'openaiChatCompletions', baseUrl: `http://127.0.0.1:${stubPort}/v1`, apiKey: 'ap60-mock-key', enabled: true, description: 'app-e2e AP-60 桩渠道' } })
        const channels = await api('GET', '/api/ai/channel', { token: adminToken })
        const CH_ID = (channels.json?.items ?? []).find((c) => c.name === chName)?.id ?? ''
        await api('POST', '/api/ai/model', { token: adminToken, body: { channelId: CH_ID, meta: { modelId: modelName, name: modelName, modelKind: 'conversation', description: 'app-e2e AP-60 桩模型' }, enabled: true, isPublic: false } })
        const models = await api('GET', `/api/ai/model?channelId=${CH_ID}`, { token: adminToken })
        const MODEL_ID = String((models.json?.items ?? []).find((m) => m.name === modelName)?.id ?? '')
        const curAuth = await api('GET', `/api/ai/model/${MODEL_ID}/authorization`, { token: adminToken })
        const teamIds = [...new Set([...(curAuth.json?.items ?? []).map((i) => Number(i.teamId)), TID])]
        await api('PUT', `/api/ai/model/${MODEL_ID}/authorization`, { token: adminToken, body: { teamIds } })

        const app = await api('POST', '/api/app', { token: adminToken, body: { teamId: TID, name: `ap60-审批策略应用-${TS}`, appType: 'agent' } })
        const APP_ID = String(app.json?.value ?? '')
        const cfgUrl = `/api/app/${APP_ID}/agent-config`

        // 保存校验：自动放行插件必须是已绑定插件（未绑定的固定 Guid 一律 400）
        const badPolicy = await api('PUT', cfgUrl, { token: adminToken, body: {
          prompt: 'p', wikiIds: [], plugins: [String(bindable.pluginId)], modelId: MODEL_ID,
          executionSettings: { toolApproval: { autoApprovePlugins: ['01924f5e-0000-7000-8000-00000000aaaa'], sandboxAutoApproved: false } },
        } })
        check('AP-60a 自动放行插件未绑定时保存 400', badPolicy.status === 400, `${badPolicy.status} ${badPolicy.text.slice(0, 140)}`)
        const badShape = await api('PUT', cfgUrl, { token: adminToken, body: {
          prompt: 'p', wikiIds: [], plugins: [], modelId: MODEL_ID,
          executionSettings: { toolApproval: { autoApprovePlugins: 'not-array' } },
        } })
        check('AP-60b 自动放行插件非法结构保存 400', badShape.status === 400, `${badShape.status} ${badShape.text.slice(0, 140)}`)

        // 正式配置：绑定插件并加入白名单，启用沙箱但不自动放行沙箱
        const saveCfg = await api('PUT', cfgUrl, { token: adminToken, body: {
          prompt: 'p', wikiIds: [], plugins: [String(bindable.pluginId)], modelId: MODEL_ID,
          executionSettings: {
            sandbox: { enabled: true },
            toolApproval: { autoApprovePlugins: [String(bindable.pluginId)], sandboxAutoApproved: false },
          },
        } })
        check('AP-60c 白名单插件保存 200', saveCfg.status === 200, `${saveCfg.status} ${saveCfg.text.slice(0, 140)}`)
        check('AP-60d 发布应用 200', (await api('POST', `/api/app/${APP_ID}/publish`, { token: adminToken })).status === 200)

      // userconfig 下发自动放行工具名（静态插件=插件名，自定义插件=插件名__函数名）
      const uc = await api('GET', `/api/app/${APP_ID}/userconfig`, { token: adminToken })
      const autoNames = uc.json?.toolApprovalAutoApprovedNames ?? []
      const nameHit = autoNames.some((n) => n === bindable.pluginName || n.startsWith(`${bindable.pluginName}__`))
      check('AP-60e userconfig 下发白名单插件工具名', uc.status === 200 && nameHit && (uc.json?.toolApprovalAutoApprovedPrefixes ?? []).length === 0,
        `names=${JSON.stringify(autoNames).slice(0, 200)} pluginName=${bindable.pluginName}`)

        const chatSse = async (sessionId, text, mode) => {
          // 审批等待上限 300s：用例里预期自动放行的调用若误入等待会拖垮脚本，60s 兜底中断判失败
          const controller = new AbortController()
          const timer = setTimeout(() => controller.abort(), 60000)
          let res
          try {
            res = await fetch(`${BASE}/api/agent/${APP_ID}/chat`, {
              method: 'POST',
              headers: {
                'Content-Type': 'application/json',
                Authorization: `Bearer ${adminToken}`,
                'X-Moai-Tool-Approval': mode ?? 'approval',
              },
              body: JSON.stringify({
                threadId: sessionId,
                runId: crypto.randomUUID(),
                state: {},
                messages: [{ id: crypto.randomUUID(), role: 'user', content: text }],
                tools: [],
                context: [],
                forwardedProps: {},
              }),
              signal: controller.signal,
            })
          } catch {
            return { status: 0, reply: '', runError: 'abort-60s' }
          }
          let raw = ''
          try {
            raw = await res.text()
          } catch {
            clearTimeout(timer)
            return { status: 0, reply: '', runError: 'abort-60s' }
          }
          clearTimeout(timer)
          const events = raw.split('\n')
            .filter((l) => l.startsWith('data:'))
            .map((l) => { try { return JSON.parse(l.slice(5).trim()) } catch { return null } })
            .filter(Boolean)
          const reply = events
            .filter((e) => e.type === 'TEXT_MESSAGE_CONTENT' && typeof e.delta === 'string')
            .map((e) => e.delta)
            .join('')
          const runError = events.find((e) => e.type === 'RUN_ERROR')?.message ?? null
          return { status: res.status, reply, runError }
        }

        // 白名单插件：审批模式下直接执行，无待审批记录（决策接口 missing）
        const sess1 = await api('POST', `/api/app/${APP_ID}/session`, { token: adminToken, body: { title: 'ap60-white' } })
        const SID1 = String(sess1.json?.value ?? '')
        const chat1 = await chatSse(SID1, `CALL:${autoNames[0] ?? bindable.pluginName}`)
        const dec1 = await api('POST', `/api/app/session/${SID1}/tool-approval`, { token: adminToken, body: { toolName: autoNames[0] ?? bindable.pluginName, approved: false } })
        check('AP-60f 审批模式下白名单插件直接执行（无人工等待）',
          chat1.status === 200 && chat1.reply.includes('工具回显:') && !chat1.reply.includes('工具不存在') && !chat1.runError && dec1.json?.status === 'missing',
          `${chat1.status} reply=${chat1.reply.slice(0, 160)} err=${chat1.runError} dec=${dec1.json?.status}`)

        // 非白名单（沙箱未开自动放行）：审批模式下挂起等待，拒绝后模型收到拒绝说明且工具未执行
        const sess2 = await api('POST', `/api/app/${APP_ID}/session`, { token: adminToken, body: { title: 'ap60-wait' } })
        const SID2 = String(sess2.json?.value ?? '')
        const chat2Promise = chatSse(SID2, 'CALL:sandbox_run_shell')
        let dec2Status = ''
        for (let i = 0; i < 100; i++) {
          await new Promise((r) => setTimeout(r, 300))
          const dec = await api('POST', `/api/app/session/${SID2}/tool-approval`, { token: adminToken, body: { toolName: 'sandbox_run_shell', approved: false } })
          if (dec.json?.status === 'rejected') { dec2Status = dec.json.status; break }
        }
        const chat2 = await chat2Promise
        // 工具结果 JSON 中文会被转义（\u62D2\u7EDD=拒绝），两种形态都认
        const rejectedText = chat2.reply.includes('拒绝') || chat2.reply.includes('62D2')
        check('AP-60g 审批模式下非白名单工具挂起并按拒绝收敛',
          chat2.status === 200 && rejectedText && dec2Status === 'rejected',
          `${chat2.status} reply=${chat2.reply.slice(0, 160)} dec=${dec2Status}`)

        // 自动模式（auto 头）：全部工具直接执行，沙箱工具也无人工等待（沙箱未真正运行：CALL 指令的目标在 auto 下也会进入执行，
        // 此处仍用白名单插件验证自动模式全放行语义）
        const sess3 = await api('POST', `/api/app/${APP_ID}/session`, { token: adminToken, body: { title: 'ap60-auto' } })
        const SID3 = String(sess3.json?.value ?? '')
        const chat3 = await chatSse(SID3, `CALL:${autoNames[0] ?? bindable.pluginName}`, 'auto')
        check('AP-60h 自动模式下工具直接执行', chat3.status === 200 && chat3.reply.includes('工具回显:') && !chat3.runError,
          `${chat3.status} reply=${chat3.reply.slice(0, 160)} err=${chat3.runError}`)

        // 草稿改策略不影响线上（发布快照生效）：草稿移除白名单后发布版仍自动放行；重新发布后进入人工等待
        await api('PUT', cfgUrl, { token: adminToken, body: {
          prompt: 'p', wikiIds: [], plugins: [String(bindable.pluginId)], modelId: MODEL_ID,
          executionSettings: { sandbox: { enabled: true }, toolApproval: { autoApprovePlugins: [], sandboxAutoApproved: true } },
        } })
        const sess4 = await api('POST', `/api/app/${APP_ID}/session`, { token: adminToken, body: { title: 'ap60-snap' } })
        const SID4 = String(sess4.json?.value ?? '')
        const chat4 = await chatSse(SID4, `CALL:${autoNames[0] ?? bindable.pluginName}`)
        check('AP-60i 草稿移除白名单后发布快照仍自动放行', chat4.status === 200 && chat4.reply.includes('工具回显:') && !chat4.runError,
          `${chat4.status} reply=${chat4.reply.slice(0, 160)} err=${chat4.runError}`)

        // 重新发布后按新策略：插件回到人工等待（拒绝收敛验证），沙箱变为自动放行
        await api('POST', `/api/app/${APP_ID}/publish`, { token: adminToken })
        const sess5 = await api('POST', `/api/app/${APP_ID}/session`, { token: adminToken, body: { title: 'ap60-repub' } })
        const SID5 = String(sess5.json?.value ?? '')
        const chat5Promise = chatSse(SID5, `CALL:${autoNames[0] ?? bindable.pluginName}`)
        let dec5Status = ''
        for (let i = 0; i < 100; i++) {
          await new Promise((r) => setTimeout(r, 300))
          const dec = await api('POST', `/api/app/session/${SID5}/tool-approval`, { token: adminToken, body: { toolName: autoNames[0] ?? bindable.pluginName, approved: false } })
          if (dec.json?.status === 'rejected') { dec5Status = dec.json.status; break }
        }
        const chat5 = await chat5Promise
        const rejectedText5 = chat5.reply.includes('拒绝') || chat5.reply.includes('62D2')
        check('AP-60j 重新发布后插件回到人工审批', chat5.status === 200 && rejectedText5 && dec5Status === 'rejected',
          `${chat5.status} reply=${chat5.reply.slice(0, 160)} dec=${dec5Status}`)

        // 清理桩模型/渠道（应用留给台账）
        await api('POST', '/api/ai/model/batch-delete', { token: adminToken, body: { modelIds: [MODEL_ID] } })
        await api('DELETE', `/api/ai/channel/${CH_ID}`, { token: adminToken })
        stub.close()
      }
    }
  }

  console.log(`\n===== 应用管理 E2E 汇总: PASS=${PASS} FAIL=${FAIL} =====`)
  process.exit(FAIL > 0 ? 1 : 0)
}

main().catch(e => { console.error('脚本异常:', e); process.exit(2) })
