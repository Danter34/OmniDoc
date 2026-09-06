# Converter smoke check

Run against a disposable Gotenberg instance configured like `docker-compose.yml`:

```sh
dotnet run --project backend/tools/OmniDoc.ConversionSmoke -- http://localhost:53007/
```

The runner uses the real normalizer and PDF parser, verifies multi-page TXT and
Markdown conversion and Vietnamese text extraction, and keeps source/PDF bytes in
memory. It does not access the application database or document volume.

For a migration check, use a separate empty PostgreSQL 17 database: migrate it to
`20260904113602_AddWorkspaceAdminRole`, execute `legacy-seed.sql` with psql, migrate
to the latest version, then execute `legacy-verify.sql`. Pass the disposable database
connection explicitly to `dotnet ef database update --connection ...`. The seed is
test data and must never be run against an application database.
