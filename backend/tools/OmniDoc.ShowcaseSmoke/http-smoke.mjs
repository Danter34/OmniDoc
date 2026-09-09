import assert from "node:assert/strict";

// Run against the isolated smoke API (port 5157), started with a fresh limiter window.
// This script never targets the normal application port or prints access tokens.
const base = "http://127.0.0.1:5157/api";
const workspace = "b4987f7e-48cc-4ba5-a117-10ac4cbced02";
const credentials = { email: "guest@omnidoc.io", password: "OmniDoc-Showcase2026!" };
const call = (path, body, token, method = "POST") => fetch(base + path, {
  method,
  headers: { "Content-Type": "application/json", ...(token ? { Authorization: `Bearer ${token}` } : {}) },
  ...(body === undefined ? {} : { body: JSON.stringify(body) }),
});

let response = await call("/auth/login", credentials);
assert.equal(response.status, 200);
const { token } = await response.json();
assert.ok(token);
console.log("PASS real password login/JWT");

for (const [path, body] of [
  ["/auth/change-password", { currentPassword: credentials.password, newPassword: "Replacement-password!" }],
  ["/auth/forgot-password", { email: credentials.email }],
  ["/auth/reset-password", { email: credentials.email, token: "some-token", newPassword: "Replacement-password!" }],
  ["/auth/send-verification-otp", {}],
  ["/workspaces", { name: "Forbidden workspace", description: "smoke" }],
]) {
  response = await call(path, body, token);
  assert.equal(response.status, 403, path);
  console.log(`PASS guard ${path}`);
}

const form = new FormData();
form.set("file", new Blob(["%PDF-1.7 sample"], { type: "application/pdf" }), "sample.pdf");
response = await fetch(`${base}/workspaces/${workspace}/documents`, { method: "POST", headers: { Authorization: `Bearer ${token}` }, body: form });
assert.equal(response.status, 403);
console.log("PASS upload guard");

response = await call(`/workspaces/${workspace}/conversations`, { title: "Showcase smoke history" }, token);
assert.equal(response.status, 201);
const conversation = await response.json();
response = await call(`/workspaces/${workspace}/conversations/${conversation.id}`, undefined, token, "DELETE");
assert.equal(response.status, 403);
console.log("PASS conversation delete guard");

response = await call(`/workspaces/${workspace}/documents`, undefined, token, "GET");
assert.equal(response.status, 200);
const documents = await response.json();
assert.equal(documents.length, 2);
for (const document of documents) {
  response = await call(`/workspaces/${workspace}/documents/${document.id}/content`, undefined, token, "GET");
  assert.equal(response.status, 200);
  assert.ok(response.headers.get("content-type").includes("application/pdf"));
  assert.ok((await response.arrayBuffer()).byteLength > 0);
}
console.log("PASS all three canonical PDFs available");

// Permission denial avoids AI calls while still exercising the actual middleware/endpoint policies.
for (let i = 0; i < 6; i++) {
  response = await call("/workspaces/11111111-1111-1111-1111-111111111111/chat", { message: "smoke", topK: 4 }, token);
  assert.equal(response.status, 403);
}
response = await call(`/workspaces/${workspace}/chat/stream`, { message: "smoke", topK: 4 }, token);
assert.equal(response.status, 429);
console.log("PASS chat/SSE shared IP budget (7th request returns 429)");

for (let i = 0; i < 4; i++) {
  response = await call("/auth/login", { ...credentials, password: "incorrect" });
  assert.equal(response.status, 401);
}
response = await call("/auth/login", { ...credentials, password: "incorrect" });
assert.equal(response.status, 429);
assert.ok(response.headers.get("retry-after"));
console.log("PASS login IP budget (6th request returns 429 + Retry-After)");
