// 应用对话附件 E2E（场景 @AP-S55）
// 用法：node local-dev/chat-attachment-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5000，可用 APP_BASE 覆盖）
// 前置：后端运行中，MinIO 可达；覆盖附件直传（pre_upload_chat_file）与文本提取（/api/app/chat-attachment/extract）全链路。
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

const sha256Of = (bytes) => crypto.createHash('sha256').update(bytes).digest('hex')

/** 走聊天附件直传管线：pre_upload_chat_file → PUT → complate_url，返回 objectKey */
async function uploadChatFile(token, fileName, bytes, contentType) {
  const pre = await api('POST', '/api/storage/public/pre_upload_chat_file', {
    token,
    body: { fileName, contentType, fileSize: bytes.length, sha256: sha256Of(bytes) },
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
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const owner = await mkuser('ca')
  const TID = Number((await api('POST', '/api/team', { token: owner.token, body: { name: 'chat-att-team-' + TS } })).json.value)

  // CA-01 未登录不能预上传附件
  check('CA-01 未登录预上传 401', (await api('POST', '/api/storage/public/pre_upload_chat_file', {
    body: { fileName: 'a.txt', contentType: 'text/plain', fileSize: 10, sha256: '0'.repeat(64) },
  })).status === 401)

  // CA-02 不支持的扩展名被白名单拒绝
  check('CA-02a .exe 预上传 400', (await uploadChatFile(owner.token, 'tool.exe', Buffer.from('x'.repeat(8)), 'application/octet-stream')).status === 400)
  check('CA-02b 超过 20MB 预上传 400', (await api('POST', '/api/storage/public/pre_upload_chat_file', {
    token: owner.token,
    body: { fileName: 'big.pdf', contentType: 'application/pdf', fileSize: 20 * 1024 * 1024 + 1, sha256: '1'.repeat(64) },
  })).status === 400)

  // CA-03 txt 直传 + 提取，内容原样返回
  const txt = Buffer.from('# 周报\n- 完成附件功能\n- 完成提取链路', 'utf8')
  const up1 = await uploadChatFile(owner.token, '笔记.txt', txt, 'text/plain')
  check('CA-03a txt 直传成功', up1.status === 200 && up1.objectKey.startsWith('public/chat/'), JSON.stringify(up1).slice(0, 120))
  const ex1 = await api('POST', '/api/app/chat-attachment/extract', { token: owner.token, body: { objectKey: up1.objectKey, fileName: '笔记.txt' } })
  check('CA-03b 提取返回原文', ex1.status === 200 && ex1.json?.markdown?.includes('完成附件功能') && ex1.json?.truncated === false, ex1.text.slice(0, 160))

  // CA-04 markdown 文件提取
  const md = Buffer.from('## 需求\n1. 对话支持附件\n2. 提取后注入消息', 'utf8')
  const up2 = await uploadChatFile(owner.token, '需求.md', md, 'text/markdown')
  const ex2 = await api('POST', '/api/app/chat-attachment/extract', { token: owner.token, body: { objectKey: up2.objectKey, fileName: '需求.md' } })
  check('CA-04 md 提取成功', ex2.status === 200 && (ex2.json?.markdown ?? '').includes('对话支持附件'), ex2.text.slice(0, 160))

  // CA-05 图片附件可直传（提取不适用于图片，前端不做提取）
  const png = crypto.randomBytes(64)
  const up3 = await uploadChatFile(owner.token, '截图.png', png, 'image/png')
  check('CA-05 图片直传成功', up3.status === 200 && up3.objectKey.startsWith('public/chat/'), JSON.stringify(up3).slice(0, 120))

  // CA-06 提取接口的目录越权防护：私有目录/其他目录 objectKey 拒绝
  check('CA-06a 非 chat 目录 objectKey 400', (await api('POST', '/api/app/chat-attachment/extract', { token: owner.token, body: { objectKey: 'public/images/abc.png', fileName: 'a.png' } })).status === 400)
  check('CA-06b 私有 wiki 目录 objectKey 400', (await api('POST', '/api/app/chat-attachment/extract', { token: owner.token, body: { objectKey: 'wiki/1/abc.docx', fileName: 'a.docx' } })).status === 400)
  check('CA-06c 空 objectKey 400', (await api('POST', '/api/app/chat-attachment/extract', { token: owner.token, body: { objectKey: '', fileName: 'a.txt' } })).status === 400)

  // CA-07 不存在的附件提取 404
  const ghost = await api('POST', '/api/app/chat-attachment/extract', { token: owner.token, body: { objectKey: 'public/chat/' + 'f'.repeat(64) + '.txt', fileName: 'ghost.txt' } })
  check('CA-07 不存在附件 404', ghost.status === 404, `${ghost.status} ${ghost.text.slice(0, 120)}`)

  // CA-08 未登录不能提取
  check('CA-08 未登录提取 401', (await api('POST', '/api/app/chat-attachment/extract', { body: { objectKey: 'public/chat/a.txt', fileName: 'a.txt' } })).status === 401)

  console.log(`\n结果: ${PASS} 通过, ${FAIL} 失败`)
  process.exit(FAIL > 0 ? 1 : 0)
}

main().catch((e) => {
  console.error('E2E 执行异常:', e)
  process.exit(1)
})
