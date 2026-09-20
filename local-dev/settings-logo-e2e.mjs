// 网站 Logo 与网站名称 E2E（场景 @SET-S24+）
// 覆盖：超级管理员在系统设置上传/恢复全局网站 Logo；匿名 serverinfo 暴露 logoPath；门禁与非法文件防护；
//       超级管理员修改网站名称（SYSTEM_NAME，仅前端展示），serverinfo.name 空值回退默认、长度校验与门禁。
// 用法：node local-dev/settings-logo-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5000）
// 前置：后端运行中，MinIO 可达；种子账号 admin / abcd123456（root）。脚本结束时恢复默认 Logo（清空）并清空网站名称。
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

async function rootLogin() {
  const l = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
  return l.json?.accessToken ?? null
}

const serverinfo = () => api('GET', '/api/common/serverinfo')
const putLogo = (token, objectKey) => api('POST', '/api/settings/logo', { token, body: { objectKey } })

/** 走图片上传管线：pre_upload_image → PUT → complate_url，返回 objectKey */
async function uploadImage(token, fileName, bytes, contentType) {
  const pre = await api('POST', '/api/storage/public/pre_upload_image', {
    token,
    body: { fileName, contentType, fileSize: bytes.length, sha256: crypto.createHash('sha256').update(bytes).digest('hex') },
  })
  if (pre.status !== 200) return { status: pre.status, text: pre.text }
  if (!pre.json.isExist) {
    const put = await fetch(pre.json.uploadUrl, { method: 'PUT', headers: { 'Content-Type': contentType }, body: bytes })
    if (!put.ok) throw new Error(`直传失败: ${put.status}`)
    const complete = await api('POST', '/api/storage/complate_url', { token, body: { fileId: pre.json.fileId, isSuccess: true } })
    if (complete.status !== 200) throw new Error(`完成上传失败: ${complete.status}`)
  }
  return { status: 200, objectKey: String(pre.json.objectKey) }
}

async function main() {
  const si = await serverinfo()
  RSA_KEY = si.json.rsaPublic

  const root = await rootLogin()
  check('SET-00 root 登录', Boolean(root))

  // ============ 匿名可读（登录/注册页依赖） ============
  // SET-24 匿名 serverinfo 返回 logoPath 字段（string）
  check('SET-24 匿名 serverinfo 含 logoPath 字段', typeof si.json?.logoPath === 'string', JSON.stringify(si.json ?? {}).slice(0, 160))

  // ============ root 上传并生效 ============
  // SET-25 root 上传图片并提交 Logo
  const png = crypto.randomBytes(256)
  const up = await uploadImage(root, 'site-logo.png', png, 'image/png')
  check('SET-25a 图片直传成功', up.status === 200 && up.objectKey.startsWith('public/images/'), JSON.stringify(up).slice(0, 140))
  const put = await putLogo(root, up.objectKey)
  check('SET-25b root 提交网站 Logo 200', put.status === 200, `${put.status} ${put.text.slice(0, 140)}`)

  // SET-26 设置项与公开 serverinfo 均反映新 Logo
  {
    const q = await api('GET', '/api/settings', { token: root })
    const item = (q.json?.items ?? []).find((s) => s.key === 'SYSTEM_LOGO')
    check('SET-26a 设置项列表含 SYSTEM_LOGO=objectKey', q.status === 200 && item?.value === up.objectKey, `${q.status} ${q.text.slice(0, 160)}`)
  }
  {
    const r = await serverinfo()
    check('SET-26b 匿名 serverinfo logoPath=objectKey', r.status === 200 && r.json?.logoPath === up.objectKey, `${r.status} ${r.text.slice(0, 160)}`)
  }

  // ============ 防护 ============
  // SET-27 普通用户不能修改 Logo
  const member = await mkuser('sl')
  check('SET-27 member 提交 Logo 403', (await putLogo(member.token, up.objectKey)).status === 403)
  check('SET-27b 未登录提交 Logo 401', (await putLogo(undefined, up.objectKey)).status === 401)

  // SET-28 未登记/未完成上传的 objectKey 拒绝
  {
    const r = await putLogo(root, 'public/images/' + 'f'.repeat(64) + '.png')
    check('SET-28 伪造 objectKey 404', r.status === 404 && (r.text.includes('不存在') || r.json?.detail?.includes('不存在')), `${r.status} ${r.text.slice(0, 140)}`)
  }

  // ============ 恢复默认 ============
  {
    const r = await putLogo(root, '')
    check('SET-29a 清空 Logo 恢复默认 200', r.status === 200, `${r.status} ${r.text.slice(0, 140)}`)
    const s = await serverinfo()
    check('SET-29b 匿名 serverinfo logoPath 为空', s.status === 200 && (s.json?.logoPath ?? '') === '', `${s.status} ${s.text.slice(0, 160)}`)
  }

  // ============ 网站名称（SYSTEM_NAME，仅前端展示） ============
  const putName = (token, value) => api('PUT', '/api/settings', { token, body: { key: 'SYSTEM_NAME', value } })
  const NEW_NAME = `MoAI-测试站-${TS}`

  // SET-31 root 修改网站名称后立即反映在设置项与匿名 serverinfo
  {
    const r = await putName(root, NEW_NAME)
    check('SET-31a root 保存 SYSTEM_NAME 200', r.status === 200, `${r.status} ${r.text.slice(0, 140)}`)
    const s = await serverinfo()
    check('SET-31b 匿名 serverinfo name=新名称', s.status === 200 && s.json?.name === NEW_NAME, `${s.status} ${s.text.slice(0, 160)}`)
    const q = await api('GET', '/api/settings', { token: root })
    const item = (q.json?.items ?? []).find((i) => i.key === 'SYSTEM_NAME')
    check('SET-31c 设置项列表含 SYSTEM_NAME=新名称', q.status === 200 && item?.value === NEW_NAME, `${q.status} ${q.text.slice(0, 160)}`)
  }

  // SET-32 超长名称拒绝（>50 字符）
  {
    const r = await putName(root, 'n'.repeat(51))
    check('SET-32 超长名称(>50) 400', r.status === 400, `${r.status} ${r.text.slice(0, 140)}`)
  }

  // SET-33 门禁与恢复默认
  {
    const r = await putName(member.token, '越权改名')
    check('SET-33a 普通用户保存设置 403', r.status === 403, `${r.status} ${r.text.slice(0, 140)}`)
    const c = await putName(root, '')
    check('SET-33b 清空名称 200', c.status === 200, `${c.status} ${c.text.slice(0, 140)}`)
    const s = await serverinfo()
    check(
      'SET-33c 清空后 serverinfo.name 回退配置默认（非空且不等于新名称）',
      s.status === 200 && typeof s.json?.name === 'string' && s.json.name.length > 0 && s.json.name !== NEW_NAME,
      `${s.status} ${s.text.slice(0, 160)}`,
    )
  }

  console.log(`\n总计: PASS ${PASS} / FAIL ${FAIL}`)
  if (FAIL > 0) process.exit(1)
}

main().catch((e) => { console.error('E2E 执行异常:', e); process.exit(1) })
