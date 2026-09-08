-- Use only in a disposable database migrated to AddWorkspaceAdminRole.
\set ON_ERROR_STOP on
INSERT INTO "Users" ("Id", "Email", "PasswordHash", "FullName", "CreatedAtUtc")
VALUES ('10000000-0000-0000-0000-000000000001', 'migration@example.test', 'unused', 'Migration test', now());
INSERT INTO "Workspaces" ("Id", "Name", "OwnerId", "CreatedAtUtc")
VALUES ('20000000-0000-0000-0000-000000000001', 'Migration test', '10000000-0000-0000-0000-000000000001', now());
INSERT INTO "Documents" ("Id", "WorkspaceId", "Title", "FileName", "ContentType", "FileSizeBytes", "StoragePath", "Status", "ChunkCount", "CreatedAtUtc")
SELECT gen_random_uuid(), '20000000-0000-0000-0000-000000000001', 'Legacy ' || s,
       'legacy-' || s || '.PDF', 'application/pdf', 1024, 'workspaces/legacy-' || s || '.pdf', s, 1, now()
FROM generate_series(0, 3) s;
INSERT INTO "DocumentChunks" ("Id", "DocumentId", "ChunkIndex", "PageNumber", "Content", "CreatedAtUtc")
SELECT gen_random_uuid(), "Id", 0, 7, 'Existing page-seven evidence', now() FROM "Documents";
