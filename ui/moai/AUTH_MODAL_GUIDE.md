# 登录/注册弹窗组件使用指南

## 📦 组件位置
`src/components/common/AuthModal.tsx`

## ✨ 功能特性

### 1. 统一的登录/注册体验
- ✅ 使用 Tabs 切换登录和注册
- ✅ 无需页面跳转，保持用户上下文
- ✅ 支持第三方 OAuth 登录
- ✅ 表单验证和错误提示
- ✅ 响应式设计，移动端友好

### 2. 灵活的配置选项
- 可指定默认显示的 Tab（登录或注册）
- 可自定义登录成功后的跳转路径
- 支持成功回调函数
- 自动重置表单状态

### 3. 优雅的交互体验
- 平滑的动画效果
- 清晰的视觉反馈
- 加载状态提示
- 错误信息展示

## 🚀 使用方法

### 基础用法

```typescript
import { useState } from "react";
import AuthModal from "./components/common/AuthModal";

function YourComponent() {
  const [authModalOpen, setAuthModalOpen] = useState(false);
  const [authModalTab, setAuthModalTab] = useState<"login" | "register">("login");

  return (
    <>
      {/* 触发按钮 */}
      <Button onClick={() => {
        setAuthModalTab("login");
        setAuthModalOpen(true);
      }}>
        登录
      </Button>

      <Button onClick={() => {
        setAuthModalTab("register");
        setAuthModalOpen(true);
      }}>
        注册
      </Button>

      {/* 弹窗组件 */}
      <AuthModal
        open={authModalOpen}
        onClose={() => setAuthModalOpen(false)}
        defaultTab={authModalTab}
      />
    </>
  );
}
```

### 高级用法

#### 1. 指定登录成功后的跳转路径

```typescript
<AuthModal
  open={authModalOpen}
  onClose={() => setAuthModalOpen(false)}
  defaultTab="login"
  redirectPath="/app/dashboard"
/>
```

#### 2. 使用成功回调

```typescript
<AuthModal
  open={authModalOpen}
  onClose={() => setAuthModalOpen(false)}
  defaultTab="login"
  onSuccess={() => {
    console.log("登录成功");
    // 执行自定义逻辑
    fetchUserData();
    showWelcomeMessage();
  }}
/>
```

#### 3. 从 URL 参数触发

```typescript
import { useSearchParams } from "react-router";

function YourComponent() {
  const [searchParams] = useSearchParams();
  const [authModalOpen, setAuthModalOpen] = useState(false);

  useEffect(() => {
    // 如果 URL 包含 ?auth=login，自动打开登录弹窗
    if (searchParams.get("auth") === "login") {
      setAuthModalOpen(true);
    }
  }, [searchParams]);

  return (
    <AuthModal
      open={authModalOpen}
      onClose={() => setAuthModalOpen(false)}
      defaultTab="login"
    />
  );
}
```

## 📋 Props 说明

| 属性 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| `open` | `boolean` | ✅ | - | 控制弹窗显示/隐藏 |
| `onClose` | `() => void` | ✅ | - | 关闭弹窗的回调函数 |
| `defaultTab` | `"login" \| "register"` | ❌ | `"login"` | 默认显示的标签页 |
| `redirectPath` | `string` | ❌ | `"/app"` | 登录成功后的跳转路径 |
| `onSuccess` | `() => void` | ❌ | - | 登录/注册成功的回调函数 |

## 🎨 样式定制

### 修改主题色

在 `AuthModal.css` 中修改：

```css
/* 修改主色调 */
.auth-tabs .ant-tabs-ink-bar {
  background: linear-gradient(90deg, #your-color 0%, #your-color-light 100%);
}

.auth-input:focus {
  border-color: #your-color;
  box-shadow: 0 0 0 2px rgba(your-rgb, 0.1);
}
```

### 修改弹窗尺寸

```typescript
<Modal
  width={600}  // 修改宽度
  // ...其他属性
>
```

## 🔧 与现有登录页面的关系

### 保留原有登录页面的原因

1. **SEO 友好**: 独立的登录页面有独立的 URL，利于搜索引擎收录
2. **直接访问**: 用户可以直接访问 `/login` 路径
3. **兼容性**: 某些第三方服务可能需要重定向到独立页面
4. **备用方案**: 如果弹窗出现问题，仍有备用方案

