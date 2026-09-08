# Isolated showcase smoke checks

The runner is deliberately pinned to `127.0.0.1:55439/omnidoc_showcase_smoke`, with the temporary
password below. It applies existing migrations, imports the corpus twice using separate DbContexts,
checks persisted counts/password stability and exercises real pgvector retrieval for three questions.
Set `GEMINI_API_KEY` in the process environment; it is used only for query embeddings and never logged.

From the repository root:

```sh
docker run --rm -d --name omnidoc-showcase-db-smoke -p 127.0.0.1:55439:5432 --tmpfs /var/lib/postgresql/data -e POSTGRES_DB=omnidoc_showcase_smoke -e POSTGRES_USER=omnidoc -e POSTGRES_PASSWORD=smoke-only-local pgvector/pgvector:pg17
dotnet run --project backend/tools/OmniDoc.ShowcaseSmoke -- backend/ShowcaseCorpus/manifest.json
```

`http-smoke.mjs` targets only `http://127.0.0.1:5157`. Start the API on that address with:

- `ConnectionStrings__DefaultConnection` matching the temporary DB above.
- `Showcase__Enabled=true`, `Showcase__SeedOnStartup=true`, `Showcase__Password=OmniDoc-Showcase2026!`.
- `Showcase__CorpusPath` set to the absolute manifest path.
- `FileStorage__RootPath` set to the artifact directory printed by the runner.
- `ASPNETCORE_ENVIRONMENT=Production` and a temporary `JwtSettings__Secret` of at least 32 bytes.

Then run `node backend/tools/OmniDoc.ShowcaseSmoke/http-smoke.mjs`. The API checks use no AI calls:
they verify real login, mutation guards, PDF reads, and shared login/chat/SSE rate windows. Restart the
temporary API before re-running so limiter counters start empty. Unit tests separately verify shared
concurrency permits and release behavior.

Stop the temporary API, then `docker stop omnidoc-showcase-db-smoke`. The container and tmpfs database
are removed automatically. Artifacts remain under the smoke runner's ignored `bin/` directory.
