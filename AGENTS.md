# Asystent UDIP — guide for contributors and AI agents

Internal tool for Bydgoszcz City Hall clerks handling requests for access to public information
(UDIP). It keeps a register/calendar of requests with statutory deadlines, finds similar past cases,
suggests departments, drafts responses from department replies and proposes anonymisation.
The program never sends anything by itself and the clerk always makes the final decision.

## Layout

```
backend/                     .NET 10 (LTS) minimal API, EF Core + PostgreSQL (pgvector, pg_trgm)
  src/ClerkAssistant.Api/
    Domain/                  entities, enums, deadline rules (KPA art. 57, UDIP art. 13)
    Features/<Feature>/      vertical slices: <Feature>Contracts.cs, <Feature>Endpoints.cs, services
    Infrastructure/          persistence, security, documents, AI clients, seeding, shared helpers
    Seed/                    reference data (departments from BIP)
  tests/ClerkAssistant.Api.Tests/
frontend/                    React + TypeScript (Vite), React Router data router, Chakra UI v3, TanStack Query
docker-compose.yml           postgres (pgvector) + app (+ optional Ollama profile)
```

## Commands

```
cd backend && dotnet build && dotnet test
cd backend && dotnet ef migrations add <Name> -p src/ClerkAssistant.Api -o Infrastructure/Persistence/Migrations
cd backend/src/ClerkAssistant.Api && dotnet run --launch-profile http     # http://localhost:5080
cd frontend && npm run dev                                                 # http://localhost:5173, proxies /api
cd frontend && npm run lint && npm run typecheck && npm test
```

Development database: `Host=localhost;Database=udip;Username=udip;Password=udip_dev_password`
(extensions `vector`, `pg_trgm`, `unaccent` must exist; `docker compose up db` provides them).
Development users are seeded when `Seed:DemoData` is true (see `appsettings.Development.json`).

## Backend conventions

- One feature = one folder under `Features/`. Endpoints are static `Map<Feature>Endpoints` extension
  methods on the `/api` route group; services are registered in `Add<Feature>Feature`.
- Contracts are C# records in `<Feature>Contracts.cs`; validation uses DataAnnotations (`AddValidation`).
- Use `TypedResults` and `Results<...>` return types. Errors are ProblemDetails with Polish messages
  (they are shown to clerks). Code, identifiers and logs stay in English.
- Every `/api` endpoint requires an authenticated user by default; admin endpoints use
  `.RequireAuthorization(Roles.Admin)`. Mutating requests are CSRF-protected by `AntiforgeryFilter`.
- Write changes worth tracing to `AuditLog.Record(...)` and save them in the same `SaveChangesAsync`.
- Optimistic concurrency: `InfoRequest.Version` / `ResponseDraft.Version` map to `xmin`; clients send
  the version back, a conflict becomes HTTP 409 via `ConcurrencyExceptionHandler`.
- Dates of the register are `DateOnly` in Europe/Warsaw (`AppClock.Today`). Never use `DateTime.Now`.
- Use Postgres for everything it can do: full-text search (`search_vector`, config `simple`),
  trigram similarity (`pg_trgm`), vectors (`pgvector`), files (`bytea`). Do not add external services.
- AI goes through `AiClients` (OpenAI-compatible: OpenAI or Ollama). Always handle `IsEnabled == false`
  and AI failures with a deterministic fallback plus a warning; AI output is a suggestion only.
- Treat uploaded documents and AI output as untrusted: never execute or render them as HTML, never
  log their content, wrap them in clear delimiters in prompts.
- No comments that restate the code. Prefer small, well-named methods.

## Frontend conventions

- Polish UI copy, sentence case. Domain labels live in `src/lib/labels.ts`.
- Feature folders under `src/features/<feature>/` (pages, components, `api.ts` with TanStack Query hooks).
  Shared building blocks live in `src/components/`.
- All HTTP goes through `src/api/client.ts` (adds the CSRF header, maps ProblemDetails to `ApiError`).
- Chakra UI v3 only (no v2 APIs). Use semantic tokens from `src/theme` instead of raw colours.
  The Chakra MCP server (`.mcp.json`) documents v3 components.
- Accessibility is a requirement (WCAG 2.1 AA): labelled form fields, visible focus, keyboard access,
  status never conveyed by colour alone, `aria-live` for async results, contrast >= 4.5:1.
