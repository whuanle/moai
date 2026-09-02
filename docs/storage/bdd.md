# 文件存储（Storage，STO）行为规格（BDD，Gherkin）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md)（场景→验证映射） ｜ [SOP](./sop.md)（操作与验收流程）
> 编号规则与标签语义见 [../DOC-STANDARD.md](../DOC-STANDARD.md) 第 3 节。

```gherkin
Feature: 图片预上传
  Background:
    Given 用户已登录
    And 对象存储正常运行（本地 MinIO，桶 moai 存在）

  @STO-S1 @auto:e2e
  Scenario: 首次预上传新图片
    When 提交 { fileName, contentType, fileSize, sha256 }
    Then 返回 isExist=false、fileId、objectKey="public/images/{sha256}.{ext}"
    And uploadUrl 为指向对象存储的预签名 PUT 地址（5 分钟有效）

  @STO-S2 @auto:e2e
  Scenario: 相同内容再次预上传命中秒传
    Given 该 objectKey 已上传完成
    When 以相同 sha256+fileName 再次预上传
    Then 返回 isExist=true 且复用同一 fileId
    And 不签发 uploadUrl、不新增记录

  @STO-S3 @auto:e2e
  Scenario: 字段校验失败
    When 提交 fileSize=0（或 sha256 为空、fileName 为空）
    Then 返回请求错误（400）且提示对应校验消息

  @STO-S4 @manual
  Scenario: 未登录预上传被拒
    When 不带 token 请求预上传
    Then 返回未认证（401）

Feature: 临时文件预上传
  @STO-S5 @manual
  Scenario: 预上传临时文件
    When 已登录用户提交 { fileName, contentType, fileSize, sha256 }
    Then 返回 objectKey="temp/{sha256}.{ext}"
    And 该 key 不带 public 前缀，后续不能通过 /static 访问

Feature: 浏览器直传与完成上传
  Background:
    Given 用户已登录并取得预签名 uploadUrl

  @STO-S6 @auto:e2e
  Scenario: 浏览器直传预签名地址
    When 以 PUT 携带正确 Content-Type 上传文件内容至 uploadUrl（不带登录态）
    Then 对象存储写入成功

  @STO-S7 @auto:e2e
  Scenario: 正常完成上传
    When 提交完成上传 { fileId, isSuccess: true }
    Then 返回与预上传一致的 objectKey 且记录标记为已上传
    And 公开文件的 accessUrl 指向 /static 地址

  @STO-S8 @manual
  Scenario: 私有文件完成上传不返回静态地址
    Given 临时文件（temp/ 前缀）已完成直传
    When 提交完成上传
    Then accessUrl 为空

  @STO-S9 @manual
  Scenario: 上传失败清理
    Given 预上传后未直传（或直传失败）
    When 提交完成上传 { fileId, isSuccess: false }
    Then 操作成功
    And 登记记录被删除、对象存储残留对象被清理

  @STO-S10 @manual
  Scenario: 文件损坏校验
    Given 直传内容与登记的 fileSize 不一致（或未直传）
    When 提交完成上传 { fileId, isSuccess: true }
    Then 返回冲突（409）"上传的文件已损坏"
    And 记录与残留对象被清理

  @STO-S11 @manual
  Scenario: 完成不存在的文件
    When 提交完成上传 { fileId: 99999 }
    Then 返回不存在（404）"文件不存在"

  @STO-S12 @manual
  Scenario: 重复完成幂等
    Given 该 fileId 已标记上传完成
    When 再次提交完成上传
    Then 操作成功且 objectKey 不变（忽略请求）

  @STO-S13 @manual
  Scenario: 其他用户抢占同一文件
    Given 用户 B 的预上传记录未完成且未过期
    When 用户 A 提交完成上传该 fileId
    Then 返回冲突（409）"其他用户正在上传此文件"

  @STO-S14 @manual
  Scenario: 过期记录被废弃
    Given 某预上传记录超过 5 分钟未完成
    When 其他用户对同一 objectKey 预上传或完成
    Then 旧记录被废弃（对象与记录清理）后重建

Feature: 公开静态访问（/static/{objectKey}）
  @STO-S15 @auto:e2e
  Scenario: 免登录访问公开文件
    Given objectKey="public/images/{sha}.png" 已上传完成
    When 匿名 GET /static/public/images/{sha}.png
    Then 返回 200 且响应体与上传内容字节一致
    And Content-Type 为登记值，Cache-Control=public,max-age=86400

  @STO-S16 @auto:e2e
  Scenario: 非公开目录被拒绝
    When GET /static/temp/{sha}.png
    Then 返回不存在（404）（仅放行 public/ 前缀的安全边界）

  @STO-S17 @manual
  Scenario: 未完成或不存在的文件不可访问
    Given 该 objectKey 的记录未完成上传（或不存在）
    When GET /static/{objectKey}
    Then 返回不存在（404）

  @STO-S18 @manual
  Scenario: 非 GET/HEAD 方法被拒绝
    When POST /static/public/images/{sha}.png
    Then 返回方法不允许（405）

Feature: 领域服务（其他模块视角，非 HTTP）
  @STO-S19 @manual
  Scenario: 服务端流式上传
    When 模块调用 UploadStreamAsync({ Stream, ContentType, FileSize, SHA256, ObjectKey })
    Then 对象写入成功且记录直接标记已上传
    And 重复调用同一 ObjectKey 幂等复用

  @STO-S20 @manual
  Scenario: 生成下载地址
    When 模块调用 GetDownloadUrlAsync(objectKey, fileName, expiry)
    Then 返回对象存储预签名 GET 地址（有效期 expiry）

  @STO-S21 @manual
  Scenario: 删除文件
    When 模块调用 DeleteFilesAsync(fileIds)
    Then 登记记录被删除且对象被清理

Feature: 前端上传链路（头像 / 图标）
  @STO-S22 @manual
  Scenario: 用户更换头像
    Given 用户在「账号设置」点击头像上传并选择本地图片
    Then 前端完成 预上传→直传→完成→提交 objectKey 给账号接口
    And 页面头像刷新为 {server}/static/public/images/{sha256}.png

  @STO-S23 @manual
  Scenario: 绝对 URL 与 ObjectKey 兼容展示
    Given 数据库字段存的可能是 http URL 或 ObjectKey
    When 页面渲染 resolveStorageUrl(值)
    Then 绝对 URL 原样展示；ObjectKey 拼接为 /static 地址

  @STO-S24 @manual
  Scenario: 上传失败提示
    When 直传或完成接口失败
    Then 页面就地反馈上传失败错误
```
