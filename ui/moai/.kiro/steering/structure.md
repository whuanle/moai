# Project Structure

```
src/
├── apiClient/           # Auto-generated Kiota API client (DO NOT EDIT MANUALLY)
│   ├── api/             # API endpoint builders organized by resource
│   ├── models/          # TypeScript interfaces for API types
│   └── moAIClient.ts    # Main client factory
│
├── components/          # React components organized by feature
│   ├── admin/           # Admin panel pages (aimodel, oauth, plugin, user management)
│   ├── app/             # Main application components (AI assistant)
│   ├── common/          # Shared components (CodeEditorModal)
│   ├── dashboard/       # Dashboard and navigation
│   ├── login/           # Authentication (login, register, OAuth)
│   ├── plugin/          # Plugin management
│   ├── prompt/          # Prompt management (list, create, edit)
│   ├── user/            # User settings and OAuth binding
│   ├── wiki/            # Knowledge base management
│   │   ├── documentEmbedding/  # Document chunking and embedding
│   │   └── plugins/            # Wiki plugins (crawler, feishu)
│   └── ServiceClient.tsx       # API client factory with auth middleware
│
├── helper/              # Utility functions
│   ├── TokenHelper.tsx  # JWT token validation
│   ├── RsaHelper.tsx    # RSA encryption
│   ├── Md5Helper.tsx    # File hashing
│   └── FileTypeHelper.ts # MIME type detection
│
├── lobechat/            # Vendored lobechat model-bank for AI model metadata
│
├── stateshare/          # Global state management
│   └── store.tsx        # Zustand store (serverInfo, userInfo)
│
├── App.tsx              # Main app layout with sidebar navigation
├── PageRouter.tsx       # Route definitions aggregator
├── InitService.tsx      # Token refresh and server info initialization
├── Env.tsx              # Environment variable access
└── main.tsx             # App entry point
```

## Key Patterns

### Component Organization
- Each feature has its own folder under `components/`
- Page routers exported from `*PageRouter.tsx` files
- Custom hooks extracted for reusable logic

### API Calls
- Always use `GetApiClient()` from `ServiceClient.tsx`
- Use `GetAllowApiClient()` for unauthenticated endpoints
- API client handles 401 redirects automatically

### State Access
- Use `useAppStore` hook for reactive state
- Use `useAppStore.getState()` for non-reactive access
- User/server info persisted to localStorage

### Routing
- Nested routes defined in feature-specific router files
- Aggregated in `PageRouter.tsx`
- Protected routes check token in `App.tsx` useEffect
