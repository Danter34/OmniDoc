-- Run after AddDocumentArtifactModel in the disposable seeded database.
\set ON_ERROR_STOP on
DO $$
BEGIN
    IF (SELECT count(*) FROM "DocumentArtifacts") <> 8 THEN
        RAISE EXCEPTION 'Expected two artifacts for each legacy document';
    END IF;
    IF EXISTS (
        SELECT 1 FROM "Documents" d
        LEFT JOIN "DocumentArtifacts" s ON s."Id" = d."SourceArtifactId"
        LEFT JOIN "DocumentArtifacts" c ON c."Id" = d."CanonicalArtifactId"
        WHERE s."Id" IS NULL OR c."Id" IS NULL OR s."Id" = c."Id"
           OR s."DocumentId" <> d."Id" OR c."DocumentId" <> d."Id"
           OR s."Kind" <> 0 OR c."Kind" <> 1
           OR s."StoragePath" <> d."StoragePath" OR c."StoragePath" <> d."StoragePath"
           OR s."FileName" <> d."FileName" OR c."FileName" NOT LIKE '%.pdf'
           OR s."Sha256" <> '' OR c."Sha256" <> ''
           OR d."DetectedFormat" <> 0
           OR d."ProcessingStage" <> CASE d."Status" WHEN 2 THEN 5 WHEN 3 THEN 6 WHEN 1 THEN 2 ELSE 0 END
           OR d."ProgressPercentage" <> CASE d."Status" WHEN 2 THEN 100 WHEN 3 THEN -1 WHEN 1 THEN 10 ELSE 0 END
    ) THEN RAISE EXCEPTION 'Legacy artifact mapping changed'; END IF;
    IF (SELECT count(*) FROM "DocumentChunks" WHERE "PageNumber" = 7 AND "Content" = 'Existing page-seven evidence') <> 4 THEN
        RAISE EXCEPTION 'Legacy chunks changed';
    END IF;
END $$;
SELECT 'Legacy backfill verified: 4 documents, 8 artifacts, original paths and page-seven chunks preserved.' AS result;
