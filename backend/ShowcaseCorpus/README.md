# Showcase corpus and startup import

This directory contains two official AMRO reports in Vietnamese:

- `AFSR2024_Highlights_Vietnamese.pdf`: ASEAN+3 Financial Stability Report 2024 highlights, 2 pages.
- `Highlights-Booklet_Vietnamese.pdf`: ASEAN+3 Regional Economic Outlook 2024 highlights, 4 pages.

The original PDFs are also the canonical evidence; no conversion is needed.
The manifest contains 32 chunks extracted by the existing PdfPig/chunker services and real
768-dimensional `gemini-embedding-2` embeddings. No random/mock embeddings are used.

The API publish includes this bundle. Startup import performs no Gotenberg or Gemini calls.
Chat still needs Gemini at request time, including query embeddings. Changing the embedding model
requires a compatible corpus; equal dimensions alone are insufficient.

## Enable

For local API configuration, set `Showcase__Enabled=true`, `Showcase__SeedOnStartup=true`,
and `Showcase__Password=OmniDoc-Showcase2026!` (or the same public password supplied to the frontend).
Default IDs/email are in API `appsettings.json`. `CorpusPath` resolves relative to the API binary
directory; an absolute path can override it. Apply the **existing** migrations before startup.
No new schema or migration is introduced.

For Compose, set `SHOWCASE_ENABLED=true` and `SHOWCASE_SEED_ON_STARTUP=true` in `.env`, then rebuild
backend and frontend. Compose passes the same credentials to seed configuration and frontend build
arguments. After importing, turn `SHOWCASE_SEED_ON_STARTUP=false` while keeping `SHOWCASE_ENABLED=true`
so guards remain active. Keep one API replica for this portfolio deployment.

Do not seed every replica concurrently. Unique-key conflicts make a competing importer fail rather
than overwrite data. A completed import validates evidence and skips existing records; it never
resets passwords or replaces conflicting accounts, workspaces or AMRO corpus. The `amro-2024-v1`
upgrade retires only the three known Northstar IDs marked `ShowcaseSeeder:northstar-v1` in the
configured showcase workspace, in the same database save as the AMRO import. Legacy artifact files
are deleted after commit; cleanup failures are logged. Other documents are retained. Missing/corrupt artifacts
require deliberate repair. Files written by a failed import are cleaned up where possible; failures
are logged. A crash between storage and database commit can leave an unreferenced file, but cannot
mark a partially saved corpus as ready.

## Guards and limits

Server-configured IDs protect password change/reset, OTP, upload, conversation deletion, workspace
creation, member changes, self-removal and invitation acceptance. Workspace authorization denies
non-read permissions on the sample workspace, including `ManageDocuments` and `DeleteWorkspace`.
There are currently **no document-delete or workspace-edit/delete endpoints** in this repository;
future handlers must use these permissions and `IShowcasePolicy` before writing.
Ordinary users retain existing auth behavior. Sample chat history is shared.

Login allows 5 requests/minute/IP. Chat, SSE and retrieval share 6 requests/minute/IP;
showcase AI requests also share four concurrent permits held until the response ends.
Rejected requests return HTTP 429 with Retry-After when the limiter can determine it.
Built-in limiters are process-local and do not implement a daily token budget. Multiple replicas
require a shared edge limiter or distributed implementation before claiming a global limit.

Set `ReverseProxy:KnownProxies` / `REVERSE_PROXY_IP` to the immediate trusted Nginx IP, obtainable with:

```sh
docker inspect --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' omnidoc-nginx-1
```

Update after recreating the proxy. Untrusted forwarded headers are ignored; without this setting,
visitors behind a proxy share its IP budget. Do not enable `ASPNETCORE_FORWARDEDHEADERS_ENABLED`, which
clears trust restrictions. See [Microsoft rate limiting documentation](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0).

## Rebuild explicitly

Set `GEMINI_API_KEY` and optionally `GEMINI_EMBEDDING_MODEL` in the process environment, then run:

```sh
dotnet run --project backend/tools/OmniDoc.ShowcaseCorpus -- backend/ShowcaseCorpus --inspect
dotnet run --project backend/tools/OmniDoc.ShowcaseCorpus -- backend/ShowcaseCorpus
```

The inspect command prints extracted pages without an API key or network access. Preparation reads
the two PDFs, checks their 2/4 page counts, calls Gemini, validates every 768-dimensional vector,
and atomically replaces the manifest after both documents pass. It does not modify the database.
The key is read only from `GEMINI_API_KEY`; never put a key in source code or a command argument.
`text-embedding-004` was shut down on January 14, 2026; this bundle uses `gemini-embedding-2`
as configured for query embeddings ([Google model lifecycle](https://ai.google.dev/gemini-api/docs/deprecations)).
Importing changed AMRO evidence over an already seeded AMRO bundle is intentionally rejected.

Citation page numbers are physical PDF indices (1–2 and 1–4), not printed booklet numbers
(25–26 and 41–44). The default showcase viewer opens `Highlights-Booklet_Vietnamese.pdf`.

Evidence to try:

| Question | Source |
| --- | --- |
| GDP và lạm phát Việt Nam 2024–2025? | Outlook, page 2: GDP 6.0% / 6.5%; inflation 3.6% / 2.7% |
| Rủi ro từ sự phụ thuộc vào USD? | Financial Stability, page 2 |
| Già hóa dân số và vai trò của công nghệ? | Outlook, pages 3–4 |
