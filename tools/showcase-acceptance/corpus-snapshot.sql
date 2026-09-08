-- Read-only fingerprint; contains no passwords, access tokens, or document text.
WITH docs AS (
  SELECT * FROM "Documents" WHERE "WorkspaceId" = 'b4987f7e-48cc-4ba5-a117-10ac4cbced02'
), chunks AS (
  SELECT c.* FROM "DocumentChunks" c JOIN docs d ON d."Id" = c."DocumentId"
), artifacts AS (
  SELECT a.* FROM "DocumentArtifacts" a JOIN docs d ON d."Id" = a."DocumentId"
)
SELECT json_build_object(
  'documents', (SELECT count(*) FROM docs),
  'chunks', (SELECT count(*) FROM chunks),
  'artifacts', (SELECT count(*) FROM artifacts),
  'documentsDigest', (SELECT md5(string_agg(row_to_json(d)::text, '' ORDER BY d."Id")) FROM docs d),
  'chunksDigest', (SELECT md5(string_agg(row_to_json(c)::text, '' ORDER BY c."Id")) FROM chunks c),
  'artifactsDigest', (SELECT md5(string_agg(row_to_json(a)::text, '' ORDER BY a."Id")) FROM artifacts a)
);
