import assert from "node:assert/strict";
import { createRequire } from "node:module";

const require = createRequire(import.meta.url);
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || "./bin/browser-deps/node_modules/playwright");
const { expect } = require((process.env.PLAYWRIGHT_MODULE || "./bin/browser-deps/node_modules/playwright") + "/test");
const base = process.env.SHOWCASE_TEST_URL || "http://localhost";
assert.ok(["localhost", "127.0.0.1"].includes(new URL(base).hostname));
const email = `system-auth-${Date.now()}@omnidoc.test`;
const password = "System-Auth2026!";
const changedPassword = "Changed-Auth2026!";
const resetPassword = "Reset-Auth2026!";
const currentReuse = "Mật khẩu mới không được trùng với mật khẩu hiện tại.";
const resetReuse = "Mật khẩu mới không được trùng với mật khẩu cũ gần nhất.";
const invalidLogin = "Email hoặc mật khẩu không chính xác.";
const checks = [];

async function api(path, body, token) {
  const response = await fetch(`${base}/api/auth${path}`, {
    method: body === undefined ? "GET" : "POST",
    headers: { "Content-Type": "application/json", ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  return { status: response.status, data: await response.json() };
}

async function root(token) {
  return fetch(base, { redirect: "manual", headers: token ? { Cookie: `omnidoc.session=${encodeURIComponent(token)}` } : {} });
}

async function resetLink(debugUrl) {
  if (debugUrl) return new URL(debugUrl, base);
  for (let attempt = 0; attempt < 30; attempt++) {
    const inbox = await (await fetch("http://localhost:8025/api/v1/messages?limit=50")).json();
    for (const item of inbox.messages.filter(item => item.To.some(recipient => recipient.Address === email))) {
      const message = await (await fetch(`http://localhost:8025/api/v1/message/${item.ID}`)).json();
      const link = message.HTML.match(/href="([^"]*reset-password[^\"]*)"/);
      if (link) return new URL(link[1].replaceAll("&amp;", "&"), base);
    }
    await new Promise(resolve => setTimeout(resolve, 1000));
  }
  throw new Error("Password reset email was not delivered to the local Mailpit inbox.");
}

const registration = await api("/register", { email, password, fullName: "Kiểm thử hệ thống" });
assert.equal(registration.status, 201);
const oldToken = registration.data.token;
assert.equal((await root()).status, 200);
assert.equal((await root("invalid-token")).status, 200);
const redirect = await root(oldToken);
assert.equal(redirect.status, 307);
assert.equal(redirect.headers.get("location"), "/workspaces");
checks.push("Root: guest/invalid cookie returns landing; valid session returns HTTP 307 to /workspaces");

const reuse = await api("/change-password", { currentPassword: password, newPassword: password }, oldToken);
assert.equal(reuse.status, 400);
assert.deepEqual(reuse.data.errors, [currentReuse]);
assert.equal((await api("/me", undefined, oldToken)).status, 200);
const duplicate = await api("/register", { email, password, fullName: "Kiểm thử hệ thống" });
assert.equal(duplicate.status, 409);
assert.deepEqual(duplicate.data.errors, ["Địa chỉ email này đã được đăng ký trong hệ thống."]);
const unauthorized = await api("/me");
assert.equal(unauthorized.status, 401);
assert.deepEqual(unauthorized.data.errors, ["Bạn không có quyền thực hiện thao tác này."]);
checks.push("API: password reuse, duplicate email and unauthorized responses are Vietnamese");

const browser = await chromium.launch({ channel: process.env.BROWSER_CHANNEL || "msedge", headless: true });
const legacy = await browser.newContext();
await legacy.addInitScript(token => localStorage.setItem("omnidoc.auth.token", token), oldToken);
const legacyPage = await legacy.newPage();
await legacyPage.goto(base).catch(error => {
  if (!error.message.includes("ERR_ABORTED")) throw error;
});
await legacyPage.waitForURL(`${base}/workspaces`);
await legacy.close();
checks.push("Browser: existing localStorage session migrates to a cookie and redirects from root");
const context = await browser.newContext();
const page = await context.newPage();
page.setDefaultTimeout(20000);
const errors = [];
page.on("pageerror", error => errors.push(error.message));
try {
  await page.goto(`${base}/login`);
  await page.locator('input[type="email"]').fill(email);
  await page.locator('input[autocomplete="current-password"]').fill("Wrong-Auth2026!");
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await expect(page.locator("main").getByRole("alert")).toHaveText(invalidLogin);
  await page.locator('input[autocomplete="current-password"]').fill(password);
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await page.waitForURL(`${base}/workspaces`);
  assert.ok((await context.cookies()).some(cookie => cookie.name === "omnidoc.session"));
  await page.goto(base);
  await page.waitForURL(`${base}/workspaces`);
  checks.push("Browser: wrong login shows exact Vietnamese error; successful login writes cookie and root redirects");

  await page.getByRole("button", { name: /Mở menu tài khoản/ }).click();
  await page.getByRole("menuitem", { name: "Đổi mật khẩu" }).click();
  await page.locator('input[autocomplete="current-password"]').fill(password);
  const passwordInputs = page.locator('input[autocomplete="new-password"]');
  await passwordInputs.nth(0).fill(password);
  await passwordInputs.nth(1).fill(password);
  await expect(page.locator("#password-reuse-error")).toHaveText(currentReuse);
  await expect(passwordInputs.nth(0)).toHaveAttribute("aria-invalid", "true");
  await expect(page.getByRole("button", { name: "Đổi mật khẩu", exact: true })).toBeDisabled();
  await passwordInputs.nth(0).fill(changedPassword);
  await passwordInputs.nth(1).fill(changedPassword);
  const changeResponse = page.waitForResponse(response => response.url().endsWith("/api/auth/change-password"));
  await page.getByRole("button", { name: "Đổi mật khẩu", exact: true }).click();
  assert.equal((await changeResponse).status(), 200);
  await expect(page.getByRole("heading", { name: "Đổi mật khẩu thành công" })).toBeVisible();
  assert.equal((await root(oldToken)).status, 200);
  const newToken = await page.evaluate(() => localStorage.getItem("omnidoc.auth.token"));
  assert.equal((await root(newToken)).status, 307);
  checks.push("Browser: inline reuse validation; successful password change refreshes cookie and revokes old session");

  await expect(page.getByRole("dialog")).toHaveCount(0);
  await page.getByRole("button", { name: /Mở menu tài khoản/ }).click();
  await page.getByRole("menuitem", { name: "Đăng xuất" }).click();
  await page.waitForURL(/\/login/);
  assert.ok(!(await context.cookies()).some(cookie => cookie.name === "omnidoc.session"));
  await page.goto(base);
  assert.equal(new URL(page.url()).pathname, "/");
  checks.push("Browser: logout clears session cookie and restores landing");

  await page.goto(`${base}/forgot-password`);
  await page.locator('input[type="email"]').fill(email);
  const forgotResponse = page.waitForResponse(response => response.url().endsWith("/api/auth/forgot-password"));
  await page.getByRole("button", { name: "Gửi liên kết đặt lại" }).click();
  const forgot = await (await forgotResponse).json();
  await expect(page.getByRole("heading", { name: "Kiểm tra hộp thư của bạn" })).toBeVisible();
  await expect(page.getByText("Nếu email tồn tại, hướng dẫn đặt lại mật khẩu đã được gửi.")).toBeVisible();
  await expect(page.getByRole("button", { name: "Gửi lại hoặc dùng email khác" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Quay lại đăng nhập" })).toBeVisible();
  await expect(page.getByText(/Recruiter Demo Mode|Demo: Mở trang|Mở hộp thư Mailpit/i)).toHaveCount(0);
  await expect(page.locator('a[href*="reset-password"], a[href*="8025"]')).toHaveCount(0);
  checks.push("Browser: forgot-password success contains production UI and no demo card or links");

  const link = await resetLink(forgot.debugResetUrl);
  await page.goto(`${base}${link.pathname}${link.search}`);
  const resetInputs = page.locator('input[autocomplete="new-password"]');
  await resetInputs.nth(0).fill(changedPassword);
  await resetInputs.nth(1).fill(changedPassword);
  const resetResponse = page.waitForResponse(response => response.url().endsWith("/api/auth/reset-password"));
  await page.getByRole("button", { name: "Đặt lại mật khẩu", exact: true }).click();
  assert.equal((await resetResponse).status(), 400);
  await expect(page.locator("main").getByRole("alert")).toHaveText(resetReuse);
  await resetInputs.nth(0).fill(resetPassword);
  await resetInputs.nth(1).fill(resetPassword);
  await page.getByRole("button", { name: "Đặt lại mật khẩu", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Đặt lại mật khẩu thành công" })).toBeVisible();
  assert.equal((await root(newToken)).status, 200);
  const consumed = await api("/reset-password", { email, token: link.searchParams.get("token"), newPassword: "Another-Auth2026!" });
  assert.equal(consumed.status, 400);
  assert.equal((await api("/login", { email, password: resetPassword })).status, 200);
  checks.push("Browser/API: reset reuse is rejected in Vietnamese; retry works; consumed token and old sessions are rejected");
  assert.deepEqual(errors, []);
  console.log(JSON.stringify({ checks, browserErrors: errors.length }, null, 2));
} finally {
  await browser.close();
}
