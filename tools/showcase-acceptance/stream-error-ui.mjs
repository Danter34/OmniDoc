import assert from "node:assert/strict";
import { createRequire } from "node:module";
import { readFileSync } from "node:fs";
const require = createRequire(import.meta.url);
const modulePath = process.env.PLAYWRIGHT_MODULE || "playwright";
const { chromium } = require(modulePath);
const { expect } = require(modulePath + "/test");
const wid = "b4987f7e-48cc-4ba5-a117-10ac4cbced02";
const message = "Dịch vụ AI đang tạm thời không khả dụng. Vui lòng thử lại sau giây lát. Bạn vẫn có thể đọc tài liệu trong trình xem.";
const browser = await chromium.launch({ channel: "msedge", headless: true });
try {
  const page = await browser.newPage();
  let historyReads = 0;
  await page.addInitScript(() => localStorage.setItem("omnidoc.auth.token", "ui-test-only"));
  await page.route("**/api/**", async route => {
    const path = new URL(route.request().url()).pathname;
    let body = [];
    if (path.endsWith("/auth/me")) body = { id: "user", email: "guest@omnidoc.io", fullName: "UI test", emailConfirmed: true };
    else if (path === "/api/workspaces") body = [{ id: wid, name: "Showcase", role: "Owner", documentCount: 1, createdAtUtc: new Date().toISOString() }];
    else if (path.endsWith("/documents")) body = [{ id: "pdf", workspaceId: wid, fileName: "report.pdf", title: "Report", status: "Indexed", detectedFormat: "Pdf", processingStage: "Indexed", progressPercentage: 100 }];
    else if (path.endsWith("/content")) return route.fulfill({ contentType: "application/pdf", body: readFileSync(new URL("../../backend/ShowcaseCorpus/northstar-report.pdf", import.meta.url)) });
    else if (path.endsWith("/chat/stream")) return route.fulfill({ contentType: "text/event-stream", body: [{ type: "token", content: "", conversationId: "failed-conversation" }, { type: "error", content: message }].map(e => `data: ${JSON.stringify(e)}\n\n`).join("") });
    else if (path.endsWith("/messages")) {
      historyReads++;
      body = [{ id: "user-message", conversationId: "failed-conversation", role: "User", content: "Question", citations: [], createdAtUtc: new Date().toISOString() }];
    }
    await route.fulfill({ contentType: "application/json", body: JSON.stringify(body) });
  });
  await page.route("**/hubs/**", route => route.fulfill({ status: 503 }));
  await page.goto(`http://127.0.0.1:3103/workspaces/${wid}/chat`);
  await page.getByRole("button", { name: "Doanh thu quý III đạt bao nhiêu tỷ đồng?", exact: true }).click();
  await expect.poll(() => historyReads).toBeGreaterThan(0);
  await expect(page.getByText("Question", { exact: true })).toBeVisible();
  await expect(page.getByText(message, { exact: true })).toBeVisible();
  await expect(page.getByRole("textbox", { name: "Nhập câu hỏi" })).toBeEnabled();
  await expect(page.locator("iframe")).toBeVisible();
  await page.getByRole("button", { name: "Cuộc trò chuyện mới", exact: true }).click();
  await expect(page.getByText(message, { exact: true })).toHaveCount(0);
  assert.ok(historyReads > 0);
  console.log("PASS injected post-setup provider error remains visible after history refresh; fresh chat clears it; input/PDF remain usable");
} finally { await browser.close(); }
