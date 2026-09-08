# Local showcase acceptance

These checks use the real Docker API, PostgreSQL, seeded Gemini corpus and browser SSE.
Run only on a disposable localhost stack: they create conversations/test accounts, consume a small
number of Gemini calls and intentionally exhaust the login/chat IP windows. No credentials/tokens
are written to results. Public demo defaults are used; adjust the scripts for custom showcase IDs.

Prerequisites: `SHOWCASE_ENABLED=true`, `SHOWCASE_SEED_ON_STARTUP=true`, a valid `GEMINI_API_KEY`,
the matching `gemini-embedding-2` model and `EMAIL_SHOW_DEMO_OTP=true` for local normal-auth checks.
Rebuild the frontend after changing showcase variables. Install Playwright in a temporary tools
directory, then set `PLAYWRIGHT_MODULE` to its absolute package directory. Edge is the default;
`BROWSER_CHANNEL=chrome` selects installed Chrome instead. The application gains no browser dependency.

From the repository root:

```powershell
docker compose config --quiet
docker compose build
docker compose up -d db redis gotenberg mailpit
docker compose run --rm --no-deps backend --migrate-only=true
docker compose up -d
# Restart the API to begin with empty in-memory limiter windows.
docker compose restart backend
docker compose exec -T nginx nginx -s reload
docker compose run -d --no-deps --name omnidoc-fault-backend -e AiSettings__Gemini__ApiKey=invalid-showcase-test-key -e Showcase__SeedOnStartup=false -p 127.0.0.1:5158:8080 backend
node tools/showcase-acceptance/browser.mjs
# Run these after the rate window recovers (one minute), or restart the backend first.
node tools/showcase-acceptance/normal-auth.mjs
Get-Content tools/showcase-acceptance/corpus-snapshot.sql | docker compose exec -T db psql -U omnidoc -d omnidoc_db -At
node tools/showcase-acceptance/corpus-guards.mjs
Get-Content tools/showcase-acceptance/corpus-snapshot.sql | docker compose exec -T db psql -U omnidoc -d omnidoc_db -At
python tools/showcase-acceptance/secret-scan.py
docker rm -f omnidoc-fault-backend
```

Wait for `/api/health` after API startup. Compare corpus fingerprints before/after mutations.
Browser measurements are in ignored `tools/showcase-acceptance/bin/result.json`; TTFT starts just
before clicking a prompt and stops at the first non-empty token, excluding the empty setup frame.
Login readiness means prompt enabled plus fetched canonical PDF mounted as an iframe, not a guarantee
that every browser's native PDF engine has finished painting. Landing uses the Navigation Timing API.
The provider-error probe forwards just one browser request to the real second API with an invalid key;
it does not synthesize the backend response. Unit tests separately inject upstream HTTP 429/503.

`fresh-ready.mjs` checks the rebuilt stack without sending chat or creating data. `stream-error-ui.mjs`
is a separate mocked-response browser regression against a production frontend on port 3103: it
injects an error after the conversation setup frame and verifies that history refresh preserves it.

`secret-scan.py` checks tracked files and reachable history for configured local secrets and selected
credential patterns; it is not an exhaustive secret-detection service.

## Rebuilding a disposable stack from empty volumes

This deletes OmniDoc database records, uploads and Redis state. Use only when a clean reset is intended.
After `docker compose down --volumes --rmi all --remove-orphans`, pull service images, build using
`docker compose build --no-cache`, then follow the migration/start sequence above. The migration command
applies existing EF migrations; it exits before starting the API, seeder and background workers.

After recreating Nginx, obtain its address with
`docker inspect omnidoc-nginx-1 --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}'`,
set local `REVERSE_PROXY_IP` to that exact address and recreate the backend. Reload Nginx afterwards.
This trusts the immediate proxy for per-client IP limits; leave other proxies untrusted.

Mailpit binds localhost only. Keep debug OTP disabled for public deployments unless deliberately
providing the demonstration flow; configure real SMTP independently. Public use still needs a deliberate
AI spending budget: request/concurrency limits do not enforce a daily monetary/token cap.
