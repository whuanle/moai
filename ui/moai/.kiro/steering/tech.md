# Tech Stack

## Core Framework
- React 19 with TypeScript
- Vite 6 (build tool, dev server on port 4000)

## UI Libraries
- Ant Design 5 (primary UI components)
- @lobehub/ui (theming, AI-related components)
- Monaco Editor (code editing)
- Vditor (markdown editing)

## State Management
- Zustand (global state)
- Redux Toolkit (available but Zustand preferred)
- localStorage for persistence (tokens, server info)

## Routing
- React Router 7

## API Client
- Microsoft Kiota (auto-generated TypeScript client from OpenAPI)
- Client located in `src/apiClient/`
- Bearer token authentication with auto-refresh

## Key Dependencies
- jwt-decode (token handling)
- jsencrypt (RSA encryption for passwords)
- spark-md5 (file hashing)
- zod (schema validation)
- react-markdown (markdown rendering)

## Common Commands

```bash
# Development
npm run dev          # Start dev server (port 4000)

# Build
npm run build        # TypeScript check + Vite build
npm run build:strict # Strict TypeScript build
npm run build:docker # Docker-optimized build

# Linting
npm run lint         # Run ESLint
npm run lint:fix     # Auto-fix lint issues

# API Client Generation
npm run syncapi      # Regenerate Kiota client from OpenAPI spec
```

## Environment Variables
- `VITE_ServerUrl` - Backend API URL (configured in `.env` or `.env.local`)

## Docker Support
- `Dockerfile` / `Dockerfile.dev` available
- `docker-compose.yml` for orchestration
- nginx.conf for production serving
