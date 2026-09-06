# Converter smoke check

Run against a disposable Gotenberg instance configured like `docker-compose.yml`:

```sh
dotnet run --project backend/tools/OmniDoc.ConversionSmoke -- http://localhost:53007/
```

The runner uses the real normalizers and PDF parser, verifies multi-page TXT,
Markdown and DOCX conversion, Vietnamese text extraction, and 16:9/4:3 PPTX slide
dimensions with hidden slides and speaker notes excluded. It keeps source/PDF bytes in
memory. It does not access the application database or document volume.

For a migration check, use a separate empty PostgreSQL 17 database: migrate it to
`20260904113602_AddWorkspaceAdminRole`, execute `legacy-seed.sql` with psql, migrate
to the latest version, then execute `legacy-verify.sql`. Pass the disposable database
connection explicitly to `dotnet ef database update --connection ...`. The seed is
test data and must never be run against an application database.

Office conversion uses `updateIndexes=false`, `exportNotes=false`,
`exportNotesPages=false`, and `exportHiddenSlides=false`; no paper-size override is
sent, preserving the slide canvas. Preflight rejects macro extensions/VBA payloads,
OLE/encrypted packages, extension/content mismatch, unsafe or duplicate ZIP paths,
and packages above 2048 entries, 64 MiB per entry, 256 MiB total, 200:1 compression
ratio, or 8 MiB for inspected metadata XML. OLE under an OOXML extension receives
`PasswordRequired` (legacy binary Office is also unsupported). Conversion errors
are `ConversionFailed` (400/invalid output), `ConversionTimeout` (client timeout,
408/504), or `ConverterUnavailable` (network, 429/5xx including 503).

LibreOffice font substitution can change Word pagination even when indexes are
not updated. Stored Canonical PDFs remain the citation source of truth.
See [Gotenberg LibreOffice options](https://gotenberg.dev/docs/convert-with-libreoffice/convert-to-pdf).
