# Converter smoke check

Run against a disposable Gotenberg instance configured like `docker-compose.yml`:

```sh
dotnet run --project backend/tools/OmniDoc.ConversionSmoke -- http://localhost:53007/
```

The runner uses the real normalizers and PDF parser, verifies multi-page TXT,
Markdown and DOCX conversion, Vietnamese text extraction, and 16:9/4:3 PPTX slide
dimensions with hidden slides and speaker notes excluded. It keeps source/PDF bytes in
memory. It does not access the application database or document volume.

It also checks paginated CSV (UTF-8/Vietnamese and Latin1), repeated table headers,
the final data row and automatic landscape above six columns, plus XLSX print
areas, page breaks and hidden-sheet exclusion. XLSX conversion explicitly sends
`singlePageSheets=false`, preserving the workbook's visual print layout.

CSV uses streaming `TextFieldParser` records with comma, semicolon or tab detected
from the first logical header. Quoted separators, escaped quotes and multiline
cells are supported; ambiguous headers and inconsistent row widths are rejected.
Blank lines are ignored. The first record is the header; up to 5,000 data records
are accepted and numbered from one in the PDF. Additional bounds are 128 columns,
16,384 characters per cell, 65,536 header characters, 8,388,608 decoded input
characters and 33,554,432 generated HTML characters. Files exceeding a limit are
rejected rather than silently truncated. Cell and header text is HTML-escaped.

CSV source bytes and SHA-256 remain unchanged. `/source` returns
`text/csv; charset=utf-8` for UTF-8 (with or without BOM) and ASCII, or
`text/csv; charset=iso-8859-1` for Latin1. Valid UTF-8 takes precedence when bytes
could be interpreted as either encoding. UTF-16 and binary controls are rejected.
XLSX source MIME is `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`.

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
