# Artefacts

A catalogue for a private or corporate art collection: artefact records with imprecise historical dates, acquisition (including price), provenance by region and period, images (TIFF masters included), and a print preview that produces a clean paper record of any artefact. See [PRODUCT.md](PRODUCT.md) for who it is for and why.

| Part | Technology |
|---|---|
| API | .NET 10, Minimal APIs, EF Core on PostgreSQL. Layered: Domain → Application → Infrastructure → Api |
| Read side | PostgreSQL views (`v_artefact`) mapped to a no-tracking `ReadDbContext`, described by a field catalog (`GET /api/catalog`) |
| Media | Originals in S3-compatible storage (SeaweedFS in development); resized on the fly by imgproxy through signed URLs |
| Frontend | React + TypeScript (Vite). Pages are composed of widgets from JSON-serialisable page definitions; the API client is generated from the OpenAPI document |
| Print | Paged.js lays the record out on real pages (A4/Letter, portrait/landscape) with running headers and "page N of M" |

The design and its decisions are in [`openspec/changes/foundation/`](openspec/changes/foundation/) (`design.md`, `specs/`, `tasks.md`).

## Prerequisites

- **Docker** (Docker Desktop on Windows/macOS), for PostgreSQL, SeaweedFS and imgproxy. The backend integration tests also start containers through Testcontainers.
- **.NET SDK 10.0.400** or a later 10.0 feature band (pinned in `backend/global.json`).
- **Node.js 22.12 or later** (developed on Node 24), for the frontend and the seed script.

## Quick start

From the repository root:

```bash
# 1. Start PostgreSQL, SeaweedFS (S3), the bucket setup and imgproxy; waits until all are healthy.
docker compose -f docker/docker-compose.yml up -d --wait

# 2. Start the API on http://localhost:5032. In Development it applies database migrations on startup.
dotnet run --project backend/src/Artefacts.Api

# 3. In a second terminal: start the frontend on http://localhost:5173 (it proxies /api and /graphql to the API).
cd frontend
npm install
npm run dev

# 4. Optional, in a third terminal: load a sample collection (30 artefacts, 31 regions, 32 images).
node scripts/seed/seed.mjs
```

Open http://localhost:5173. The API reference (Scalar) is at http://localhost:5032/scalar/v1 and the OpenAPI document at http://localhost:5032/openapi/v1.json.

Development settings live in `backend/src/Artefacts.Api/appsettings.Development.json` and `docker/.env.example`. Every compose value has a default, so a `docker/.env` is only needed to change ports or credentials. The imgproxy key and salt must match on both sides. All of these credentials are for local development only.

## Everyday commands

| Task | Command |
|---|---|
| Stop the stack (keeps data) | `docker compose -f docker/docker-compose.yml down` |
| Reset all data (database and images) | `docker compose -f docker/docker-compose.yml down -v`, then start again and re-seed |
| Backend tests (needs Docker running) | `cd backend && dotnet test` |
| Frontend tests | `cd frontend && npm test` |
| Frontend typecheck + production build | `cd frontend && npm run build` |
| Frontend lint | `cd frontend && npm run lint` |
| Regenerate the typed API client after an API change | `cd backend && dotnet build`, then `cd frontend && npm run api:generate` |
| Add a migration | `cd backend && dotnet tool restore && dotnet ef migrations add <Name> --context CollectionDbContext --project src/Artefacts.Infrastructure --startup-project src/Artefacts.Api` |
| Apply migrations without starting the API | the same `dotnet ef` options with `database update` |
| Validate the specs | `openspec validate foundation --strict` |

The seed script is safe to rerun: regions are matched by name, and artefacts whose inventory number already exists are skipped. Point it at another API with `--api <url>` or `ARTEFACTS_API_URL`. The top of `scripts/seed/data.mjs` lists the records that exist to exercise particular cases (a provenance table long enough to split across pages, a 26 MB TIFF master, a multi-page description, records without images or dates).

## Repository layout

```
backend/
  src/Artefacts.Domain          Aggregates (Artefact, Region, MediaAsset), HistoricalDate parsing, Money. No dependencies.
  src/Artefacts.Application     Command/query handlers, DTOs, ports (repositories, media storage, image URLs).
  src/Artefacts.Infrastructure  EF Core (write and read contexts, migrations, views), S3/file-system storage, imgproxy URLs.
  src/Artefacts.Api             Endpoints, ProblemDetails mapping, OpenAPI. Writes backend/openapi/Artefacts.Api.json at build time.
  tests/                        Domain and Application unit tests; API integration tests (Testcontainers PostgreSQL and SeaweedFS).
frontend/src/
  api/                          Generated OpenAPI types and the typed client.
  widgets/core/                 Widget registry, binding paths and validation, PageRenderer.
  widgets/builtin/              Display widgets (image, gallery, table, field list, …) and action widgets (artefact form, image manager).
  pages/definitions/            Pages as data: list, detail, edit, print.
  print/                        Print preview: Paged.js integration and print CSS.
docker/                         Compose stack and SeaweedFS S3 identities.
scripts/seed/                   Sample-collection seed (dependency-free Node).
openspec/                       Specifications and change proposals.
```

## Troubleshooting

- **Upload or image URLs fail with "No RegionEndpoint or ServiceURL configured".** The API is using S3 storage without its settings. Run it in Development (the default `dotnet run` profile), or provide the `Storage:S3:*` settings.
- **The API exits at startup listing missing settings.** Configuration is validated on start: set the named values (`ConnectionStrings:Collection`, `Storage:*`, `Imgproxy:*`), usually by running in Development.
- **Images do not show (404 from imgproxy).** imgproxy runs with `IMGPROXY_ONLY_PRESETS`, so URLs must use the bare preset form `/{signature}/web/plain/...`, and the key and salt in `appsettings.Development.json` must match those in compose.
- **`dotnet test` fails in the integration tests with container errors.** Docker is not running; start Docker Desktop and rerun.
- **`dotnet test` reports "Zero tests ran" and fails.** Every test project must contain at least one test, because an empty test module fails the whole run under Microsoft.Testing.Platform.
- **`dotnet ef` complains that more than one DbContext was found.** Pass `--context CollectionDbContext` (the read context maps views and has no migrations).
- **`npm install` exits with code 1 without an error, or the first `dotnet build` fails inside MSBuild (Windows).** The checkout is too deep for the 260-character path limit. Clone to a shorter path (for example `C:\srcrtefacts`), or enable long paths (`git config --system core.longpaths true` and the `LongPathsEnabled` Windows setting).
- **Port already in use.** Change `POSTGRES_PORT`, `S3_PORT` or `IMGPROXY_PORT` in `docker/.env`, and the matching URLs in `appsettings.Development.json`.

## Work tracking

Issues are tracked with [beads](https://github.com/gastownhall/beads) (`bd ready`, `bd show <id>`); see `CLAUDE.md`. The current implementation plan and its status are in `openspec/changes/foundation/tasks.md`.
