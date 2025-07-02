# OAuthLogin 组件

## 功能描述

OAuthLogin 组件用于处理第三方OAuth登录的回调流程，支持多种登录场景：

### 主要功能

1. **URL参数解析**：从URL中解析OAuth回调参数（code、state、client_id）
2. **OAuth登录处理**：调用 `/api/account/oauth_login` 接口处理OAuth登录
3. **用户状态管理**：根据用户登录状态和绑定状态进行不同处理

### 处理流程

#### 情况1：用户已绑定OAuth账号
- 如果 `isBindUser` 为 `true`
- 直接使用返回的 `loginCommandResponse` 完成登录
- 将用户信息和token存储到浏览器
- 重定向到 `/app` 页面

#### 情况2：用户未绑定OAuth账号

##### 情况2-1：用户已登录其他账号
- 弹出确认对话框询问是否绑定当前OAuth账号
- 用户确认绑定：调用 `/api/account/oauth_bind_account` 接口
- 用户取消绑定：跳转到 `/login?oAuthBindId={oAuthBindId}` 页面

##### 情况2-2：用户未登录任何账号
- 显示注册界面，提供"一键注册"和"取消"选项
- 一键注册：调用 `/api/account/oauth_register` 接口
- 取消：跳转到 `/login` 页面

### URL参数格式

组件期望的URL格式：
```
http://127.0.0.1:4000/oauth_login?code=8eaf7720c1398f30816e&state=4d29cea370f6eba5d051&client_id=4d29cea370f6eba5d051
```

### 路由配置

组件已配置在路由中：
```typescript
{
  path: "/oauth_login",
  Component: OAuthLogin,
}
```

### 依赖组件

- `Login.tsx`：参考其登录流程和样式
- `InitPage.tsx`：使用其中的用户状态管理函数
- `ServiceClient.tsx`：使用API客户端

### 样式

组件使用 `Login.css` 样式文件，保持与登录页面一致的视觉风格。

### 错误处理

- 无效的OAuth参数：显示错误消息并跳转到登录页面
- API请求失败：显示具体错误信息
- 网络错误：显示通用错误消息

### 状态管理

组件内部管理以下状态：
- `loading`：加载状态
- `oauthParams`：解析的OAuth参数
- `oauthResponse`：OAuth登录响应
- `isUserLoggedIn`：用户登录状态 