### 推荐使用场景

| 场景 | 推荐方式 | 原因 |
|------|----------|------|
| 首页快速登录 | 弹窗 | 不打断用户浏览 |
| 需要登录才能访问的页面 | 弹窗 | 保持页面上下文 |
| 直接访问 `/login` | 独立页面 | 用户明确意图 |
| OAuth 回调 | 独立页面 | 第三方服务要求 |
| 移动端 | 弹窗优先 | 更好的体验 |

## 📱 响应式设计

组件已针对不同屏幕尺寸优化：

- **桌面端** (>768px): 480px 宽度，居中显示
- **平板端** (576-768px): 自适应宽度
- **移动端** (<576px): 全屏宽度，优化触摸交互

## 🐛 常见问题

### 1. 弹窗打开后表单有旧数据？

**解决方案**: 组件已自动处理，使用 `destroyOnClose` 和表单重置。

### 2. 登录成功后页面没有更新？

**解决方案**: 使用 `onSuccess` 回调刷新页面或重新获取数据：

```typescript
<AuthModal
  onSuccess={() => {
    window.location.reload(); // 简单粗暴
    // 或
    refetchUserData(); // 优雅方式
  }}
/>
```

### 3. 如何在弹窗中显示自定义错误？

**解决方案**: 组件已集成错误处理，后端返回的错误会自动显示。

### 4. 第三方登录按钮不显示？

**检查**: 确保后端 API `/api/account/oauth_prividers` 返回了数据。

## 🎯 最佳实践

### 1. 统一的触发方式

创建一个全局的认证状态管理：

```typescript
// src/hooks/useAuth.ts
import { create } from 'zustand';

interface AuthModalState {
  isOpen: boolean;
  tab: 'login' | 'register';
  openLogin: () => void;
  openRegister: () => void;
  close: () => void;
}

export const useAuthModal = create<AuthModalState>((set) => ({
  isOpen: false,
  tab: 'login',
  openLogin: () => set({ isOpen: true, tab: 'login' }),
  openRegister: () => set({ isOpen: true, tab: 'register' }),
  close: () => set({ isOpen: false }),
}));
```

使用：

```typescript
import { useAuthModal } from './hooks/useAuth';

function AnyComponent() {
  const { openLogin, openRegister } = useAuthModal();

  return (
    <>
      <Button onClick={openLogin}>登录</Button>
      <Button onClick={openRegister}>注册</Button>
    </>
  );
}

// 在 App.tsx 中统一放置弹窗
function App() {
  const { isOpen, tab, close } = useAuthModal();

  return (
    <>
      <AuthModal open={isOpen} defaultTab={tab} onClose={close} />
      {/* 其他内容 */}
    </>
  );
}
```

### 2. 权限拦截

在需要登录的操作前自动打开弹窗：

```typescript
function ProtectedAction() {
  const { openLogin } = useAuthModal();
  const isLoggedIn = useAppStore((state) => !!state.getUserInfo());

  const handleAction = () => {
    if (!isLoggedIn) {
      openLogin();
      return;
    }
    // 执行需要登录的操作
    doSomething();
  };

  return <Button onClick={handleAction}>需要登录的操作</Button>;
}
```

## 📚 相关文件

- 组件: `src/components/common/AuthModal.tsx`
- 样式: `src/components/common/AuthModal.css`
- 原登录页: `src/components/login/Login.tsx`
- 原注册页: `src/components/login/Register.tsx`

## 🔄 迁移指南

### 从独立页面迁移到弹窗

**之前**:
```typescript
<Button onClick={() => navigate("/login")}>登录</Button>
```

**之后**:
```typescript
<Button onClick={() => {
  setAuthModalTab("login");
  setAuthModalOpen(true);
}}>登录</Button>
```

## 🎉 总结

使用 `AuthModal` 组件可以：
- ✅ 提升用户体验（无需页面跳转）
- ✅ 保持页面上下文
- ✅ 统一的认证流程
- ✅ 更现代的交互方式
- ✅ 更好的移动端体验

同时保留原有登录页面作为备用方案，确保兼容性和 SEO。
