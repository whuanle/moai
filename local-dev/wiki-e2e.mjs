// 知识库模块 E2E（真实 HTTP，依赖 team-e2e 同款用户工厂；后端 127.0.0.1:5210，可用 argv[2] 覆盖）
// 场景编号与 docs/wiki/bdd.md 对应（@WK-Sn）
import crypto from 'node:crypto'

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

const sha256Hex = (data) => crypto.createHash('sha256').update(data).digest('hex')

// ==================== WK-13 桩文档生成器（纯 Node 标准库） ====================
import { deflateSync, crc32 as zlibCrc32 } from 'node:zlib'

/** 生成 2x2 纯色变体 PNG（RGB 8 位），seed 决定像素值，用于断言图片字节一致性 */
function buildPng(seed) {
  const width = 2, height = 2
  const raw = Buffer.alloc(height * (1 + width * 3))
  for (let y = 0; y < height; y++) {
    const rowStart = y * (1 + width * 3)
    raw[rowStart] = 0
    for (let x = 0; x < width; x++) {
      const p = rowStart + 1 + x * 3
      raw[p] = (seed + x) % 256
      raw[p + 1] = (seed + y * 3) % 256
      raw[p + 2] = (seed * 7 + x + y) % 256
    }
  }
  const chunk = (type, data) => {
    const len = Buffer.alloc(4)
    len.writeUInt32BE(data.length, 0)
    const body = Buffer.concat([Buffer.from(type, 'ascii'), data])
    const crc = Buffer.alloc(4)
    crc.writeUInt32BE(zlibCrc32(body) >>> 0, 0)
    return Buffer.concat([len, body, crc])
  }
  const ihdr = Buffer.alloc(13)
  ihdr.writeUInt32BE(width, 0)
  ihdr.writeUInt32BE(height, 4)
  ihdr[8] = 8
  ihdr[9] = 2
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk('IHDR', ihdr),
    chunk('IDAT', deflateSync(raw)),
    chunk('IEND', Buffer.alloc(0)),
  ])
}

/** 手工构造 STORE 型 zip（entry 内容支持 Buffer/string），docx 即 zip 包 */
function buildRawZip(entries) {
  const enc = new TextEncoder()
  const chunks = []
  const central = []
  let offset = 0
  const dosTime = 0
  const dosDate = 0x21
  for (const [name, content] of entries) {
    const nameBuf = enc.encode(name)
    const data = Buffer.isBuffer(content) ? content : Buffer.from(content, 'utf8')
    const crc = zlibCrc32(data) >>> 0
    const local = Buffer.alloc(30)
    local.writeUInt32LE(0x04034b50, 0)
    local.writeUInt16LE(20, 4)
    local.writeUInt16LE(0, 6)
    local.writeUInt16LE(0, 8)
    local.writeUInt16LE(dosTime, 10)
    local.writeUInt16LE(dosDate, 12)
    local.writeUInt32LE(crc, 14)
    local.writeUInt32LE(data.length, 18)
    local.writeUInt32LE(data.length, 22)
    local.writeUInt16LE(nameBuf.length, 26)
    local.writeUInt16LE(0, 28)
    chunks.push(local, nameBuf, data)

    const cd = Buffer.alloc(46)
    cd.writeUInt32LE(0x02014b50, 0)
    cd.writeUInt16LE(20, 4)
    cd.writeUInt16LE(20, 6)
    cd.writeUInt16LE(0, 8)
    cd.writeUInt16LE(0, 10)
    cd.writeUInt16LE(dosTime, 12)
    cd.writeUInt16LE(dosDate, 14)
    cd.writeUInt32LE(crc, 16)
    cd.writeUInt32LE(data.length, 20)
    cd.writeUInt32LE(data.length, 24)
    cd.writeUInt16LE(nameBuf.length, 28)
    cd.writeUInt16LE(0, 30)
    cd.writeUInt16LE(0, 32)
    cd.writeUInt16LE(0, 34)
    cd.writeUInt16LE(0, 36)
    cd.writeUInt32LE(0, 38)
    cd.writeUInt32LE(offset, 42)
    central.push(cd, nameBuf)

    offset += local.length + nameBuf.length + data.length
  }
  const centralBuf = Buffer.concat(central)
  const eocd = Buffer.alloc(22)
  eocd.writeUInt32LE(0x06054b50, 0)
  eocd.writeUInt16LE(entries.length, 8)
  eocd.writeUInt16LE(entries.length, 10)
  eocd.writeUInt32LE(centralBuf.length, 12)
  eocd.writeUInt32LE(offset, 16)
  return Buffer.concat([...chunks, centralBuf, eocd])
}

