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
    Features/Assistant/      in-app assistant: SSE chat, tools, proposals confirmed by the clerk;
                             AppGuide.md (embedded) is the app knowledge in every prompt — keep it current
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

Layering inside a feature folder (`Features/<Feature>/`), see `Features/Departments` and `Features/Users`
for the reference implementation:

| File | Responsibility |
|---|---|
| `<Feature>Contracts.cs` | Request/response records (the API contract). DataAnnotations for input validation. |
| `<Feature>Endpoints.cs` | `Add<Feature>Feature` (DI registration) and `Map<Feature>Endpoints`. Endpoints only bind input, call a service and wrap the result (`TypedResults.Created/NoContent/File`). No queries, no rules, no mapping. |
| `<Name>Service.cs` | Business logic and data access (EF Core). Returns contract DTOs. Signals expected failures with `NotFoundException`, `ValidationException` (field → Polish message), `ConflictException`, `AuthenticationFailedException`; `ApiExceptionHandler` turns them into ProblemDetails. |
| `<Name>Mapper.cs` | Mapperly (`[Mapper] static partial class`) entity → DTO mappings. Values computed by a service (e.g. urgency, flags) are passed as extra mapping parameters. Every target member must be mapped (enforced at build time). |

- Never hand-write `new SomeDto(entity.A, entity.B, …)` in endpoints or services — add a mapper method.
- Contracts validation uses DataAnnotations (`AddValidation`); cross-field rules live in services (`ValidationErrors.ThrowIfAny()`).
- Error messages shown to clerks are Polish. Code, identifiers and logs stay in English.
- Every `/api` endpoint requires an authenticated user by default; admin endpoints use
  `.RequireAuthorization(Roles.Admin)`. Mutating requests are CSRF-protected by `AntiforgeryFilter`.
- Write changes worth tracing to `AuditLog.Record(...)` and save them in the same `SaveChangesAsync`.
- Optimistic concurrency: `InfoRequest.Version` / `ResponseDraft.Version` map to `xmin`; clients send
  the version back, a conflict becomes HTTP 409. Background jobs must not update these rows
  (embeddings live in `request_embeddings` for that reason).
- Dates of the register are `DateOnly` in Europe/Warsaw (`AppClock.Today`). Never use `DateTime.Now`.
- Use Postgres for everything it can do: full-text search (`search_vector`, config `simple`),
  trigram similarity (`pg_trgm`), vectors (`pgvector`), files (`bytea`). Do not add external services.
- AI goes through `AiGateway`/`AiClients` (OpenAI-compatible: OpenAI or Ollama). Always handle AI being
  disabled or failing with a deterministic fallback plus a warning; AI output is a suggestion only.
- Treat uploaded documents and AI output as untrusted: never execute or render them as HTML, never
  log their content, wrap them in clear delimiters in prompts (`PromptText`).
- Letters follow the city hall's format; build headers, salutations and footers with `LetterLayout`.
- No comments that restate the code. Prefer small, well-named methods.

## Frontend conventions

- Polish UI copy, sentence case. Domain labels live in `src/lib/labels.ts`.
- Feature folders under `src/features/<feature>/` (pages, components, `api.ts` with TanStack Query hooks).
  Shared building blocks live in `src/components/`.
- All HTTP goes through `src/api/client.ts` (adds the CSRF header, maps ProblemDetails to `ApiError`).
- Chakra UI v3 only (no v2 APIs). Use semantic tokens from `src/theme` instead of raw colours.
  The Chakra MCP server (`.mcp.json`) documents v3 components.
- Buttons (one system everywhere; buttons shown side by side always share size):
  - size `md` (default): page header actions, form footers, dialog footers, a panel's main call to action;
    `sm`: actions in panel headers, toolbars and inline forms; `xs`: per-row actions in tables and lists;
    `lg`: only next to `lg` inputs (login form, archive search).
  - variant `solid` for the single primary action of a region, `outline` for secondary, `ghost` for low-emphasis,
    `LinkButton` (`src/components/common/LinkButton.tsx`) for link-like actions ("Pokaż więcej").
    No colour palettes on buttons except `red` for destructive actions.
- Dates: always `DateInput` (`src/components/common/DateInput.tsx`, Chakra DatePicker, Polish locale, `dd.mm.rrrr`),
  never `<input type="date">`; pass `portalled={false}` inside dialogs.
- Forms never use native browser validation: `noValidate` on the `<form>` (dialogs use `DialogForm`), rules in the
  submit handler with `collectErrors`/`useFieldErrors` from `src/lib/validation.ts`, then `focusFirstInvalid`.
  Show messages with `FieldErrorText` (icon + text) inside a `Field.Root invalid`; don't disable submit buttons to validate.
- Segmented controls (`SegmentGroup`), inputs, selects and checkboxes get their look from the theme
  (`src/theme/index.ts`) — don't restyle them per screen.
- Accessibility is a requirement (WCAG 2.1 AA): labelled form fields, visible focus, keyboard access,
  status never conveyed by colour alone, `aria-live` for async results, contrast >= 4.5:1.
