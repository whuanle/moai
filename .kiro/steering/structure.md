# Project Structure

## Solution Organization

```
MoAI.sln                    # Main solution file
src/                        # Backend source code
├── MoAI/                   # Main API host project (entry point)
├── admin/                  # Admin management module
├── ai/                     # AI integration (Semantic Kernel, providers)
├── aimodel/                # AI model configuration management
├── ai_app/                 # AI applications (assistant, etc.)
├── common/                 # Shared utilities
├── database/               # Database context and entities
├── infra/                  # Infrastructure (config, external HTTP, shared)
├── login/                  # Authentication module
├── plugin/                 # Plugin system (native, custom, tools)
├── prompt/                 # Prompt template management
├── storage/                # File storage (local, S3)
├── team/                   # Team management
├── user/                   # User management
├── wiki/                   # Knowledge base module
└── workflow/               # Workflow automation
ui/moai/                    # React frontend
configs/                    # Configuration templates
tool/                       # Database scaffolding tools
```

## Module Pattern (per domain)

Each domain follows a three-layer pattern:
- `*.Shared` - DTOs, Commands, Queries, Models (no dependencies)
- `*.Core` - Business logic, Handlers, Services
- `*.Api` - Controllers/Endpoints (depends on Core)

Example: `MoAI.User.Shared` → `MoAI.User.Core` → `MoAI.User.Api`

## Key Conventions

- Modules registered via `[InjectModule<T>]` attributes in `*Module.cs` files
- Commands/Queries use MediatR `IRequest<TResponse>` pattern
- Handlers implement `IRequestHandler<TRequest, TResponse>`
- Controllers use `[ApiController]` with route prefix `/api`
- Entity classes suffixed with `Entity` in `Database.Shared/Entities`
- Database context: `DatabaseContext` with DbSet properties
- Central package management via `Directory.Packages.props`
- Build settings via `Directory.Build.props`