/** 构造最小 docx：一段文本 + N 张内嵌 PNG（word/media/imageN.png，document.xml drawing 引用） */
function buildDocxWithImages(pngs, text = '内嵌图片提取测试文档') {
  const relsXml = pngs
    .map((_, i) => `<Relationship Id="rId${i + 1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="media/image${i + 1}.png"/>`)
    .join('')
  const drawingsXml = pngs
    .map((_, i) => `<w:p><w:r><w:drawing><wp:inline><wp:extent cx="${990000 + i}" cy="990000"/><a:graphic><a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/picture"><pic:pic xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture"><pic:nvPicPr><pic:cNvPr id="${i + 1}" name="img${i + 1}"/><pic:cNvPicPr/></pic:nvPicPr><pic:blipFill><a:blip r:embed="rId${i + 1}"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill><pic:spPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="990000" cy="990000"/></a:xfrm><a:prstGeom prst="rect"><a:avLst/></a:prstGeom></pic:spPr></pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p>`)
    .join('')
  return buildRawZip([
    ['[Content_Types].xml', '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Default Extension="png" ContentType="image/png"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>'],
    ['_rels/.rels', '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId0" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>'],
    ['word/document.xml', `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"><w:body><w:p><w:r><w:t>${text}</w:t></w:r></w:p>${drawingsXml}</w:body></w:document>`],
    ['word/_rels/document.xml.rels', `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">${relsXml}</Relationships>`],
    ...pngs.map((png, i) => [`word/media/image${i + 1}.png`, png]),
  ])
}

/** 构造最小 PDF：一页 + 一张 FlateDecode 2x2 RGB 内嵌图（提取后应解析出页码） */
function buildPdfWithImage(seed) {
  const raw = Buffer.from(Array.from({ length: 12 }, (_, i) => (seed + i * 13) % 256))
  const img = deflateSync(raw)
  const contents = Buffer.from('q 100 0 0 100 0 0 cm /Im0 Do Q')
  const objs = []
  objs[1] = Buffer.from('<< /Type /Catalog /Pages 2 0 R >>')
  objs[2] = Buffer.from('<< /Type /Pages /Kids [3 0 R] /Count 1 >>')
  objs[3] = Buffer.from('<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Resources << /XObject << /Im0 4 0 R >> >> /Contents 5 0 R >>')
  objs[4] = Buffer.concat([Buffer.from(`<< /Type /XObject /Subtype /Image /Width 2 /Height 2 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode /Length ${img.length} >>\nstream\n`), img, Buffer.from('\nendstream')])
  objs[5] = Buffer.concat([Buffer.from(`<< /Length ${contents.length} >>\nstream\n`), contents, Buffer.from('\nendstream')])
  let pdf = Buffer.from('%PDF-1.4\n')
  const offsets = [0]
  for (let i = 1; i <= 5; i++) {
    offsets[i] = pdf.length
    pdf = Buffer.concat([pdf, Buffer.from(`${i} 0 obj\n`), objs[i], Buffer.from('\nendobj\n')])
  }
  const xrefStart = pdf.length
  let xref = 'xref\n0 6\n0000000000 65535 f \n'
  for (let i = 1; i <= 5; i++) xref += `${String(offsets[i]).padStart(10, '0')} 00000 n \n`
  return Buffer.concat([pdf, Buffer.from(`${xref}trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n${xrefStart}\n%%EOF\n`)])
}

