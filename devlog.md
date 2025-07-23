

### CQRS 、API 要求

* 命令使用 `{X}Command` 结尾，响应结果尽量使用 `{X}CommandResponse` 结尾，部分无特殊结构，可使用 SimpleInt、EmptyCommandResponse 等。

* 如果是集合或分页，使用 CommandResponse 结尾包装 `IReadonlyCollection<T>`，集合中的 T 建议单独抽象模型，因为复用概率比较高，示例：

  > ```csharp
  > public class QueryAllOAuthPrividerDetailCommandResponse
  > {
  >     /// <summary>
  >     /// 列表.
  >     /// </summary>
  >     public IReadOnlyCollection<OAuthPrividerDetailModel> Items { get; init; } = Array.Empty<OAuthPrividerDetailModel>();
  > }
  > ```

* 有些只能用于内部使用、不允许 Api 使用的作为模型类的 Command，需要加上 Internal，以便区分限制，不过依然可以被其它模块使用，只是不能出现在 Api 里面，命名：`Internal{X}Command`。

* 查询类的命名统一使用 `Query{X}Command` 做前缀，命令类使用动词做前缀如 `SetUserStateCommand`。

* Handler 使用 `{X}CommandHandler` 结尾。

* Endpoint 使用 `{X}Endpoint`，与 `{X}Command` 抱持一致。

* `.Core` 项目不可以直接使用 UserContext ，如果查询或命令需要知道用户 id，则需要 Command 传入，起名示例：

  > ```
  > Query{X}ByUserIdCommand
  > ```
  
* Endpoint 可以单独做自己的模型，起名 `{X}Request`，例如查询需要根据用户 id 判断能够查询的数据，但是不能让用户传递 UserId，Endpoint 注入 UserContext 获取当前用户 id，然后传递给 Command。
  
  > ```
  > public class QueryWikiBaseListRequest
  > {
  > }
  > public class QueryWikiBaseListByUserIdCommand : IRequest<T>
  > {
  >     public int UserId{ get; init; }
  > }
  > ```
  
* MQ 的事件模型使用 `{X}Message` 命名。

* API 路径命名不使用 Rest API 命名规则，删除资源统一使用 `[HttpDelete]`，其它查询、操作根据情况使用 `[HttpGet]`、`[HttpPost]`，不必使用 `[HttpPut]`。





### 数据库要求

* 一般标题 20 字符，考虑比较长 50、100，描述 255，其它长文本 1000、2000，可变字符串请勿设置太长，避免使用 text 类型。
* UUID 类型使用 `binary(16)`，数据库生成器会自动对应 C# Guid 类型。





### 0.0.1 版本

- [ ] 用户注册、用户登录、token、修改密码；
- [ ] 文件上传、静态文件访问；
- [ ] 文档上传、权限隔离；
- [ ] key 加密
- [ ] 创建团队、管理团队、团队配置。
- [ ] 团队知识库管理、文件上传、分割向量、预览、存储向量；
- [ ] 团队知识库应用、管理；
- [ ] 知识库应用模板。
- [ ] 头像上传，静态文件访问、虚拟文件系统
- [ ] 文档文件下载

- [ ] 支持网页文档
- [ ] 增加飞书接入应用，从飞书中读取文档实时更新
- [ ] 文档类型上传过滤

- [ ] 支持 oss

- [ ] 知识库管理员
- [ ] 应用管理员

- [ ] 验证码注册、登录；
- [ ] i18n 支持；
- [ ] root 用户系统配置；
- [ ] 接入系统邮件；邮件通知系统；
- [ ] 从后台获取菜单显示前端
- [ ] 团队应用可以单独计费、限额、扣费通知；
- [ ] 增加哈希桶 Channel，允许自定义 Channel 数量，根据不同的团队固定分配到指定的 Channel，避免多次点击文档生成时出现并发冲突。

- [ ] 应用支持流程编排、模块定制、函数调用；

- [ ] 支持对外开放应用，应用跨域配置；

- [ ] 接入 OAuth2.0 外部账号系统
