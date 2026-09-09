import assert from "node:assert/strict";
import { createRequire } from "node:module";
import { mkdirSync, writeFileSync } from "node:fs";
import { performance } from "node:perf_hooks";

// Real localhost stack only. No API mocks; the fault probe uses a second real API.
const require = createRequire(import.meta.url);
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || "playwright");
const { expect } = require((process.env.PLAYWRIGHT_MODULE || "playwright") + "/test");
const base = process.env.SHOWCASE_TEST_URL || "http://localhost";
assert.ok(["localhost", "127.0.0.1"].includes(new URL(base).hostname));
const wid = process.env.SHOWCASE_WORKSPACE_ID || "b4987f7e-48cc-4ba5-a117-10ac4cbced02";
const prompt = ["Doanh thu quý III đạt bao nhiêu tỷ đồng?", "Ai phê duyệt đề nghị mua sắm trước khi chuyển đến phòng tài chính?", "Lộ trình triển khai tháng 10, 11, 12 gồm những bước nào?"];
const rateMessage = "Bạn đang thao tác quá nhanh, vui lòng thử lại sau giây lát.";
const results = { at: new Date().toISOString(), target: base, landing: [], loginReadyMs: [], streams: [], checks: [] };
const out = new URL("./bin/", import.meta.url);
mkdirSync(out, { recursive: true });
const browser = await chromium.launch({ channel: process.env.BROWSER_CHANNEL || "msedge", headless: true });
const errors = [];
async function visitor() {
  const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
  const page = await context.newPage();
  page.setDefaultTimeout(15000);
  page.on("pageerror", e => errors.push(e.message));
  let loginRequests = 0, historyRequests = 0;
  page.on("request", r => {
    if (r.url().endsWith("/api/auth/login")) loginRequests++;
    if (r.url().endsWith("/messages")) historyRequests++;
  });
  await page.goto(base + "/login?mode=showcase");
  await page.getByRole("button", { name: "Sử dụng tài khoản Trải nghiệm" }).click();
  assert.equal(loginRequests, 0);
  const started = performance.now();
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await page.waitForURL(base + `/workspaces/${wid}/chat`);
  await expect(page.getByRole("button", { name: prompt[0], exact: true })).toBeEnabled();
  await expect(page.locator("iframe")).toHaveAttribute("src", /blob:.*#page=1/);
  results.loginReadyMs.push(Math.round(performance.now() - started));
  assert.equal(historyRequests, 0, "Fresh visit must not load prior messages");
  return { page, context, token: await page.evaluate(() => localStorage.getItem("omnidoc.auth.token")) };
}
async function ask(visitor, index) {
  const { page } = visitor;
  // Read a clone of the actual browser stream, measuring non-empty tokens, not setup frames.
  await page.evaluate(() => {
    window.__probe = null;
    if (window.__originalFetch) return;
    window.__originalFetch = window.fetch;
    window.fetch = async (...args) => {
      const response = await window.__originalFetch(...args);
      if (String(args[0]).endsWith("/chat/stream")) {
        const probe = window.__probe = { ttft: null, conversationId: null, done: false, error: null, citationCount: 0 };
        const reader = response.clone().body.getReader();
        void (async () => {
          let buffer = "";
          const decoder = new TextDecoder();
          while (true) {
            const { value, done } = await reader.read();
            if (done) break;
            buffer += decoder.decode(value, { stream: true });
            const frames = buffer.split(/\r?\n\r?\n/); buffer = frames.pop();
            for (const frame of frames) {
              const line = frame.split("\n").find(l => l.startsWith("data: "));
              if (!line) continue;
              const event = JSON.parse(line.slice(6));
              if (event.conversationId) probe.conversationId = event.conversationId;
              if (event.type === "token" && event.content && probe.ttft === null) probe.ttft = performance.now() - window.__clickTime;
              if (event.type === "error") probe.error = event.content;
              if (event.type === "citation") probe.citationCount++;
              if (event.type === "done") probe.done = true;
            }
          }
        })();
      }
      return response;
    };
    window.__clickTime = performance.now();
  });
  await page.evaluate(() => { window.__clickTime = performance.now(); });
  await page.getByRole("button", { name: prompt[index], exact: true }).click();
  await page.waitForFunction(() => window.__probe?.done || window.__probe?.error, null, { timeout: 120000 });
  const probe = await page.evaluate(() => window.__probe);
  results.streams.push({ prompt: index + 1, ...probe });
  assert.equal(probe.error, null, "Real Gemini stream must finish without provider error");
  assert.ok(probe.ttft > 0 && probe.citationCount > 0);
  assert.ok(probe.done);
  const expectedPage = index === 2 ? 2 : 1;
  const expectedId = `b4987f7e-48cc-4ba5-a117-10ac4cbced1${index + 1}`;
  const citation = page.getByRole("button", { name: new RegExp(`Trích dẫn trang ${expectedPage},`) }).first();
  await citation.click();
  await expect(page.locator("iframe")).toHaveAttribute("src", new RegExp(`#page=${expectedPage}`));
  // At least one citation must reference the intended canonical document.
  const messages = await api(`/workspaces/${wid}/conversations/${probe.conversationId}/messages`, undefined, visitor.token, "GET");
  const saved = await messages.json();
  assert.ok(saved.some(m => m.citations.some(c => c.documentId === expectedId && c.pageNumber === expectedPage)));
  await page.getByRole("button", { name: "Đóng nguồn" }).click();
  return probe;
}
function api(path, body, token, method = "POST", url = base) {
  return fetch(url + "/api" + path, { method, headers: { "Content-Type": "application/json", ...(token ? { Authorization: `Bearer ${token}` } : {}) }, ...(body === undefined ? {} : { body: JSON.stringify(body) }) });
}
try {
  for (let i = 0; i < 3; i++) {
    const context = await browser.newContext(); const page = await context.newPage();
    await page.goto(base);
    assert.equal(new URL(page.url()).pathname, "/");
    await expect(page.getByRole("link", { name: "Trải nghiệm ngay", exact: true }).first()).toBeVisible();
    results.landing.push(await page.evaluate(() => {
      const n = performance.getEntriesByType("navigation")[0];
      return { ttfbMs: Math.round(n.responseStart - n.startTime), loadMs: Math.round(n.loadEventEnd - n.startTime), jsBytes: performance.getEntriesByType("resource").filter(r => r.name.includes(".js")).reduce((n, r) => n + r.encodedBodySize, 0) };
    }));
    await context.close();
  }
  const [a, b] = await Promise.all([visitor(), visitor()]);
  const [one, two] = await Promise.all([ask(a, 0), ask(b, 1)]);
  assert.notEqual(one.conversationId, two.conversationId);
  results.checks.push("Two independent browser contexts: concurrent real Gemini SSE completed with different conversation IDs and persisted citations");
  console.log("PASS concurrent real SSE and citations");
  const c = await visitor(); await ask(c, 2);
  results.checks.push("All three curated prompts resolve expected document/page");

  const token = a.token;
  const documents = await (await api(`/workspaces/${wid}/documents`, undefined, token, "GET")).json();
  for (const [path, body, method] of [
    ["/auth/change-password", { currentPassword: "OmniDoc-Showcase2026!", newPassword: "Replacement-password!" }],
    ["/auth/forgot-password", { email: "guest@omnidoc.io" }],
    ["/auth/reset-password", { email: "guest@omnidoc.io", token: "invalid", newPassword: "Replacement-password!" }],
    ["/auth/send-verification-otp", {}],
    ["/workspaces", { name: "Blocked workspace" }],
    [`/workspaces/${wid}/invitations`, { email: "blocked@example.com", role: "Member" }],
    [`/workspaces/${wid}/conversations/${one.conversationId}`, undefined, "DELETE"],
  ]) assert.equal((await api(path, body, token, method)).status, 403, path);
  const form = new FormData(); form.set("file", new Blob(["%PDF-1.7 sample"], { type: "application/pdf" }), "sample.pdf");
  assert.equal((await fetch(base + `/api/workspaces/${wid}/documents`, { method: "POST", headers: { Authorization: `Bearer ${token}` }, body: form })).status, 403);
  // This version has no delete-document or delete/update-workspace endpoint; direct attempts must fail.
  for (const path of [`/workspaces/${wid}/documents/${documents[0].id}`, `/workspaces/${wid}`]) {
    assert.ok([404, 405].includes((await api(path, undefined, token, "DELETE")).status));
  }
  assert.deepEqual(await (await api(`/workspaces/${wid}/documents`, undefined, token, "GET")).json(), documents);
  results.checks.push("Direct API account/corpus/admin mutations denied; document metadata unchanged");

  // Exercise a real backend with a deliberately invalid provider key; route only this request.
  await a.page.getByRole("button", { name: "Cuộc trò chuyện mới", exact: true }).click();
  await a.page.route("**/chat/stream", async route => {
    const response = await api(`/workspaces/${wid}/chat/stream`, route.request().postDataJSON(), token, "POST", "http://127.0.0.1:5158");
    await route.fulfill({ status: response.status, contentType: "text/event-stream", body: await response.text() });
  });
  await a.page.getByRole("button", { name: prompt[0], exact: true }).click();
  await expect(a.page.getByText(/Dịch vụ AI đang tạm thời không khả dụng/).first()).toBeVisible({ timeout: 120000 });
  await expect(a.page.getByRole("textbox", { name: "Nhập câu hỏi" })).toBeEnabled();
  await expect(a.page.locator("iframe")).toBeVisible();
  assert.equal((await api(`/workspaces/${wid}/documents/${documents[0].id}/content`, undefined, token, "GET")).status, 200);
  await a.page.unroute("**/chat/stream");
  results.checks.push("Real provider rejection shows safe message; input recovers and canonical PDF remains readable");

  // Exhaust the current window without AI cost; slow provider calls may have crossed a window.
  const denied = "/workspaces/11111111-1111-1111-1111-111111111111/chat";
  let chatLimited = false;
  for (let i = 0; i < 7; i++) {
    const response = await api(denied, { message: "rate probe" }, token);
    if (response.status === 429) { chatLimited = true; break; }
    assert.equal(response.status, 403);
  }
  assert.ok(chatLimited);
  await b.page.getByRole("textbox", { name: "Nhập câu hỏi" }).fill("rate probe");
  const limitedChat = b.page.waitForResponse(r => r.url().endsWith("/chat/stream"));
  await b.page.getByRole("button", { name: "Gửi câu hỏi" }).click();
  const limited = await limitedChat;
  assert.equal(limited.status(), 429);
  assert.ok(limited.headers()["retry-after"]);
  await expect(b.page.getByText(rateMessage).first()).toBeVisible();
  results.checks.push("Chat budget exhausted: real HTTP 429 + Retry-After and friendly UI");
  let loginLimited = false;
  for (let i = 0; i < 6; i++) {
    const response = await api("/auth/login", { email: "guest@omnidoc.io", password: "wrong" });
    if (response.status === 429) { loginLimited = true; break; }
    assert.equal(response.status, 401);
  }
  assert.ok(loginLimited);
  const d = await browser.newContext(); const login = await d.newPage();
  await login.goto(base + "/login");
  await login.getByRole("button", { name: "Sử dụng tài khoản Trải nghiệm" }).click();
  const limitedLogin = login.waitForResponse(r => r.url().endsWith("/api/auth/login"));
  await login.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  assert.equal((await limitedLogin).status(), 429);
  await expect(login.getByText(rateMessage)).toBeVisible();
  results.checks.push("Login budget exhausted: real HTTP 429 and friendly UI");
  assert.deepEqual(errors, []);
  console.log("PASS corpus guards, provider degradation, login/chat rate limits and UI recovery");
} finally {
  writeFileSync(new URL("result.json", out), JSON.stringify(results, null, 2));
  await browser.close();
}