/** WK-13 文档上传（preupload → PUT → complete 自动提取），返回 documentId */
async function uploadWikiDoc(wikiId, token, fileName, contentType, buf) {
  const pre = await api('POST', `/api/wiki/${wikiId}/documents/preupload`, { token, body: { fileName, contentType, fileSize: buf.length, sha256: sha256Hex(buf) } })
  if (pre.status !== 200) return { error: `preupload ${pre.status} ${pre.text.slice(0, 120)}` }
  if (!pre.json.isExist && pre.json.uploadUrl) {
    const put = await fetch(pre.json.uploadUrl, { method: 'PUT', headers: { 'Content-Type': contentType }, body: buf })
    if (put.status !== 200) return { error: `PUT ${put.status}` }
  }
  const comp = await api('POST', `/api/wiki/${wikiId}/documents/complete`, { token, body: { isSuccess: true, fileId: pre.json.fileId, fileName } })
  if (comp.status !== 200) return { error: `complete ${comp.status} ${comp.text.slice(0, 120)}` }
  const list = await api('POST', `/api/wiki/${wikiId}/documents/list`, { token, body: { pageNo: 1, pageSize: 50 } })
  const item = (list.json?.items ?? []).find((x) => Number(x.fileId) === Number(pre.json.fileId))
  const documentId = item ? Number(item.documentId) : 0
  if (documentId <= 0) return { error: 'documents/list 未找到文档' }
  return { documentId }
}

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const owner = await mkuser('wo')
  const alice = await mkuser('wa')
  const bob = await mkuser('wb')
  const outsider = await mkuser('wx')

  // 准备团队：owner 建，alice=Admin，bob=Member
  const t = await api('POST', '/api/team', { token: owner.token, body: { name: 'wiki-team-' + TS } })
  const TID = Number(t.json.value)
  await api('POST', `/api/team/${TID}/users`, { token: owner.token, body: { userId: alice.userId, role: 1 } })
  await api('POST', `/api/team/${TID}/users`, { token: owner.token, body: { userId: bob.userId, role: 0 } })

  // WK-01 无 token 401
  check('WK-01 无 token 查列表 401', (await api('GET', `/api/wiki/list?teamId=${TID}`)).status === 401)

  // WK-02 参数校验
  check('WK-02a 空名称 400', (await api('POST', '/api/wiki', { token: owner.token, body: { teamId: TID, name: '' } })).status === 400)
  check('WK-02b 超长名称 400', (await api('POST', '/api/wiki', { token: owner.token, body: { teamId: TID, name: 'x'.repeat(51) } })).status === 400)
  check('WK-02c 非法 teamId 400', (await api('POST', '/api/wiki', { token: owner.token, body: { teamId: 0, name: 'ok1' } })).status === 400)

  // WK-03 权限：Member 403 / 非成员 404
  check('WK-03a Member 创建 403', (await api('POST', '/api/wiki', { token: bob.token, body: { teamId: TID, name: 'nope' } })).status === 403)
  check('WK-03b 非成员创建 404', (await api('POST', '/api/wiki', { token: outsider.token, body: { teamId: TID, name: 'nope' } })).status === 404)

  // WK-04 Admin 创建成功 + 列表
  const WNAME = 'wk-' + TS
  const cr = await api('POST', '/api/wiki', { token: alice.token, body: { teamId: TID, name: WNAME, description: 'e2e wiki' } })
  check('WK-04a Admin 创建 200 且返回 id', cr.status === 200 && Number(cr.json?.value) > 0, `${cr.status} ${cr.text.slice(0, 120)}`)
  const WID = Number(cr.json?.value)
  {
    const r = await api('GET', `/api/wiki/list?teamId=${TID}`, { token: bob.token })
    const item = (r.json?.items ?? []).find(i => Number(i.wikiId) === WID)
    check('WK-04b Member 查列表 200 且含 myRole', r.status === 200 && r.json.myRole === 0 && !!item, JSON.stringify(r.json).slice(0, 150))
  }
  check('WK-04c 非成员查列表 404', (await api('GET', `/api/wiki/list?teamId=${TID}`, { token: outsider.token })).status === 404)

  // WK-05 同团队重名 409（另一团队同名不冲突）
  check('WK-05a 同团队重名 409', (await api('POST', '/api/wiki', { token: owner.token, body: { teamId: TID, name: WNAME } })).status === 409)
  {
    const t2 = await api('POST', '/api/team', { token: outsider.token, body: { name: 'wiki-team2-' + TS } })
    const TID2 = Number(t2.json.value)
    const r2 = await api('POST', '/api/wiki', { token: outsider.token, body: { teamId: TID2, name: WNAME } })
    check('WK-05b 不同团队同名 200', r2.status === 200, `${r2.status}`)
    await api('DELETE', `/api/wiki/${Number(r2.json?.value)}`, { token: outsider.token })
    // 团队不可解散：清理改为管理员禁用归档
    {
      const rootL = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
      await api('PUT', `/api/admin/team/${TID2}/disable`, { token: rootL.json.accessToken, body: { isDisable: true } })
    }
  }

  // WK-06 详情
  check('WK-06a 成员查详情 200', (await api('GET', `/api/wiki/${WID}`, { token: bob.token })).json?.name === WNAME)
  check('WK-06b 非成员查详情 404', (await api('GET', `/api/wiki/${WID}`, { token: outsider.token })).status === 404)
  check('WK-06c 不存在 404', (await api('GET', '/api/wiki/999999', { token: bob.token })).status === 404)

  // WK-07 更新
  check('WK-07a Member 更新 403', (await api('PUT', `/api/wiki/${WID}`, { token: bob.token, body: { name: 'new-' + WNAME } })).status === 403)
  check('WK-07b Admin 更新 200', (await api('PUT', `/api/wiki/${WID}`, { token: alice.token, body: { name: 'new-' + WNAME, description: '改过' } })).status === 200)
  check('WK-07c 详情回显', (await api('GET', `/api/wiki/${WID}`, { token: alice.token })).json?.name === 'new-' + WNAME)
  check('WK-07d 更新为已有重名 409', await (async () => {
    await api('POST', '/api/wiki', { token: owner.token, body: { teamId: TID, name: 'second-' + TS } })
    return (await api('PUT', `/api/wiki/${WID}`, { token: alice.token, body: { name: 'second-' + TS } })).status
  })() === 409)

  // WK-08 删除
  check('WK-08a Member 删除 403', (await api('DELETE', `/api/wiki/${WID}`, { token: bob.token })).status === 403)
  check('WK-08b Admin 删除 200', (await api('DELETE', `/api/wiki/${WID}`, { token: alice.token })).status === 200)
  check('WK-08c 删除后详情 404', (await api('GET', `/api/wiki/${WID}`, { token: alice.token })).status === 404)
  check('WK-08d 删除后列表不含', !(await api('GET', `/api/wiki/list?teamId=${TID}`, { token: alice.token })).json.items.some(i => i.name === 'new-' + WNAME))
  check('WK-08e 删除后同名可重建', (await api('POST', '/api/wiki', { token: alice.token, body: { teamId: TID, name: 'new-' + WNAME } })).status === 200)

  // WK-09 公开字段已移除：团队知识库不存在公开概念，非成员访问一律 404
  const pubName = 'pub-' + TS
  const pub = await api('POST', '/api/wiki', { token: alice.token, body: { teamId: TID, name: pubName } })
  const PUBID = Number(pub.json?.value)
  check('WK-09a 非成员查详情 404（公开字段已移除）', (await api('GET', `/api/wiki/${PUBID}`, { token: outsider.token })).status === 404)
  check('WK-09b 非成员更新 404', (await api('PUT', `/api/wiki/${PUBID}`, { token: outsider.token, body: { name: 'hack' } })).status === 404)
  check('WK-09c 非成员删除 404', (await api('DELETE', `/api/wiki/${PUBID}`, { token: outsider.token })).status === 404)

  // WK-10 非成员门禁保持
  const privName = 'priv-' + TS
  const prv = await api('POST', '/api/wiki', { token: alice.token, body: { teamId: TID, name: privName } })
  const PRIVID = Number(prv.json?.value)
  check('WK-10 非成员查详情 404', (await api('GET', `/api/wiki/${PRIVID}`, { token: outsider.token })).status === 404)

  // WK-10b 列表项带统计字段（文件数/切片数/最近文档更新时间）
  {
    const listR = await api('GET', `/api/wiki/list?teamId=${TID}`, { token: alice.token })
    const item = (listR.json?.items ?? []).find((i) => Number(i.wikiId) === PUBID)
    check('WK-10b 列表项含统计字段 documentCount/chunkCount/lastDocumentUpdateTime', !!item && typeof item.documentCount === 'number' && typeof item.chunkCount === 'number' && (item.lastDocumentUpdateTime == null || typeof item.lastDocumentUpdateTime === 'string'), JSON.stringify(item ?? null).slice(0, 160))
  }

  // WK-11 知识库最大文件大小（WIKI_MAX_FILE_SIZE_MB，超管设置，网页端与开放接口上传同时受限）
  {
    const wk11 = await api('POST', '/api/wiki', { token: alice.token, body: { teamId: TID, name: 'wk11-' + TS } })
    const WID11 = Number(wk11.json?.value)
    const rootL = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
    const rootTk = rootL.json.accessToken
    const sha256Hex = (s) => crypto.createHash('sha256').update(s).digest('hex')
    const preup = (token, fileName, fileSize, sha) => api('POST', `/api/wiki/${WID11}/documents/preupload`, {
      token,
      body: { fileName, contentType: 'text/markdown', fileSize, sha256: sha },
    })

    await api('PUT', '/api/settings', { token: rootTk, body: { key: 'WIKI_MAX_FILE_SIZE_MB', value: '1' } })
    const limit1 = await api('GET', '/api/wiki/upload-limit', { token: bob.token })
    check('WK-11a 设置 1MB 后 upload-limit 返回 1（成员可读）', limit1.status === 200 && limit1.json?.maxFileSizeMb === 1 && Number(limit1.json?.maxFileSizeBytes) === 1048576, JSON.stringify(limit1.json))
    check('WK-11b 超限 2MB preupload 400', (await preup(bob.token, `big-${TS}.md`, 2 * 1024 * 1024, sha256Hex(`big-${TS}`))).status === 400)
    check('WK-11c 恰好 1MB preupload 200', (await preup(bob.token, `ok-${TS}.md`, 1024 * 1024, sha256Hex(`ok-${TS}`))).status === 200)

    await api('PUT', '/api/settings', { token: rootTk, body: { key: 'WIKI_MAX_FILE_SIZE_MB', value: '50' } })
    const limit50 = await api('GET', '/api/wiki/upload-limit', { token: bob.token })
    const free = await preup(bob.token, `free-${TS}.md`, 2 * 1024 * 1024, sha256Hex(`free-${TS}`))
    check('WK-11d 恢复默认 50MB 后 2MB preupload 200', limit50.json?.maxFileSizeMb === 50 && Number(limit50.json?.maxFileSizeBytes) === 52428800 && free.status === 200, `${JSON.stringify(limit50.json)} ${free.status}`)
  }

  // WK-12 知识库允许上传文件类型（WIKI_ALLOWED_FILE_TYPES，超管设置，网页端与开放接口上传同时受限）
  {
    const wk12 = await api('POST', '/api/wiki', { token: alice.token, body: { teamId: TID, name: 'wk12-' + TS } })
    const WID12 = Number(wk12.json?.value)
    const rootL = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
    const rootTk = rootL.json.accessToken
    const sha256Hex = (s) => crypto.createHash('sha256').update(s).digest('hex')
    const DEFAULT_TYPES = '.pdf,.docx,.xlsx,.pptx,.html,.htm,.md,.markdown,.txt,.json'
    const preup = (fileName, sha) => api('POST', `/api/wiki/${WID12}/documents/preupload`, {
      token: bob.token,
      body: { fileName, contentType: 'application/pdf', fileSize: 10, sha256: sha },
    })
    const putTypes = async (value) => api('PUT', '/api/settings', { token: rootTk, body: { key: 'WIKI_ALLOWED_FILE_TYPES', value } })

    await putTypes(DEFAULT_TYPES)
    const dft = await api('GET', '/api/wiki/upload-limit', { token: bob.token })
    check('WK-12a 默认允许类型 upload-limit 回显（成员可读）', dft.status === 200 && Array.isArray(dft.json?.allowedFileTypes) && dft.json.allowedFileTypes.join(',') === DEFAULT_TYPES, JSON.stringify(dft.json))

    await putTypes('.md,.txt')
    const noPdf = await preup(`no-${TS}.pdf`, sha256Hex(`no-${TS}`))
    check('WK-12b 收窄后 .pdf preupload 400 且错误信息带允许清单', noPdf.status === 400 && noPdf.text.includes('.md') && noPdf.text.includes('.txt'), `${noPdf.status} ${noPdf.text.slice(0, 160)}`)

    await putTypes(DEFAULT_TYPES)
    check('WK-12c 恢复默认后 .pdf preupload 200', (await preup(`yes-${TS}.pdf`, sha256Hex(`yes-${TS}`))).status === 200)
  }

  // WK-13 文档内嵌图片附件（提取 pdf/word/excel/ppt 落出的图片资产：登记/列表/下载/页码/重提取替换/删除联动与共享保护）
  {
    const wk13 = await api('POST', '/api/wiki', { token: alice.token, body: { teamId: TID, name: 'wk13-' + TS } })
    const WID13 = Number(wk13.json?.value)

    const PNG1 = buildPng(10)
    const PNG2 = buildPng(200)
    const DOCX_TYPE = 'application/vnd.openxmlformats-officedocument.wordprocessingml.document'
    const docxBuf = buildDocxWithImages([PNG1, PNG2])
    const d1 = await uploadWikiDoc(WID13, alice.token, `imgdoc-${TS}.docx`, DOCX_TYPE, docxBuf)
    check('WK-13a 带图 docx 上传提取成功', !!d1.documentId, d1.error ?? '')

    const imgList1 = await api('GET', `/api/wiki/${WID13}/documents/${d1.documentId}/images`, { token: alice.token })
    const items1 = imgList1.json?.items ?? []
    check('WK-13b 图片列表返回 2 项且字段回显', imgList1.status === 200 && items1.length === 2
      && items1.every((x, i) => Number(x.imageIndex) === i && x.fileType === '.png' && String(x.fileName).endsWith('.png') && Number(x.fileSize) > 0 && !!x.downloadUrl && x.pageNumber == null),
    `${imgList1.status} ${JSON.stringify(items1).slice(0, 220)}`)

    const content1 = await api('GET', `/api/wiki/${WID13}/documents/${d1.documentId}/content`, { token: alice.token })
    check('WK-13c 正文不含图片引用且保留文本', content1.status === 200 && !String(content1.json?.value ?? '').includes('![') && String(content1.json?.value ?? '').includes('内嵌图片提取测试文档'), `${content1.status} ${String(content1.json?.value ?? '').slice(0, 120)}`)

    // 下载字节与源 PNG 一致（顺序不敏感比较）
    const dl0 = await api('GET', `/api/wiki/${WID13}/documents/${d1.documentId}/images/${items1[0].imageId}/download`, { token: alice.token })
    const dl1 = await api('GET', `/api/wiki/${WID13}/documents/${d1.documentId}/images/${items1[1].imageId}/download`, { token: alice.token })
    let bytesOk = false
    try {
      const b0 = Buffer.from(await (await fetch(dl0.json.value)).arrayBuffer())
      const b1 = Buffer.from(await (await fetch(dl1.json.value)).arrayBuffer())
      const got = [sha256Hex(b0), sha256Hex(b1)].sort().join(',')
      bytesOk = got === [sha256Hex(PNG1), sha256Hex(PNG2)].sort().join(',')
    } catch { bytesOk = false }
    check('WK-13d 图片下载字节与源一致', dl0.status === 200 && dl1.status === 200 && bytesOk, `dl0=${dl0.status} dl1=${dl1.status}`)

    check('WK-13e 非成员查图片列表 404', (await api('GET', `/api/wiki/${WID13}/documents/${d1.documentId}/images`, { token: outsider.token })).status === 404)
    check('WK-13f 伪造 imageId 下载 404', (await api('GET', `/api/wiki/${WID13}/documents/${d1.documentId}/images/999999/download`, { token: alice.token })).status === 404)

    // PDF 内嵌图：解析页码
    const pdfBuf = buildPdfWithImage(42)
    const d2 = await uploadWikiDoc(WID13, alice.token, `imgpdf-${TS}.pdf`, 'application/pdf', pdfBuf)
    const imgList2 = await api('GET', `/api/wiki/${WID13}/documents/${d2.documentId}/images`, { token: alice.token })
    const items2 = imgList2.json?.items ?? []
    check('WK-13g PDF 内嵌图提取且页码=1', imgList2.status === 200 && items2.length === 1 && Number(items2[0].pageNumber) === 1, `${imgList2.status} ${JSON.stringify(items2).slice(0, 220)}`)

    // 重新提取：图片登记替换不重复
    await api('POST', `/api/wiki/${WID13}/documents/${d1.documentId}/extract`, { token: alice.token })
    const imgList1b = await api('GET', `/api/wiki/${WID13}/documents/${d1.documentId}/images`, { token: alice.token })
    check('WK-13h 重新提取后图片仍为 2 项不重复', imgList1b.status === 200 && (imgList1b.json?.items ?? []).length === 2, `${imgList1b.status} len=${(imgList1b.json?.items ?? []).length}`)

    // 内嵌图片内容寻址共享：不同文档包含同一批图片 → 图片 file/objectKey 共享；删除其一，另一文档图片仍可下载
    // （同知识库同文件 preupload 会 409，故第二份文档用不同文本、相同图片字节）
    const docxBuf2 = buildDocxWithImages([PNG1, PNG2], `第二份内嵌图片文档-${TS}`)
    const d5 = await uploadWikiDoc(WID13, alice.token, `imgdoc2-${TS}.docx`, DOCX_TYPE, docxBuf2)
    const imgList5 = d5.documentId ? await api('GET', `/api/wiki/${WID13}/documents/${d5.documentId}/images`, { token: alice.token }) : { json: {} }
    const items5 = imgList5.json?.items ?? []
    const sharedIds1 = items1.map((x) => Number(x.fileId)).sort().join(',')
    const sharedIds5 = items5.map((x) => Number(x.fileId)).sort().join(',')
    check('WK-13i 不同文档同图共享 fileId', !!d5.documentId && imgList5.status === 200 && items5.length === 2 && sharedIds1 === sharedIds5 && sharedIds1.length > 0, `d5=${d5.documentId} ${d5.error ?? ''} ${sharedIds1} vs ${sharedIds5}`)
    await api('DELETE', `/api/wiki/${WID13}/documents`, { token: alice.token, body: { documentIds: [d1.documentId] } })
    const dl5 = items5.length > 0 ? await api('GET', `/api/wiki/${WID13}/documents/${d5.documentId}/images/${items5[0].imageId}/download`, { token: alice.token }) : { status: 0 }
    let sharedBytesOk = false
    try {
      const b = Buffer.from(await (await fetch(dl5.json.value)).arrayBuffer())
      sharedBytesOk = [sha256Hex(PNG1), sha256Hex(PNG2)].includes(sha256Hex(b))
    } catch { sharedBytesOk = false }
    check('WK-13j 删除共享文档后另一文档图片仍可下载', dl5.status === 200 && sharedBytesOk, `dl5=${dl5.status}`)
    check('WK-13k 删除文档后其图片列表 404', (await api('GET', `/api/wiki/${WID13}/documents/${d1.documentId}/images`, { token: alice.token })).status === 404)

    // 无图文档：图片列表为空数组
    const mdBuf = Buffer.from(`# 无图文档-${TS}\n\n纯文本内容。`, 'utf8')
    const d4 = await uploadWikiDoc(WID13, alice.token, `nomedia-${TS}.md`, 'text/markdown', mdBuf)
    const imgList4 = await api('GET', `/api/wiki/${WID13}/documents/${d4.documentId}/images`, { token: alice.token })
    check('WK-13l 无图文档图片列表为空数组', imgList4.status === 200 && Array.isArray(imgList4.json?.items) && imgList4.json.items.length === 0, `${imgList4.status} ${JSON.stringify(imgList4.json).slice(0, 120)}`)
  }

  console.log(`\n===== 知识库 E2E 汇总: PASS=${PASS} FAIL=${FAIL} =====`)
  process.exit(FAIL > 0 ? 1 : 0)
}

main().catch(e => { console.error('脚本异常:', e); process.exit(2) })
