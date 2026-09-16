// 应用管理 E2E（场景 @AP-Sn）
// 用法：node local-dev/app-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5000，可用 APP_BASE 覆盖）
// 前置：后端运行中，且已执行 asserts/app.sql 建表。
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

    // 流程应用不支持该配置
    check('AP-18 流程应用保存配置 400', (await api('PUT', `/api/app/${WORKFLOW_ID}/agent-config`, { token: owner.token, body: { prompt: '', wikiIds: [], plugins: [] } })).status === 400)
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

  console.log(`\n===== 应用管理 E2E 汇总: PASS=${PASS} FAIL=${FAIL} =====`)
  process.exit(FAIL > 0 ? 1 : 0)
}

main().catch(e => { console.error('脚本异常:', e); process.exit(2) })
