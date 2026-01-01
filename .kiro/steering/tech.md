# Technology Stack

## Backend (.NET 9)

- Framework: ASP.NET Core 9 with FastEndpoints
- Language: C# 12 with nullable reference types enabled
- ORM: Entity Framework Core 9 (MySQL, PostgreSQL support)
- CQRS: MediatR for command/query separation
- Validation: FluentValidation
- AI Integration: Microsoft Semantic Kernel, Kernel Memory
- Authentication: JWT Bearer tokens
- Logging: Serilog
- API Documentation: OpenAPI/Swagger with Scalar UI
- Messaging: RabbitMQ (optional, can use in-memory)
- Caching: Redis (StackExchange.Redis)
- Module System: Maomi.Core for dependency injection modules
- Code Analysis: StyleCop.Analyzers

## Frontend (React 19)

- Build: Vite 6
- Language: TypeScript 5.7
- UI: Ant Design 5, LobeHub UI
- State: Redux Toolkit, Zustand
- API Client: Microsoft Kiota (auto-generated from OpenAPI)
- Routing: React Router 7
- Editor: Monaco Editor, Vditor (Markdown)

## Common Commands

```bash
# Backend - Build solution
dotnet build MoAI.sln

# Backend - Run API (from src/MoAI)
dotnet run --project src/MoAI/MoAI.csproj

# Frontend - Install dependencies (from ui/moai)
npm install

# Frontend - Development server
npm run dev

# Frontend - Build for production
npm run build

# Frontend - Sync API client from backend OpenAPI
npm run syncapi

# Docker - Full stack deployment
docker-compose up -d
```

## Configuration

- Backend config via `MAI_CONFIG` env var pointing to JSON/YAML file
- Default config: `configs/system.yaml`
- Logging config: `configs/logger.json`
- Default port: 8080 (backend), 3000 (frontend)
