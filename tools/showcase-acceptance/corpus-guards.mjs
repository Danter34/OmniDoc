import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { readFileSync } from "node:fs";

const base = "http://localhost/api";
const wid = "b4987f7e-48cc-4ba5-a117-10ac4cbced02";
const user = "b4987f7e-48cc-4ba5-a117-10ac4cbced01";
const manifest = JSON.parse(readFileSync(new URL("../../backend/ShowcaseCorpus/manifest.json", import.meta.url)));
const login = await fetch(base + "/auth/login", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ email: "guest@omnidoc.io", password: "OmniDoc-Showcase2026!" }) });
assert.equal(login.status, 200);
const { token } = await login.json();
const headers = { Authorization: `Bearer ${token}`, "Content-Type": "application/json" };
for (const document of manifest.documents) {
  for (const [kind, hash] of [["content", document.canonicalSha256], ["source", document.sourceSha256]]) {
    const response = await fetch(`${base}/workspaces/${wid}/documents/${document.id}/${kind}`, { headers });
    assert.equal(response.status, 200);
    assert.equal(createHash("sha256").update(Buffer.from(await response.arrayBuffer())).digest("hex"), hash);
  }
}
for (const [path, body, method, status] of [
  [`/workspaces/${wid}/members/${user}/role`, { role: "Member" }, "PATCH", 403],
  [`/workspaces/${wid}/members/${user}`, undefined, "DELETE", 403],
  [`/workspaces/${wid}/invitations`, { email: "blocked@omnidoc.test", role: "Member" }, "POST", 403],
  [`/workspaces/${wid}`, { name: "Blocked rename" }, "PATCH", 405],
  [`/workspaces/${wid}`, undefined, "DELETE", 405],
  [`/workspaces/${wid}/documents/${manifest.documents[0].id}`, undefined, "DELETE", 405],
]) {
  const response = await fetch(base + path, { method, headers, ...(body ? { body: JSON.stringify(body) } : {}) });
  assert.ok(response.status === status || (status === 405 && response.status === 404), `${method} ${path}: ${response.status}`);
}
const form = new FormData();
form.set("file", new Blob([readFileSync(new URL("../../backend/ShowcaseCorpus/northstar-report.pdf", import.meta.url))], { type: "application/pdf" }), "report.pdf");
assert.equal((await fetch(`${base}/workspaces/${wid}/documents`, { method: "POST", headers: { Authorization: `Bearer ${token}` }, body: form })).status, 403);
console.log("PASS all six source/canonical SHA256 hashes match corpus; upload, membership and unsupported mutations fail");
