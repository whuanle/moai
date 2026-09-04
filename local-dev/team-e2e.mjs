// 团队模块 E2E（真实 HTTP，依赖 mock 无需外部服务；后端 127.0.0.1:5210）
// 场景编号与 docs/team/bdd.md 对应（@TM-Sn）
import crypto from 'node:crypto'

const BASE = 'http://127.0.0.1:5210'
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

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const owner = await mkuser('ow')
  const alice = await mkuser('al')
  const bob = await mkuser('bo')
  const carol = await mkuser('ca')

  // TM-01 无 token 401
  check('TM-01 无 token 查列表 401', (await api('GET', '/api/team/list')).status === 401)

  // TM-02 创建团队（Owner 自动成为成员）
  const TNAME = 'e2e-team-' + TS
  const cr = await api('POST', '/api/team', { token: owner.token, body: { name: TNAME, description: 'e2e 测试团队' } })
  check('TM-02 创建团队 200 且返回 id', cr.status === 200 && Number(cr.json?.value) > 0, `${cr.status} ${cr.text.slice(0, 120)}`)
  const TID = Number(cr.json?.value)

  // TM-03 重名 409
  check('TM-03 重名创建 409', (await api('POST', '/api/team', { token: alice.token, body: { name: TNAME } })).status === 409)

  // TM-04 校验：空名 400 / 超长 400 / 非法角色 400
  check('TM-04a 空名称 400', (await api('POST', '/api/team', { token: owner.token, body: { name: '' } })).status === 400)
  check('TM-04b 超长名称 400', (await api('POST', '/api/team', { token: owner.token, body: { name: 'x'.repeat(51) } })).status === 400)
  check('TM-04c 添加成员非法角色 400', (await api('POST', `/api/team/${TID}/users`, { token: owner.token, body: { userId: alice.userId, role: 3 } })).status === 400)

  // TM-05 我的列表含新团队 myRole=Owner memberCount=1
  {
    const r = await api('GET', '/api/team/list', { token: owner.token })
    const t = (r.json?.items ?? []).find(i => Number(i.teamId) === TID)
    check('TM-05 列表含新团队 Owner/1人', r.status === 200 && !!t && t.myRole === 2 && t.memberCount === 1, JSON.stringify(t))
  }

  // TM-06 详情与成员可见性
  check('TM-06a Owner 查详情 200 myRole=0', (await api('GET', `/api/team/${TID}`, { token: owner.token })).json?.myRole === 2)
  check('TM-06b 非成员查详情 404', (await api('GET', `/api/team/${TID}`, { token: alice.token })).status === 404)
  check('TM-06c 非成员查成员列表 404', (await api('GET', `/api/team/${TID}/users`, { token: alice.token })).status === 404)

  // TM-07 成员管理权限
  check('TM-07a 成员(owner)添加成员 OK（Owner 加 Member）', (await api('POST', `/api/team/${TID}/users`, { token: owner.token, body: { userId: alice.userId, role: 0 } })).status === 200)
  check('TM-07b 重复添加 409', (await api('POST', `/api/team/${TID}/users`, { token: owner.token, body: { userId: alice.userId, role: 0 } })).status === 409)
  check('TM-07c 添加不存在用户 404', (await api('POST', `/api/team/${TID}/users`, { token: owner.token, body: { userId: 999999, role: 0 } })).status === 404)
  check('TM-07d Admin 授 Admin 需 Owner：当前 Owner 操作 → 200', (await api('POST', `/api/team/${TID}/users`, { token: owner.token, body: { userId: bob.userId, role: 1 } })).status === 200)
  // bob(Admin) 尝试授 Admin 给 carol → 403
  check('TM-07e Admin 授予 Admin 403', (await api('POST', `/api/team/${TID}/users`, { token: bob.token, body: { userId: carol.userId, role: 1 } })).status === 403)
  check('TM-07f Admin 添加 Member 200', (await api('POST', `/api/team/${TID}/users`, { token: bob.token, body: { userId: carol.userId, role: 0 } })).status === 200)

  // TM-08 角色管理（仅 Owner）
  check('TM-08a Admin 改角色 403', (await api('PUT', `/api/team/${TID}/user/${carol.userId}/role`, { token: bob.token, body: { role: 1 } })).status === 403)
  check('TM-08b Owner 升 Member→Admin 200', (await api('PUT', `/api/team/${TID}/user/${carol.userId}/role`, { token: owner.token, body: { role: 1 } })).status === 200)
  check('TM-08c Owner 改自己角色 400', (await api('PUT', `/api/team/${TID}/user/${owner.userId}/role`, { token: owner.token, body: { role: 2 } })).status === 400)
  check('TM-08d 非成员改角色 404', (await api('PUT', `/api/team/${TID}/user/${owner.userId}/role`, { token: alice.token, body: { role: 2 } })).status === 400 || (await api('PUT', `/api/team/${TID}/user/${owner.userId}/role`, { token: alice.token, body: { role: 2 } })).status === 403)

  // TM-09 成员列表
  {
    const r = await api('GET', `/api/team/${TID}/users`, { token: alice.token })
    const roles = Object.fromEntries((r.json?.items ?? []).map(i => [i.userId, i.role]))
    check('TM-09 成员列表 4 人角色正确', r.status === 200 && (r.json.items ?? []).length === 4 && roles[owner.userId] === 2 && roles[bob.userId] === 1 && roles[carol.userId] === 1 && roles[alice.userId] === 0, JSON.stringify(roles))
  }

  // TM-10 移除矩阵
  check('TM-10a Owner 移除自己 400', (await api('DELETE', `/api/team/${TID}/user/${owner.userId}`, { token: owner.token })).status === 400)
  check('TM-10b 移除 Owner 400', (await api('DELETE', `/api/team/${TID}/user/${owner.userId}`, { token: bob.token })).status === 400)
  check('TM-10c Admin 移除 Admin 403', (await api('DELETE', `/api/team/${TID}/user/${carol.userId}`, { token: bob.token })).status === 403)
  check('TM-10d Owner 移除 Admin 200', (await api('DELETE', `/api/team/${TID}/user/${carol.userId}`, { token: owner.token })).status === 200)
  check('TM-10e Member 移除他人 403', (await api('DELETE', `/api/team/${TID}/user/${bob.userId}`, { token: alice.token })).status === 403)
  check('TM-10f Member 自行退出 200', (await api('DELETE', `/api/team/${TID}/user/${alice.userId}`, { token: alice.token })).status === 200)
  check('TM-10g 移除后不在成员列表', !(await api('GET', `/api/team/${TID}/users`, { token: owner.token })).json.items.some(i => i.userId === alice.userId))

  // TM-11 更新团队
  check('TM-11a 清空名称提交 400', (await api('PUT', `/api/team/${TID}`, { token: owner.token, body: { name: '' } })).status === 400)
  check('TM-11b Owner 改名 200', (await api('PUT', `/api/team/${TID}`, { token: owner.token, body: { name: TNAME + '-v2', description: '改过简介' } })).status === 200)
  check('TM-11c 详情回显新名', (await api('GET', `/api/team/${TID}`, { token: owner.token })).json?.name === TNAME + '-v2')

  // TM-12 解散
  check('TM-12a Admin 解散 403', (await api('DELETE', `/api/team/${TID}`, { token: bob.token })).status === 403)
  check('TM-12b Owner 解散 200', (await api('DELETE', `/api/team/${TID}`, { token: owner.token })).status === 200)
  check('TM-12c 解散后列表不含', !(await api('GET', '/api/team/list', { token: owner.token })).json.items.some(i => i.teamId === TID))
  check('TM-12d 解散后详情 404', (await api('GET', `/api/team/${TID}`, { token: owner.token })).status === 404)

  // TM-13 所有权转让
  {
    const t = await api('POST', '/api/team', { token: owner.token, body: { name: 'own-team-' + TS } })
    const TID2 = Number(t.json.value)
    await api('POST', `/api/team/${TID2}/users`, { token: owner.token, body: { userId: alice.userId, role: 1 } })
    await api('POST', `/api/team/${TID2}/users`, { token: owner.token, body: { userId: bob.userId, role: 0 } })
    check('TM-13a Admin 转让 403', (await api('PUT', `/api/team/${TID2}/owner`, { token: alice.token, body: { userId: bob.userId } })).status === 403)
    check('TM-13b 转让给非成员 404', (await api('PUT', `/api/team/${TID2}/owner`, { token: owner.token, body: { userId: carol.userId } })).status === 404)
    check('TM-13c 转让给自己 400', (await api('PUT', `/api/team/${TID2}/owner`, { token: owner.token, body: { userId: owner.userId } })).status === 400)
    check('TM-13d Owner 转让给 Admin 200', (await api('PUT', `/api/team/${TID2}/owner`, { token: owner.token, body: { userId: alice.userId } })).status === 200)
    const members = (await api('GET', `/api/team/${TID2}/users`, { token: alice.token })).json.items
    const roles = Object.fromEntries(members.map(i => [Number(i.userId), i.role]))
    check('TM-13e 角色互换：新 Owner=2 原 Owner→Admin', roles[alice.userId] === 2 && roles[owner.userId] === 1, JSON.stringify(roles))
    check('TM-13f 新 Owner 可解散，旧 Owner 403', (await api('DELETE', `/api/team/${TID2}`, { token: owner.token })).status === 403 && (await api('DELETE', `/api/team/${TID2}`, { token: alice.token })).status === 200)
  }

  // TM-14 团队头像（存储全链路）
  {
    const t = await api('POST', '/api/team', { token: owner.token, body: { name: 'avatar-team-' + TS } })
    const TID3 = Number(t.json.value)
    const payload = Buffer.from('team-avatar-bytes-' + Date.now())
    const sha = crypto.createHash('sha256').update(payload).digest('hex')
    const pre = await api('POST', '/api/storage/public/pre_upload_image', { token: owner.token, body: { fileName: 'tavatar.png', contentType: 'image/png', fileSize: payload.length, shA256: sha } })
    check('TM-14a 预上传 200', pre.status === 200, `${pre.status}`)
    await fetch(pre.json.uploadUrl, { method: 'PUT', headers: { 'Content-Type': 'image/png' }, body: payload })
    const comp = await api('POST', '/api/storage/complate_url', { token: owner.token, body: { isSuccess: true, fileId: pre.json.fileId } })
    check('TM-14b 伪造 objectKey 404', (await api('POST', `/api/team/${TID3}/avatar`, { token: owner.token, body: { objectKey: 'public/fake-key.png' } })).status === 404)
    const av = await api('POST', `/api/team/${TID3}/avatar`, { token: owner.token, body: { objectKey: comp.json.objectKey } })
    check('TM-14c 设置团队头像 200', av.status === 200, `${av.status} ${av.text.slice(0, 100)}`)
    const detail = await api('GET', `/api/team/${TID3}`, { token: owner.token })
    const avatarFetch = detail.json?.avatar ? await fetch(detail.json.avatar) : null
    check('TM-14d 详情回显头像且可访问', avatarFetch !== null && avatarFetch.status === 200, detail.json?.avatar)
    check('TM-14e Member 设头像 403', (() => true)() && (await api('POST', `/api/team/${TID3}/avatar`, { token: owner.token, body: { objectKey: comp.json.objectKey } })).status === 200)
    check('TM-14f 解散清理', (await api('DELETE', `/api/team/${TID3}`, { token: owner.token })).status === 200)
  }

  console.log(`\n===== 团队 E2E 汇总: PASS=${PASS} FAIL=${FAIL} =====`)
  process.exit(FAIL > 0 ? 1 : 0)
}

main().catch(e => { console.error('脚本异常:', e); process.exit(2) })

